using Novolis.Maui.Agent.Protocol;
using Novolis.Maui.Agent.Protocol.Dto;

var command = args.Length > 0 ? args[0] : "tree";
await using var client = new UiAgentClient();
Exception? last = null;
for (var attempt = 1; attempt <= 20; attempt++)
{
    try
    {
        await client.ConnectDefaultAsync().ConfigureAwait(false);
        last = null;
        break;
    }
    catch (Exception exception)
    {
        last = exception;
        await Task.Delay(250).ConfigureAwait(false);
    }
}

if (!client.IsConnected)
{
    Console.Error.WriteLine($"Could not connect to the MAUI agent: {last?.Message}");
    Console.Error.WriteLine("Start the host with NOVOLIS_MAUI_AGENT=1 first.");
    return 1;
}

var hello = await client.HelloAsync().ConfigureAwait(false);
Console.WriteLine($"hello ok={hello.Success} title={hello.AppTitle} pid={hello.ProcessId}");
if (!hello.Success)
{
    Console.Error.WriteLine(hello.Error);
    return 2;
}

if (command.Equals("screenshot", StringComparison.OrdinalIgnoreCase))
{
    var controlId = args.Length > 1 ? args[1] : null;
    var shot = await client.ScreenshotAsync(controlId, 1600).ConfigureAwait(false);
    if (!shot.Success || shot.Png is null)
    {
        Console.Error.WriteLine(shot.Error ?? "screenshot failed");
        return 3;
    }

    var path = Path.Combine(Path.GetTempPath(), "novolis-maui-agent.png");
    await File.WriteAllBytesAsync(path, shot.Png).ConfigureAwait(false);
    Console.WriteLine($"screenshot {shot.Width}x{shot.Height} {path}");
    return 0;
}

var tree = await client.TreeAsync(interactiveOnly: false).ConfigureAwait(false);
if (!tree.Success)
{
    Console.Error.WriteLine(tree.Error);
    return 4;
}

Console.WriteLine($"tree nodes={tree.Nodes.Length}");
foreach (var node in tree.Nodes)
{
    Console.WriteLine(
        $"{node.Id}\t{node.Role}\t{node.TypeName}\t{node.Bounds.Width:0}x{node.Bounds.Height:0}\t{node.Text}");
}

return 0;
