using Memoria.FF8.Localization;
using System.Text.Json;

if (args.FirstOrDefault() == "encoding")
{
    return EncodingCommands.Run(args);
}

if (args.FirstOrDefault() is "export" or "convert" or "patch" or "validate" or "languages")
{
    return LocalizationCommands.Run(args);
}

bool audit = args.Length == 5 && args[0] == "audit" && args[1] == "--game-dir" && args[3] == "--output";
bool refine = (args.Length == 5 || (args.Length == 7 && args[5] == "--compare")) && args[0] == "refine" && args[1] == "--input" && args[3] == "--output";
bool renumber = args.Length == 5 && args[0] == "renumber" && args[1] == "--input" && args[3] == "--output";
if (!audit && !refine && !renumber)
{
    Console.Error.WriteLine("Usage: Memoria.FF8.Localization audit --game-dir <Remastered installation> --output <new private report directory>");
    Console.Error.WriteLine("       Memoria.FF8.Localization refine --input <original audit directory> --output <new private report directory> [--compare <prior refined report directory>]");
    Console.Error.WriteLine("       Memoria.FF8.Localization renumber --input <reviewed mapping CSV> --output <new private report directory>");
    Console.Error.WriteLine("       Memoria.FF8.Localization export --game-dir <installation> --script-base <language> --target <identifier> --output <new package> [--format json|xlsx --layout single|split --references jp,en,...]");
    Console.Error.WriteLine("       Memoria.FF8.Localization convert --input <package> --output <new package> --format json|xlsx --layout single|split");
    Console.Error.WriteLine("       Memoria.FF8.Localization patch --input <base package> --output <new package> --name <patch name> [--format json|xlsx --layout single|split]");
    Console.Error.WriteLine("       Memoria.FF8.Localization validate --input <package>");
    Console.Error.WriteLine("       Memoria.FF8.Localization languages --game-dir <installation>");
    Console.Error.WriteLine("       Memoria.FF8.Localization encoding validate --input <profile.json> [--text <sample> --edition <id> --resource <family> --field <name> --dictionary <language>]");
    Console.Error.WriteLine("       Memoria.FF8.Localization encoding export --input <profile.json> --output <mod root> --name <profile folder>");
    Console.Error.WriteLine("Export references are ordered reading columns; legacy --source <language> prepends a reference.");
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
    if (renumber)
    {
        Console.WriteLine(RenumberFieldMapping.Run(args[2], args[4], cancellation.Token));
        return 0;
    }
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
