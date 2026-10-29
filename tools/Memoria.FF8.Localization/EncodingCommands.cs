using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.Localization;

internal static class EncodingCommands
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length < 4 || args[1] is not ("validate" or "export"))
            {
                throw new ArgumentException("Usage: encoding validate --input <profile.json> [--text <sample> --edition <id> --resource <family> --field <name> --dictionary <language>] | encoding export --input <profile.json> --output <mod> --name <folder>");
            }
            Dictionary<string, string> options = new(StringComparer.Ordinal);
            for (int i = 2; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i][2..], args[i + 1]))
                {
                    throw new ArgumentException("Options require distinct --name value pairs.");
                }
            }
            string Required(string name) => options.GetValueOrDefault(name) ?? throw new ArgumentException("Missing --" + name);
            string[] allowed = args[1] == "export" ? ["input", "output", "name"] : ["input", "text", "edition", "resource", "field", "dictionary"];
            if (options.Keys.Any(key => !allowed.Contains(key, StringComparer.Ordinal)))
            {
                throw new ArgumentException("Unknown encoding option.");
            }
            string path = Required("input");
            NativeFontProfile profile = NativeFontProfileIO.Load(path);
            List<NativeTextDiagnostic> diagnostics = [.. NativeFontProfileIO.ValidateDependencies(path, profile)];
            if (options.TryGetValue("text", out string? text))
            {
                NativeTextContext context = new(Required("edition"), Required("resource"), options.GetValueOrDefault("field"), options.GetValueOrDefault("dictionary"));
                diagnostics.AddRange(NativeStrictCodec.Encode(profile, text, context).Diagnostics);
                diagnostics.AddRange(NativeFontLayout.Layout(profile, text, context).Diagnostics);
            }
            foreach (NativeTextDiagnostic diagnostic in diagnostics)
            {
                Console.Error.WriteLine($"{diagnostic.Code} at {diagnostic.Position}: {diagnostic.Message}");
            }
            if (diagnostics.Count != 0)
            {
                return 1;
            }
            if (args[1] == "export")
            {
                foreach (string exported in EncodingProfilePackage.Export([new(path, Required("name"))], Required("output")))
                {
                    Console.WriteLine(exported);
                }
            }
            else
            {
                Console.WriteLine($"Profile '{profile.Id}' is valid. Native-style presentation and native table validation do not verify original-game deployment.");
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }
}
