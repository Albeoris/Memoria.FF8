using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanFixtureMigrationTests
{
    private sealed record Change(string Pointer, JsonNode? Before, JsonNode? After, bool BeforePresent = true, bool AfterPresent = true);
    private sealed record Receipt(string SourceSha256, string OutputSha256, string Purpose, Change[] Changes, long Frame, long NativeCallback, long ContinuedNativeCallback);

    [Fact]
    public void Prepare_explicit_Scan_fixture_preserving_source_and_validate_one_callback()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Configure the selected standalone source and a distinct Scan fixture output.");
        Assert.False(string.Equals(Path.GetFullPath(input!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(output!));
        Assert.False(File.Exists(output + ".changes.json"));
        byte[] originalBytes = File.ReadAllBytes(input!);
        const string expectedHash = "599CB5A3BD5D22ED96EC9438853EDE1410F282F92A5B1C15F5FE0DD0782B846C";
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        JsonObject rawOriginal = JsonNode.Parse(originalBytes)!.AsObject();
        JsonObject currentSchema = rawOriginal.DeepClone().AsObject();
        MigrateSettledDamageFeedback(currentSchema);
        BattleSnapshot original = currentSchema.Deserialize<BattleSnapshot>()!;
        Assert.Equal(362L, original.Frame);
        Assert.Equal(442L, original.NativeGraphics!.Callback);
        Assert.Equal(94, original.Definition.EncounterId);
        Assert.Equal(31, original.Definition.StageId);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Null(original.NativeEffectMemory.Scan);

        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleRetainedMemory coldMemory = game.ReadColdProcessNativeBattleMemory();
        NativeBattleScanProcess cold = coldMemory.EffectMemory!.Scan!;
        Assert.NotNull(cold);
        Assert.Equal(2, cold.MutableMeshes.Count);
        Assert.False(cold.InputInitialized);
        Assert.False(cold.Text.Initialized);
        ImmutableArray<uint?> coldColors = coldMemory.ModelColors40 ?? throw new InvalidDataException("The fixture requires authentic loader model colors.");
        Assert.Equal(7, coldColors.Length);
        Assert.All(coldColors, color => Assert.NotNull(color));
        Assert.Null(original.Definition.NativeRetainedMemory!.ModelColors40);
        int[] inactiveIndices = [1, 2, 4, 5, 6];
        Assert.Equal(inactiveIndices, original.NativeGraphics.Models.Where(model => model.Color40 is null).Select(model => model.Index).ToArray());
        ImmutableArray<NativeBattleGraphicsModel>.Builder models = original.NativeGraphics.Models.ToBuilder();
        foreach (int index in inactiveIndices)
        {
            Assert.Equal(0, models[index].Flags0 & 2);
            Assert.Null(original.NativeGraphics.ActorIds[index]);
            models[index] = models[index] with { Color40 = coldColors[index] };
        }
        Assert.Equal(original.NativeGraphics.Models[0], models[0]);
        Assert.Equal(original.NativeGraphics.Models[3], models[3]);
        NativeInputConfiguration inputConfiguration = game.ReadNewGameInputConfiguration();
        Assert.Null(original.NativeScannedModels);
        Assert.Null(original.Definition.NativeScannedModels);
        NativeScannedModels scannedModels = game.ReadNewGameScannedModels();
        BattleActionDefinition scan = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 50);
        Assert.Equal((ushort)40, scan.NativeAction!.MagicId);
        Assert.Equal((byte)2, scan.NativeAction.Command);
        Assert.Equal((ushort)50, scan.NativeAction.ActionId);
        BattleDefinition definition = BattleSession.CloneDefinition(original.Definition);
        BattleCombatantDefinition enemy = Assert.Single(definition.Combatants, actor => !actor.IsPlayer);
        int[] enemyDefenses = assets.ReadEnemyNativeStats(enemy.ModelId).Project(enemy.Level).ElementDefenses.Select(value => (int)value).ToArray();
        Assert.Equal(8, enemyDefenses.Length);
        Assert.Contains(enemyDefenses, value => value != 800);
        Assert.All(enemy.ElementDefenses, value => Assert.Equal(800, value));
        Assert.All(original.Combatants.Single(actor => actor.Id == enemy.Id).NativeParticipant!.ElementDefenses!, value => Assert.Equal(800, value));
        Dictionary<ushort, NativeBattleEffectBinding> bindings = game.ReadNativeEffectCatalogForEffects([40, 185]).ToDictionary(binding => binding.Effect);
        string casterId = definition.Combatants.First(actor => actor.IsPlayer).Id;
        string casterName = definition.Combatants.Single(actor => actor.Id == casterId).Name;
        BattleCombatantSnapshot caster = original.Combatants.Single(actor => actor.Id == casterId);
        NativeMagicStock[] casterInventory = Inventory(caster.NativeMagicInventory);
        NativeBattleAtbState fullGauge = caster.NativeAtb! with { Current = caster.NativeAtb!.Maximum };
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(fullGauge, caster.NativeParticipant!.NativeCombatantFlags ?? throw new InvalidDataException("The fixture requires its actual saved participant flags."), caster.NativeStatus!.Saved, caster.NativeStatus.Battle);
        Assert.Equal(NativeBattleGaugeRegistrationKind.Menu, registration.Kind);
        int casterIndex = original.NativeGraphics.ActorIds.IndexOf(casterId);
        Assert.InRange(casterIndex, 0, 2);
        NativeBattleUiServices ui = original.NativeGraphics.UiServices!;
        Assert.Equal(0u, ui.ReadyMask);
        Assert.Empty(ui.Commands.Queue);
        var ready = ui.Commands.Enqueue(checked((byte)casterIndex), 0x11).Dispatch(ui.ReadyMask);
        Assert.True(ready.SelectMenu);
        NativeBattleUiServices readyUi = ui.SetFlags(0, 0x10) with { Commands = ready.Commands, ReadyMask = ready.ReadyMask, Menu957 = 4 };
        byte[] encodedName = EncodeName24(casterName);
        definition = definition with
        {
            NativeEffectCatalog = definition.NativeEffectCatalog.Select(binding => bindings.GetValueOrDefault(binding.Effect) ?? binding).ToImmutableArray(),
            NativeRetainedMemory = definition.NativeRetainedMemory! with { ModelColors40 = coldColors, EffectMemory = definition.NativeRetainedMemory!.EffectMemory! with { Scan = cold } },
            NativeInputButtonMap = inputConfiguration.ButtonMap,
            NativeSavedOptions = original.Definition.NativeSavedOptions ?? inputConfiguration.Options,
            NativeScanAlwaysShow = false,
            NativeScannedModels = scannedModels,
            NativeScanNames = new() { [0x30] = EncodeTerminated(casterName), [0x34] = EncodeTerminated(casterName), [0x50] = EncodeTerminated("Griever") },
            NativeScanActorNames = new() { [casterId] = encodedName },
            ActionDefinitions = definition.ActionDefinitions.Where(action => action.Ability != BattleAbility.Magic || action.Id != 50).Append(scan).ToArray(),
            NativeActions = definition.NativeActions.Where(action => action.Command != 2 || action.ActionId != 50).Append(scan.NativeAction).ToArray(),
            Combatants = definition.Combatants.Select(actor => actor.Id == casterId ? actor with { Abilities = actor.Abilities.Append(BattleAbility.Magic).Distinct().ToArray(), NativeMagicInventory = casterInventory.ToArray() } : actor.Id == enemy.Id ? actor with { ElementDefenses = enemyDefenses.ToArray() } : actor).ToArray()
        };
        BattleSnapshot migrated = original with
        {
            Definition = definition,
            NativeScannedModels = scannedModels,
            NativeEffectMemory = original.NativeEffectMemory with { Scan = cold },
            NativeGraphics = original.NativeGraphics with { Models = models.ToImmutable(), UiServices = readyUi },
            Combatants = original.Combatants.Select(actor => actor.Id == casterId ? actor with { Atb = fullGauge.Display, NativeAtb = fullGauge, NativeParticipant = actor.NativeParticipant! with { NativeCombatantFlags = registration.ActorFlags }, NativeMagicInventory = casterInventory.ToArray() } : actor.Id == enemy.Id ? actor with { NativeParticipant = actor.NativeParticipant! with { ElementDefenses = enemyDefenses.ToArray() } } : actor).ToArray()
        };
        List<Change> changes = [];
        Diff(rawOriginal, JsonSerializer.SerializeToNode(migrated), "", changes);
        Assert.NotEmpty(changes);
        Assert.All(changes, change => Assert.True(change.Pointer.StartsWith("/Definition/", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativeScannedModels", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativeEffectMemory/Scan", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativeGraphics/UiServices/", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativeGraphics/Effects/", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativeGraphics/Models/", StringComparison.Ordinal) && change.Pointer.EndsWith("/Color40", StringComparison.Ordinal) || change.Pointer.StartsWith("/NativePartyPresentation/", StringComparison.Ordinal) || change.Pointer.StartsWith("/Combatants/0/", StringComparison.Ordinal) || change.Pointer.StartsWith("/Combatants/1/NativeParticipant/ElementDefenses/", StringComparison.Ordinal), change.Pointer));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        BattleSnapshot disk = NativeBattleEffectFamilyRunner.LoadSnapshot(output!);
        BattleSession first = BattleSession.Restore(disk);
        BattleSession second = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(output!));
        Assert.Equal(JsonSerializer.Serialize(disk), JsonSerializer.Serialize(first.Capture()));
        BattleScenePresentation scene = new(assets, disk.Definition, true);
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(443L, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input!))));
        Receipt receipt = new(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!))), "Controlled Scan fixture: explicit settled HUD/text schema, original new-game scanned-model history and retained loader color bank; only unknown inactive model1/2/4/5/6 Color40 receive authentic cold values, loaded0/3 retained. Enemy eight element defenses come from installed native stats at its existing level; HP and all other stats retained. Installed resources/cold Scan process; first party member receives Scan stock2/full native gauge using existing registration and ready dispatch. Replacement names30/34 use that party name and50 explicit Griever. No gameplay replay.", changes.ToArray(), disk.Frame, disk.NativeGraphics!.Callback, first.NativeGraphics.Callback);
        using (FileStream stream = new(output + ".changes.json", FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, receipt, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static NativeMagicStock[] Inventory(NativeMagicStock[]? original)
    {
        Assert.NotNull(original);
        Assert.Equal(32, original!.Length);
        Assert.All(original, slot => Assert.Equal((byte)0, slot.SpellId));
        NativeMagicStock[] result = original.ToArray();
        result[0] = new(50, 2);
        return result;
    }

    private static void MigrateSettledDamageFeedback(JsonObject snapshot)
    {
        Assert.Null(snapshot["ActiveAction"]);
        Assert.Empty(snapshot["GuardianForceCasts"]!.AsArray());
        JsonObject effects = snapshot["NativeGraphics"]!["Effects"]!.AsObject();
        Assert.False(effects.ContainsKey("TextDraws"));
        foreach (JsonNode? slotNode in effects["Slots"]!.AsArray())
        {
            JsonObject slot = slotNode!.AsObject();
            Assert.Equal(0, slot["Flags0"]!.GetValue<int>() & 1);
            Assert.False(slot.ContainsKey("TextAnchor"));
            slot["TextAnchor"] = null;
        }
        effects["TextDraws"] = new JsonArray();
        foreach (JsonNode? rowNode in snapshot["NativePartyPresentation"]!["Rows"]!.AsArray())
        {
            if (rowNode is not JsonObject row)
            {
                continue;
            }
            Assert.False(row.ContainsKey("DisplayedHitPointsQ12"));
            Assert.False(row.ContainsKey("DisplayedGuardianForceHitPointsQ12"));
            row["DisplayedHitPointsQ12"] = checked(row["HitPoints"]!.GetValue<int>() << 12);
            row["DisplayedGuardianForceHitPointsQ12"] = row["GuardianForceHitPoints"] is JsonNode hp ? JsonValue.Create(checked(hp.GetValue<int>() << 12)) : null;
        }
    }

    private static byte[] EncodeTerminated(string text) => FF8Encoding.CreateEuropean().GetBytes(text).Append((byte)0).ToArray();

    private static byte[] EncodeName24(string text)
    {
        byte[] encoded = EncodeTerminated(text);
        Assert.True(encoded.Length <= 24);
        byte[] result = new byte[24];
        encoded.CopyTo(result, 0);
        return result;
    }

    private static void Diff(JsonNode? before, JsonNode? after, string pointer, List<Change> changes)
    {
        if (JsonNode.DeepEquals(before, after))
        {
            return;
        }
        if (before is JsonObject left && after is JsonObject right)
        {
            foreach (string key in left.Select(pair => pair.Key).Union(right.Select(pair => pair.Key)).Order())
            {
                string child = pointer + "/" + key.Replace("~", "~0").Replace("/", "~1");
                if (left.ContainsKey(key) != right.ContainsKey(key))
                {
                    changes.Add(new(child, left[key]?.DeepClone(), right[key]?.DeepClone(), left.ContainsKey(key), right.ContainsKey(key)));
                }
                else
                {
                    Diff(left[key], right[key], child, changes);
                }
            }
            return;
        }
        if (before is JsonArray leftArray && after is JsonArray rightArray && leftArray.Count == rightArray.Count)
        {
            for (int index = 0; index < leftArray.Count; index++)
            {
                Diff(leftArray[index], rightArray[index], pointer + "/" + index, changes);
            }
            return;
        }
        changes.Add(new(pointer, before?.DeepClone(), after?.DeepClone()));
    }
}
