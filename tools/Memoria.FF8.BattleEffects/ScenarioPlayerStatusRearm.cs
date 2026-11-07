using System.Text.Json.Nodes;
using System.Text.Json;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TryConfigurePlayerStatusExecutionProfile(JsonObject recipe, BattleNativeAction action, string gameDirectory, out string? blocker, IDictionary<string, DiagnosticFreshEntryDeclaration>? sourcePins = null)
    {
        blocker = null;
        JsonObject driver = recipe["driver"]!.AsObject();
        JsonObject input = driver["input"]!.AsObject();
        if ((action.Statuses0 & 0x20) == 0 || action.StatusAttack != 255 || action.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32 || input["actor_id"]?.GetValue<string>() != input["target_id"]?.GetValue<string>())
        {
            return true;
        }
        return TryConfigureFreshEntryExecutionProfile(recipe, gameDirectory, out blocker, sourcePins);
    }

    public static bool TryConfigureFreshEntryExecutionProfile(JsonObject recipe, string gameDirectory, out string? blocker, IDictionary<string, DiagnosticFreshEntryDeclaration>? sourcePins = null)
    {
        blocker = null;
        JsonObject driver = recipe["driver"]!.AsObject();
        try
        {
            string root = Path.GetFullPath(gameDirectory);
            if (sourcePins is not null && sourcePins.TryGetValue(root, out DiagnosticFreshEntryDeclaration? cached))
            {
                cached.Validate();
                Configure(cached);
                return true;
            }
            if (File.Exists(Path.Combine(root, "main.zzz")))
            {
                throw new InvalidDataException("This source pin profile requires the actual Classic archive layout.");
            }
            List<string> relative = ["FF8_EN.exe"];
            foreach (string family in new[] { "main", "battle", "magic" })
            {
                foreach (string extension in new[] { "fs", "fi", "fl" })
                {
                    relative.Add($"Data/lang-en/{family}.{extension}");
                }
            }
            ImmutableArray<DiagnosticFreshEntrySourceFile>.Builder pins = ImmutableArray.CreateBuilder<DiagnosticFreshEntrySourceFile>();
            foreach (string name in relative)
            {
                string path = Path.GetFullPath(name, root);
                for (FileSystemInfo? current = new FileInfo(path); current is not null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
                {
                    if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("Fresh-entry installed source pins cannot pass through links.");
                    }
                }
                using FileStream stream = File.OpenRead(path);
                if (stream.Length is <= 0 or > 2147483648L)
                {
                    throw new InvalidDataException("Fresh-entry installed source file is outside its bounded size.");
                }
                pins.Add(new(name, Convert.ToHexStringLower(SHA256.HashData(stream))));
            }
            DiagnosticFreshEntryDeclaration declaration = new(DiagnosticFreshEntryDeclaration.SingleUse, pins.ToImmutable());
            declaration.Validate();
            sourcePins?.Add(root, declaration);
            Configure(declaration);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            blocker = "FRESH_ENTRY_SOURCE_PRECONDITION: " + exception.Message;
            return false;
        }

        void Configure(DiagnosticFreshEntryDeclaration declaration)
        {
            driver["fresh_entry"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options);
            if (driver["terminal_hold"] is JsonObject hold && hold["candidate"] is JsonValue candidate && candidate.TryGetValue<string>(out string? selected) && selected == nameof(BattleOutcome.Victory))
            {
                using JsonDocument original = JsonDocument.Parse(driver.ToJsonString());
                _ = VictoryTerminalHoldValidation.ValidateDeclaration(original.RootElement);
            }
            else
            {
                driver.Remove("terminal_hold");
            }
            driver.Remove("repairs");
            JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(recipe);
            recipe.Clear();
            foreach ((string name, JsonNode? value) in canonical)
            {
                recipe.Add(name, value?.DeepClone());
            }
        }
    }

    public static void DeclareOrdinaryPlayerStatusRearm(JsonObject recipe, BattleNativeAction action, bool party, string actorId, int maximumHitPoints)
    {
        if (!party || action.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32 || (action.Statuses0 == 0 && action.Statuses1 == 0) || (action.Statuses0 & 5) != 0 || (action.Statuses1 & 0x80010000) != 0)
        {
            return;
        }
        BattleDiagnosticCombatRepair repair = new(actorId, maximumHitPoints, RemoveStatuses0: action.Statuses0, RemoveStatuses1: action.Statuses1);
        repair.Validate();
        recipe["driver"]!["repairs"] = new JsonArray(new JsonObject
        {
            ["actor_id"] = actorId,
            ["hit_points"] = maximumHitPoints,
            ["revive"] = false,
            ["remove_statuses0"] = action.Statuses0,
            ["remove_statuses1"] = action.Statuses1
        });
    }
}
