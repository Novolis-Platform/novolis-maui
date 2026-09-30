using Novolis.Maui.Agent.Protocol;
using Novolis.Maui.Agent.Protocol.Dto;

namespace Novolis.Maui.Agent;

internal static class AgentTreeWalker
{
    public static UiTreeNodeDto[] Collect(Page page, bool interactiveOnly)
    {
        var nodes = new List<UiTreeNodeDto>();
        Walk(page, page, "Page", interactiveOnly, nodes);
        return nodes.ToArray();
    }

    public static VisualElement? FindById(Page page, string id)
    {
        VisualElement? found = null;
        WalkFind(page, page, "Page", id, ref found);
        return found;
    }

    public static VisualElement? HitTest(Page page, double x, double y)
    {
        VisualElement? best = null;
        var bestArea = double.MaxValue;
        foreach (var visual in Descendants(page))
        {
            if (!visual.IsVisible || AgentProperties.GetIgnore(visual))
                continue;
            var bounds = GetPageBounds(page, visual);
            if (x < bounds.X || y < bounds.Y || x > bounds.X + bounds.Width || y > bounds.Y + bounds.Height)
                continue;
            var area = bounds.Width * bounds.Height;
            if (area < bestArea && area > 0)
            {
                bestArea = area;
                best = visual;
            }
        }

        return best;
    }

    public static string? DescribeText(Element visual) => visual switch
    {
        Label label => label.Text,
        Button button => button.Text,
        Entry entry => entry.Text,
        Editor editor => editor.Text,
        SearchBar search => search.Text,
        CheckBox check => check.IsChecked.ToString(),
        _ => visual.AutomationId
    };

    private static void Walk(
        Page page,
        Element element,
        string path,
        bool interactiveOnly,
        List<UiTreeNodeDto> nodes)
    {
        if (element is BindableObject bindable && AgentProperties.GetIgnore(bindable))
            return;

        if (element is VisualElement visual)
        {
            var include = !interactiveOnly || IsInteractive(visual) || AgentProperties.GetId(visual) is not null
                || !string.IsNullOrWhiteSpace(visual.AutomationId);
            if (include)
            {
                var id = ResolveId(visual, path);
                nodes.Add(new UiTreeNodeDto(
                    id,
                    AgentProperties.GetRole(visual) ?? AgentProperties.InferRole(visual),
                    visual.GetType().Name,
                    GetPageBounds(page, visual),
                    visual.IsEnabled,
                    visual.IsVisible,
                    visual.IsFocused,
                    DescribeText(visual),
                    path));
                if (AgentProperties.GetId(visual) is not null || !string.IsNullOrWhiteSpace(visual.AutomationId))
                    AppendItemNodes(page, visual, id, path, nodes);
            }
        }

        var siblingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var child in Children(element))
        {
            var typeName = child.GetType().Name;
            siblingCounts.TryGetValue(typeName, out var index);
            siblingCounts[typeName] = index + 1;
            Walk(page, child, $"{path}/{typeName}[{index}]", interactiveOnly, nodes);
        }
    }

    private static void AppendItemNodes(
        Page page,
        VisualElement visual,
        string parentId,
        string path,
        List<UiTreeNodeDto> nodes)
    {
        try
        {
            if (visual is CollectionView collection && collection.ItemsSource is System.Collections.IEnumerable items)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (index >= 64)
                        break;
                    nodes.Add(new UiTreeNodeDto(
                        $"{parentId}[{index}]",
                        AgentRoleNames.ListItem,
                        "ListItem",
                        GetPageBounds(page, visual),
                        visual.IsEnabled,
                        visual.IsVisible,
                        Equals(collection.SelectedItem, item),
                        item?.ToString(),
                        $"{path}/Item[{index}]"));
                    index++;
                }
            }
        }
        catch
        {
            // ignore item enumeration faults
        }
    }

    private static void WalkFind(Page page, Element element, string path, string id, ref VisualElement? found)
    {
        if (found is not null || (element is BindableObject bindable && AgentProperties.GetIgnore(bindable)))
            return;

        if (element is VisualElement visual && string.Equals(ResolveId(visual, path), id, StringComparison.Ordinal))
        {
            found = visual;
            return;
        }

        var siblingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var child in Children(element))
        {
            var typeName = child.GetType().Name;
            siblingCounts.TryGetValue(typeName, out var index);
            siblingCounts[typeName] = index + 1;
            WalkFind(page, child, $"{path}/{typeName}[{index}]", id, ref found);
            if (found is not null)
                return;
        }
    }

    private static bool IsInteractive(VisualElement visual) =>
        visual is Button or Entry or Editor or SearchBar or CheckBox or Switch
            or CollectionView or Picker or Slider or ScrollView;

    private static string ResolveId(VisualElement visual, string path)
    {
        var attached = AgentProperties.GetId(visual);
        if (!string.IsNullOrWhiteSpace(attached))
            return attached;
        if (!string.IsNullOrWhiteSpace(visual.AutomationId))
            return visual.AutomationId;
        if (!string.IsNullOrWhiteSpace(visual.StyleId))
            return visual.StyleId;
        return path;
    }

    private static UiBoundsDto GetPageBounds(VisualElement root, VisualElement element)
    {
        var x = 0d;
        var y = 0d;
        Element? current = element;
        while (current is VisualElement visual)
        {
            var margin = visual is View view ? view.Margin : default;
            x += visual.X + visual.TranslationX + margin.Left;
            y += visual.Y + visual.TranslationY + margin.Top;
            if (ReferenceEquals(current, root))
                break;
            current = current.Parent;
        }

        return new UiBoundsDto(x, y, Math.Max(0, element.Width), Math.Max(0, element.Height));
    }

    private static IEnumerable<Element> Children(Element element)
    {
        if (element is IVisualTreeElement tree)
        {
            foreach (var child in tree.GetVisualChildren())
            {
                if (child is Element visual)
                    yield return visual;
            }

            yield break;
        }

        yield break;
    }

    private static IEnumerable<VisualElement> Descendants(Element root)
    {
        foreach (var child in Children(root))
        {
            if (child is VisualElement visual)
                yield return visual;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
