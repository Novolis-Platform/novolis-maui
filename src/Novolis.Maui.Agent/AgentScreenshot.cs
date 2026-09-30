using System.Reflection;
using Novolis.Maui.Agent.Protocol.Dto;

namespace Novolis.Maui.Agent;

internal static class AgentScreenshot
{
    public static async Task<UiScreenshotResponseDto> CaptureAsync(
        Page page,
        string? controlId,
        int? maxWidth,
        long requestId)
    {
        VisualElement target = page;
        if (!string.IsNullOrWhiteSpace(controlId))
        {
            var match = AgentTreeWalker.FindById(page, controlId);
            if (match is null)
                return new UiScreenshotResponseDto(requestId, false, $"Control not found: {controlId}", null, 0, 0);
            target = match;
        }

        if (TryCaptureLastFramePng(target, maxWidth, requestId, out var frame))
            return frame;

        if (!Screenshot.Default.IsCaptureSupported)
            return new UiScreenshotResponseDto(requestId, false, "Screenshot capture is not supported.", null, 0, 0);

        var shot = await Screenshot.Default.CaptureAsync().ConfigureAwait(true);
        await using var stream = await shot.OpenReadAsync().ConfigureAwait(true);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory).ConfigureAwait(true);
        var width = Math.Max(1, (int)Math.Ceiling(target.Width));
        var height = Math.Max(1, (int)Math.Ceiling(target.Height));
        if (maxWidth is > 0 && width > maxWidth.Value)
        {
            var scale = maxWidth.Value / (double)width;
            width = maxWidth.Value;
            height = Math.Max(1, (int)Math.Round(height * scale));
        }

        return new UiScreenshotResponseDto(requestId, true, null, memory.ToArray(), width, height);
    }

    private static bool TryCaptureLastFramePng(
        VisualElement root,
        int? maxWidth,
        long requestId,
        out UiScreenshotResponseDto response)
    {
        response = default!;
        foreach (var visual in Enumerate(root))
        {
            var method = visual.GetType().GetMethod(
                "TryGetLastFramePng",
                BindingFlags.Instance | BindingFlags.Public);
            if (method is null || method.ReturnType != typeof(byte[]))
                continue;
            if (method.Invoke(visual, null) is not byte[] png || png.Length == 0)
                continue;

            var width = Math.Max(1, (int)Math.Ceiling(visual.Width));
            var height = Math.Max(1, (int)Math.Ceiling(visual.Height));
            if (maxWidth is > 0 && width > maxWidth.Value)
            {
                var scale = maxWidth.Value / (double)width;
                width = maxWidth.Value;
                height = Math.Max(1, (int)Math.Round(height * scale));
            }

            response = new UiScreenshotResponseDto(requestId, true, null, png, width, height);
            return true;
        }

        return false;
    }

    private static IEnumerable<VisualElement> Enumerate(VisualElement root)
    {
        yield return root;
        if (root is not IVisualTreeElement tree)
            yield break;
        foreach (var child in tree.GetVisualChildren())
        {
            if (child is VisualElement visual)
            {
                foreach (var nested in Enumerate(visual))
                    yield return nested;
            }
        }
    }
}
