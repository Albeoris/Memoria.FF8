using Memoria.FF8.Bootstrap;
using System.Runtime.InteropServices;

using CancellationTokenSource stopping = new();
using PosixSignalRegistration? terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    stopping.Cancel();
});
using PosixSignalRegistration? hangup = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
{
    context.Cancel = true;
    stopping.Cancel();
});
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping.Cancel();
};
try
{
    if (args.FirstOrDefault() == UnixHostProcess.Command)
    {
        UnixHostProcess.Execute(args);
        throw new InvalidOperationException("The Unix host replacement unexpectedly returned.");
    }
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
