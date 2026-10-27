using Memoria.FF8.Bootstrap;

using CancellationTokenSource stopping = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping.Cancel();
};
try
{
    return await GraphicsDispatcher.RunAsync(AppContext.BaseDirectory, args, Console.Out, Console.Error, stopping.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Game launch cancelled.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Game launch failed: {exception.Message}");
    return 1;
}
