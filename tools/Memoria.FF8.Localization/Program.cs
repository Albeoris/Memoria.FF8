using Memoria.FF8.Localization;

if (args.Length != 5 || args[0] != "audit" || args[1] != "--game-dir" || args[3] != "--output")
{
    Console.Error.WriteLine("Usage: Memoria.FF8.Localization audit --game-dir <Remastered installation> --output <new private report directory>");
    return 2;
}

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
try
{
    FieldAuditReport report = RemasteredFieldAudit.Run(args[2], args[4], Console.WriteLine, cancellation.Token);
    Console.WriteLine(report.Summary);
    return report.Complete ? 0 : 1;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
