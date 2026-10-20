using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static GameSession CreateScrollComponentSession(GameAssets assets)
    {
        GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        return session;
    }

    private static bool TryGetScrollContinuationFixture(string resource, out string path)
    {
        path = "";
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        if (string.IsNullOrWhiteSpace(catalog) || !File.Exists(catalog))
        {
            Console.WriteLine($"SKIP optional continuation {resource}: configure MEMORIA_FF8_TEST_FIXTURE_CATALOG; declared scroll components still run.");
            return false;
        }
        Dictionary<string, string> entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog)) ?? throw new InvalidDataException("The external fixture catalog must contain a JSON object.");
        if (!entries.ContainsKey(resource))
        {
            Console.WriteLine($"SKIP optional continuation {resource}: the configured catalog has no matching entry.");
            return false;
        }
        path = TestResources.FixturePath(resource);
        if (!File.Exists(path))
        {
            Console.WriteLine($"SKIP optional continuation {resource}: its explicitly configured file is unavailable.");
            return false;
        }
        return true;
    }
    private static byte[] DefaultScrollComponentProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        (Jsm.Opcode Opcode, int Parameter)[] main = operations.Select(operation => operation.Opcode == Jsm.Opcode.RET ? (Jsm.Opcode.HALT, 0) : operation).ToArray();
        main[0] = (Jsm.Opcode.LBL, 1);
        return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [.. main, (Jsm.Opcode.RET, 8)]]);
    }
}
