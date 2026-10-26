using Memoria.FF8.Localization;
using System.Text.Json;

bool audit = args.Length == 5 && args[0] == "audit" && args[1] == "--game-dir" && args[3] == "--output";
bool refine = (args.Length == 5 || (args.Length == 7 && args[5] == "--compare")) && args[0] == "refine" && args[1] == "--input" && args[3] == "--output";
if (!audit && !refine)
{
    Console.Error.WriteLine("Usage: Memoria.FF8.Localization audit --game-dir <Remastered installation> --output <new private report directory>");
    Console.Error.WriteLine("       Memoria.FF8.Localization refine --input <original audit directory> --output <new private report directory> [--compare <prior refined report directory>]");
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
    FieldAuditReport report = audit
        ? RemasteredFieldAudit.Run(args[2], args[4], Console.WriteLine, cancellation.Token)
        : RefineFieldAudit.Run(args[2], args[4], Console.WriteLine, cancellation.Token, args.Length == 7 ? args[6] : null);
    Console.WriteLine(report.Summary);
    return report.Complete ? 0 : 1;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException or JsonException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
