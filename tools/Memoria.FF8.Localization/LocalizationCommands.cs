using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal static class LocalizationCommands
{
    public static int Run(string[] arguments)
    {
        using CancellationTokenSource cancellation = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        try
        {
            Dictionary<string, string> options = [];
            for (int index = 1; index < arguments.Length; index += 2)
            {
                if (index + 1 >= arguments.Length || !arguments[index].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(arguments[index][2..], arguments[index + 1]))
                {
                    throw new ArgumentException("Options require distinct --name value pairs.");
                }
            }
            string Required(string name) => options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing --" + name + ".");
            string command = arguments[0];
            string[] allowed = command switch
            {
                "export" => ["game-dir", "source", "script-base", "target", "references", "name", "format", "layout", "output"],
                "convert" => ["input", "output", "format", "layout"],
                "patch" => ["input", "output", "name", "format", "layout"],
                "validate" => ["input"],
                "languages" => ["game-dir"],
                _ => throw new ArgumentException("Unknown localization command.")
            };
            if (options.Keys.Any(name => !allowed.Contains(name, StringComparer.Ordinal)))
            {
                throw new ArgumentException("Unknown localization option.");
            }
            if (command == "languages")
            {
                foreach (var source in LocalizationSourceExporter.ProbeLanguages(Required("game-dir"), cancellation.Token))
                {
                    Console.WriteLine($"{source.Language}: {(source.Available ? "available" : source.Reason)}");
                }
                return 0;
            }
            if (command == "validate")
            {
                LocalizationProject project = LocalizationProjectIO.Read(Required("input"));
                Console.WriteLine($"Valid localization {project.Manifest.ProjectId}: {project.Entries.Count} records, Target [{project.Manifest.TargetLanguage}].");
                return 0;
            }
            if (command == "convert")
            {
                LocalizationProjectIO.Convert(Required("input"), Required("output"), Required("format"), Required("layout"), cancellation.Token);
                Console.WriteLine("Converted localization project: " + Required("output"));
                return 0;
            }
            if (command == "patch")
            {
                LocalizationProject source = LocalizationProjectIO.Read(Required("input"));
                LocalizationProject patch = LocalizationProjectExport.CreatePatch(source, Required("name"));
                LocalizationProjectIO.WriteNew(patch, Required("output"), options.GetValueOrDefault("format", source.Manifest.Format), options.GetValueOrDefault("layout", source.Manifest.Layout), cancellation.Token);
                Console.WriteLine("Created disabled localization patch: " + Required("output"));
                return 0;
            }
            string target = Required("target");
            if (new[] { "en", "fr", "de", "es", "it", "ja", "jp" }.Contains(target, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("An independent translation needs a Target identifier distinct from built-in languages.");
            }
            string scriptBase = Required("script-base");
            string[] references = (options.TryGetValue("source", out string? legacyReference) ? new[] { legacyReference } : Array.Empty<string>()).Concat(options.GetValueOrDefault("references", scriptBase).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            LocalizationAuthoringRequest request = new(references.FirstOrDefault() ?? scriptBase, scriptBase, target, references, options.GetValueOrDefault("name", target));
            LocalizationProject created = LocalizationProjectBuilder.Prepare(Required("game-dir"), request, cancellation.Token, new Progress<string>(Console.WriteLine));
            LocalizationProjectIO.WriteNew(created, Required("output"), options.GetValueOrDefault("format", "json"), options.GetValueOrDefault("layout", "split"), cancellation.Token);
            int resourceDiagnostics = created.Manifest.ExtensionData.TryGetValue("sourceGaps", out System.Text.Json.JsonElement sourceGaps) && sourceGaps.ValueKind == System.Text.Json.JsonValueKind.Array ? sourceGaps.GetArrayLength() : 0;
            Console.WriteLine($"Created disabled localization: {created.Entries.Count} records, {created.Entries.Select(row => row.Category).Distinct().Count()} categories. Resource/coverage diagnostics: {resourceDiagnostics}. {Required("output")}");
            if (resourceDiagnostics > 0)
            {
                Console.WriteLine("Review sourceGaps in Localization/localization.manifest.json. Resource/coverage diagnostics include unsupported or unavailable resources and are not a count of missing translations.");
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or OperationCanceledException or System.Text.Json.JsonException or NotSupportedException or InvalidOperationException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }
}
