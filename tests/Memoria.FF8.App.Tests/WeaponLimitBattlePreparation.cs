using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class WeaponLimitBattlePreparation
{
    internal static ushort Formation
    {
        get
        {
            string? configured = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_FORMATION");
            if (string.IsNullOrWhiteSpace(configured))
            {
                return 87;
            }
            if (!ushort.TryParse(configured, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ushort formation) || formation > 1023)
            {
                throw new InvalidDataException("MEMORIA_FF8_LOADOUT_FORMATION requires an installed formation ID from zero through 1023.");
            }
            return formation;
        }
    }

    internal static void SaveField(GameSession session, GameWorld world, int recipient, int donor, BattleLimitFamily family, bool cardSlots, string source, string directory)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_FAMILY_PREPARE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }
        Assert.False(session.HasActiveBattle);
        Directory.CreateDirectory(output);
        long retained = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
        Assert.True(retained < 1024L * 1024 * 1024, "Family preparation reached its one-GiB output budget.");
        string path = Path.GetFullPath(Path.Combine(output, $"recipient-{recipient:00}-family-{family}{(cardSlots ? "-cards" : "")}-style-{donor:00}-encounter-{Formation:000}-field.ff8save"));
        session.SetEventState(world.Events.Capture());
        session.Save(path, compact: true);
        File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new
        {
            SourceResource = "weapon-limit-field-source",
            SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))),
            StandalonePath = path,
            Field = session.State.FieldId,
            FieldFrame = world.Events.Frame,
            Recipient = recipient,
            Donor = donor,
            Formation,
            Family = family.ToString(),
            CardSlots = cardSlots,
            Stage = "FieldNativeFamily",
            Boundary = "Standalone field with explicit detached laboratory unlocks/loadouts and one pending ordinary native encounter. StartEncounter and live native controller inputs are next; no battle PCs serialized or replayed.",
            Resume = $"Memoria.FF8.exe --state \"{path}\" --game-dir \"{directory}\""
        }));
    }
}
