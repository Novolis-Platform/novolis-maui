using Novolis.Maui.Agent.Protocol;

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

switch (command.ToLowerInvariant())
{
    case "hello":
        return 0;
    case "screenshot":
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
    case "click":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: click <controlId>");
            return 5;
        }

        var click = await client.ClickAsync(args[1]).ConfigureAwait(false);
        Console.WriteLine($"click ok={click.Success} id={click.ClickedId} {click.Error}");
        return click.Success ? 0 : 6;
    }
    case "get":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: get <id[,id]>");
            return 7;
        }

        var ids = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var get = await client.GetAsync(ids).ConfigureAwait(false);
        if (!get.Success)
        {
            Console.Error.WriteLine(get.Error);
            return 8;
        }

        foreach (var control in get.Controls)
            Console.WriteLine($"{control.Id}\tfound={control.Found}\tenabled={control.IsEnabled}\t{control.Text}");
        return 0;
    }
    default:
    {
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
    }
}
