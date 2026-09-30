using System.Reflection;
using Novolis.Maui.Agent.Protocol.Dto;

namespace Novolis.Maui.Agent;

internal static class AgentInput
{
    public static UiClickResponseDto Click(Page page, UiClickRequestDto request)
    {
        VisualElement? target;
        string? clickedId;
        if (!string.IsNullOrWhiteSpace(request.ControlId))
        {
            target = AgentTreeWalker.FindById(page, request.ControlId);
            if (target is null)
                return new UiClickResponseDto(request.RequestId, false, $"Control not found: {request.ControlId}", null);
            clickedId = request.ControlId;
        }
        else if (request.X is double x && request.Y is double y)
        {
            target = AgentTreeWalker.HitTest(page, x, y);
            if (target is null)
                return new UiClickResponseDto(request.RequestId, false, $"No control at ({x}, {y}).", null);
            clickedId = AgentProperties.GetId(target) ?? target.AutomationId ?? target.GetType().Name;
        }
        else
        {
            return new UiClickResponseDto(request.RequestId, false, "Provide ControlId or X/Y.", null);
        }

        if (!target.IsEnabled)
            return new UiClickResponseDto(request.RequestId, false, "Control is disabled.", clickedId);

        InvokePrimaryClick(target);
        return new UiClickResponseDto(request.RequestId, true, null, clickedId);
    }

    public static UiFocusResponseDto Focus(Page page, UiFocusRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ControlId))
            return new UiFocusResponseDto(request.RequestId, false, "ControlId is required.", null);

        var target = AgentTreeWalker.FindById(page, request.ControlId);
        if (target is null)
            return new UiFocusResponseDto(request.RequestId, false, $"Control not found: {request.ControlId}", null);
        if (!target.IsEnabled)
            return new UiFocusResponseDto(request.RequestId, false, "Control is disabled.", request.ControlId);

        target.Focus();
        return new UiFocusResponseDto(request.RequestId, true, null, request.ControlId);
    }

    public static UiScrollResponseDto Scroll(Page page, UiScrollRequestDto request)
    {
        VisualElement? target = string.IsNullOrWhiteSpace(request.ControlId)
            ? page
            : AgentTreeWalker.FindById(page, request.ControlId);
        if (target is null)
            return new UiScrollResponseDto(request.RequestId, false, $"Control not found: {request.ControlId}", null, null);

        var scroll = FindScrollView(target);
        if (scroll is null)
            return new UiScrollResponseDto(request.RequestId, false, "No ScrollView found for target.", null, null);

        var x = scroll.ScrollX + (request.DeltaX ?? 0);
        var y = scroll.ScrollY + (request.DeltaY ?? 0);
        _ = scroll.ScrollToAsync(Math.Max(0, x), Math.Max(0, y), false);
        return new UiScrollResponseDto(request.RequestId, true, null, x, y);
    }

    public static UiSelectResponseDto Select(Page page, UiSelectRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ControlId))
            return new UiSelectResponseDto(request.RequestId, false, "ControlId is required.");

        var target = AgentTreeWalker.FindById(page, request.ControlId);
        if (target is null)
            return new UiSelectResponseDto(request.RequestId, false, $"Control not found: {request.ControlId}");
        if (!target.IsEnabled)
            return new UiSelectResponseDto(request.RequestId, false, "Control is disabled.");

        if (target is CollectionView collection && collection.ItemsSource is System.Collections.IEnumerable items)
        {
            var list = items.Cast<object?>().ToList();
            var index = request.Index;
            if (index is null && request.ItemText is { Length: > 0 } text)
            {
                index = list.FindIndex(item =>
                    item?.ToString()?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
                if (index < 0)
                    return new UiSelectResponseDto(request.RequestId, false, "Item not found.");
            }

            if (index is null || index < 0 || index >= list.Count)
                return new UiSelectResponseDto(request.RequestId, false, "Item not found.");

            collection.SelectedItem = list[index.Value];
            return new UiSelectResponseDto(request.RequestId, true, null, index.Value, list[index.Value]?.ToString());
        }

        return new UiSelectResponseDto(
            request.RequestId,
            false,
            $"Select not supported on {target.GetType().Name} (need CollectionView).");
    }

    public static UiTypeResponseDto Type(Page page, UiTypeRequestDto request)
    {
        VisualElement? target = string.IsNullOrWhiteSpace(request.ControlId)
            ? page
            : AgentTreeWalker.FindById(page, request.ControlId);
        if (target is null)
            return new UiTypeResponseDto(request.RequestId, false, $"Control not found: {request.ControlId}");

        target.Focus();
        if (request.Text is not null)
        {
            switch (target)
            {
                case Entry entry:
                    entry.Text = request.Clear ? request.Text : (entry.Text ?? string.Empty) + request.Text;
                    break;
                case Editor editor:
                    editor.Text = request.Clear ? request.Text : (editor.Text ?? string.Empty) + request.Text;
                    break;
                case SearchBar search:
                    search.Text = request.Clear ? request.Text : (search.Text ?? string.Empty) + request.Text;
                    break;
                default:
                    return new UiTypeResponseDto(request.RequestId, false, $"Cannot type into {target.GetType().Name}.");
            }
        }

        if (request.Keys is { Length: > 0 })
        {
            foreach (var key in request.Keys)
            {
                switch (key.Trim().ToLowerInvariant())
                {
                    case "enter" or "return":
                        SendCompleted(target);
                        break;
                    case "tab":
                        break;
                    case "escape":
                        break;
                    default:
                        return new UiTypeResponseDto(
                            request.RequestId,
                            false,
                            $"Unsupported key '{key}'.");
                }
            }
        }

        return new UiTypeResponseDto(request.RequestId, true, null);
    }

    private static void SendCompleted(VisualElement target)
    {
        switch (target)
        {
            case Entry entry:
                typeof(Entry).GetMethod(
                        "SendCompleted",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(entry, null);
                return;
            case SearchBar search:
                search.SearchCommand?.Execute(search.SearchCommandParameter);
                return;
        }
    }

    private static void InvokePrimaryClick(VisualElement target)
    {
        switch (target)
        {
            case CheckBox check:
                check.IsChecked = !check.IsChecked;
                return;
            case Switch toggle:
                toggle.IsToggled = !toggle.IsToggled;
                return;
            case Button button:
                if (button.Command is { } command && command.CanExecute(button.CommandParameter))
                {
                    command.Execute(button.CommandParameter);
                    return;
                }

                var clicked = typeof(Button).GetMethod(
                    "SendClicked",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (clicked is not null)
                {
                    clicked.Invoke(button, null);
                    return;
                }

                break;
        }

        target.Focus();
    }

    private static ScrollView? FindScrollView(Element element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is ScrollView scroll)
                return scroll;
        }

        return null;
    }
}
