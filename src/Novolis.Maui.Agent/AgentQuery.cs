using Novolis.Maui.Agent.Protocol.Dto;

namespace Novolis.Maui.Agent;

internal static class AgentQuery
{
    public static UiGetResponseDto Get(Page page, UiGetRequestDto request)
    {
        var ids = request.ControlIds ?? [];
        var controls = new UiControlStateDto[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            var id = ids[index];
            if (string.IsNullOrWhiteSpace(id))
            {
                controls[index] = new UiControlStateDto("", false, false, false, null, null, null);
                continue;
            }

            var visual = AgentTreeWalker.FindById(page, id);
            controls[index] = visual is null
                ? new UiControlStateDto(id, false, false, false, null, null, null)
                : new UiControlStateDto(
                    id,
                    true,
                    visual.IsEnabled,
                    visual.IsVisible,
                    AgentTreeWalker.DescribeText(visual),
                    AgentProperties.GetRole(visual) ?? AgentProperties.InferRole(visual),
                    visual.GetType().Name);
        }

        return new UiGetResponseDto(
            request.RequestId,
            true,
            null,
            controls,
            page.Title,
            Environment.ProcessId);
    }

    public static UiItemsResponseDto Items(Page page, UiItemsRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ControlId))
        {
            return new UiItemsResponseDto(
                request.RequestId, false, "ControlId is required.", "", null, null, []);
        }

        var target = AgentTreeWalker.FindById(page, request.ControlId);
        if (target is null)
        {
            return new UiItemsResponseDto(
                request.RequestId, false, $"Control not found: {request.ControlId}", request.ControlId, null, null, []);
        }

        if (target is not CollectionView collection || collection.ItemsSource is not System.Collections.IEnumerable items)
        {
            return new UiItemsResponseDto(
                request.RequestId,
                false,
                $"Items not supported on {target.GetType().Name} (need CollectionView).",
                request.ControlId,
                null,
                null,
                []);
        }

        var list = items.Cast<object?>().ToList();
        var dtos = list.Select((item, index) =>
                new UiItemDto(index, item?.ToString() ?? "", Equals(collection.SelectedItem, item)))
            .ToArray();
        return new UiItemsResponseDto(
            request.RequestId,
            true,
            null,
            request.ControlId,
            "listbox",
            collection.SelectedItem is null ? null : list.IndexOf(collection.SelectedItem),
            dtos);
    }
}
