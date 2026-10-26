using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianOriginalSourcePreparationTests
{
    [Theory]
    [InlineData(218, 2, 68, 330, 53)]
    [InlineData(326, 1, 33, 329, 52)]
    public void Original_self_selector_queues_the_selected_component_through_native_admission(int effect, int slot, int pc, int actionId, int sequence)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? baselinePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_SOURCE_BASELINE");
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_SOURCE_OUTPUT");
        Assert.SkipUnless(directory is not null && baselinePath is not null && outputRoot is not null, "Select installed resources, the original formation795 baseline and a distinct source output root.");
        string output = Path.Combine(outputRoot!, $"effect{effect}-actual-source001");
        Assert.False(Directory.Exists(output), "Preserve existing standalone sources by selecting a distinct output root.");
        Directory.CreateDirectory(output);
        using GameAssets game = new(directory!);
        BattleSnapshot baseline = NativeBattleEffectFamilyRunner.LoadSnapshot(baselinePath!);
        Assert.Equal(795, baseline.Definition.EncounterId);
        Assert.Equal(68, baseline.Definition.StageId);
        GameSession context = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            CharacterVitals = game.ReadNewGameCharacterVitals(),
            CharacterAppearance = game.ReadNewGameCharacterAppearance(),
            InputConfiguration = game.ReadNewGameInputConfiguration(),
            Economy = game.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            LimitProgress = game.ReadNewGameLimitProgress(),
            SavedBattleFlags = game.ReadNewGameSavedBattleFlags(),
            PocketStation = game.ReadNewGamePocketStation(),
            ScannedModels = game.ReadNewGameScannedModels(),
            ShotAvailability = game.ReadNewGameShotAvailability(),
            GlobalRandom = game.ReadNewGameGlobalRandom(),
            KnownMagic = game.ReadNewGameKnownMagic(),
            NativeBattleMemory = game.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(context, directory!, sharedAssets: game);
        BattleAssets assets = world.BattleResources;
        BattleEncounter formation = assets.LoadEncounter(795);
        NativeBattleMusicProcessState music = baseline.Definition.NativeMusicProcess ?? throw new InvalidDataException("The selected original baseline lacks its transferred battle music owner.");
        music.Validate();
        Assert.NotNull(music.ReturnBank);
        NativeBattleMusicEntryReceipt musicEntry = Assert.IsType<NativeBattleMusicEntryReceipt>(music.Entry);
        Assert.Equal(795, musicEntry.Encounter);
        Assert.Equal(baseline.Definition.NativeFlags, musicEntry.Flags);
        BattleDefinition definition = world.CreateEncounterBattle(795, baseline.Definition.NativeSeed, baseline.Definition.NativeFlags) with
        {
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([checked((ushort)effect)]),
            NativeMusicProcess = music,
            NativeGlobalSoundProcess = baseline.Definition.NativeGlobalSoundProcess ?? throw new InvalidDataException("The selected original baseline lacks its paired transferred sound owner.")
        };
        Assert.NotNull(definition.InitialRandom);
        BattleCombatantDefinition casterDefinition = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == 0);
        Assert.Equal(128, casterDefinition.ModelId);
        BattleCombatantDefinition companion = Assert.Single(definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == 1);
        Assert.Equal(82, companion.ModelId);
        Assert.True(companion.InitialSuspended);
        foreach (BattleCombatantDefinition enemy in definition.Combatants.Where(actor => !actor.IsPlayer))
        {
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(enemy.ModelId).Project(enemy.Level);
            Assert.Equal(stats.MaximumHp, enemy.MaximumHp);
            Assert.Equal(stats.MaximumHp, enemy.CurrentHp);
            Assert.Equal(stats.Strength, enemy.Attack);
            Assert.Equal(stats.Vitality, enemy.Defense);
            Assert.Equal(stats.Magic, enemy.Magic);
            Assert.Equal(stats.Spirit, enemy.Spirit);
            Assert.Equal(stats.ElementDefenses.Select(value => (int)value).ToArray(), enemy.ElementDefenses);
        }
        byte[] code = casterDefinition.NativeAi!.Scripts[1];
        Assert.Equal(new byte[] { 4, 200, 12, checked((byte)slot) }, code.AsSpan(pc - 2, 4).ToArray());
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        session.ConfigureDiagnosticScheduling(true, readinessActorId: casterDefinition.Id);
        Save("before-entry", session.Capture());
        BattleDiagnosticEnemyAbility? selection = null;
        Pump(() =>
        {
            if (!session.NativeEntryReady)
            {
                return false;
            }
            BattleCombatantDefinition current = session.GetCombatantDefinition(casterDefinition.Id);
            NativeBattleAiDefinition ai = current.NativeAi ?? throw new InvalidDataException("The original caster lost its initialized AI bank.");
            selection = new(128, 0, ai.LevelBand, slot, 236, checked((ushort)actionId), checked((byte)sequence), 200);
            return session.CanSubmitDiagnosticEnemyAbility(casterDefinition.Id, selection);
        }, "original-initialization-and-readiness");
        BattleSnapshot ready = session.Capture();
        Assert.True(ready.NativeGraphics!.Models.Length >= 7);
        Assert.Equal((byte)144, ready.NativeGraphics.Models[3].ModelId4);
        Assert.Equal((byte)98, ready.NativeGraphics.Models[4].ModelId4);
        Save("after-entry", ready);
        session.SubmitDiagnosticEnemyAbility(casterDefinition.Id, selection!, checked((ushort)effect));
        Pump(Queued, "normal-native-action104");
        BattleSnapshot queued = session.Capture();
        NativeBattleOriginalEffectState? admitted = queued.NativeGraphics!.OriginalEffect;
        int[] descriptors = queued.NativeGraphics.Descriptors.Queue.Where(index => queued.NativeGraphics.Descriptors.Packets[index].Command == 104).ToArray();
        int headerIndex;
        if (admitted is not null)
        {
            Assert.Equal(effect, admitted.RequestedEffect);
            Assert.Null(admitted.ReturnedDescriptor);
            Assert.Empty(admitted.Pools);
            NativeBattleEffectParameters parameters = Assert.IsType<NativeBattleEffectParameters>(admitted.Parameters);
            Assert.Equal((byte)3, parameters.Actor78);
            Assert.Equal((ushort)8, parameters.TargetMask7A);
            headerIndex = parameters.HeaderIndex7C;
        }
        else
        {
            int descriptor = Assert.Single(descriptors);
            uint argument = queued.NativeGraphics.Descriptors.Packets[descriptor].Argument;
            headerIndex = checked((int)((argument - 0x1D280C4u) / 20));
        }
        foreach (int descriptor in descriptors)
        {
            Assert.Equal(checked(0x1D280C4u + (uint)headerIndex * 20), queued.NativeGraphics.Descriptors.Packets[descriptor].Argument);
        }
        NativeBattleActionHeaderSlot header = queued.NativeActionPackets!.Headers[headerIndex];
        Assert.Equal(effect, NativeBattleActionGraphics.Word(header, 6));
        NativeBattleContactPacket contact = queued.NativeActionPackets.ReadContact(header.FirstContactIndex ?? throw new InvalidDataException("The normal producer did not retain its actual contact."));
        Assert.Equal((byte)3, contact.Primary.ActorIndex);
        Save("queued-action104", queued);
        File.WriteAllText(Path.Combine(output, "original-formation.json"), JsonSerializer.Serialize(formation));
        File.WriteAllText(Path.Combine(output, "source-result.json"), JsonSerializer.Serialize(new { Effect = effect, Status = "admitted-before-entry", Boundary = admitted is null ? "queued-action104" : "admitted-before-entry", Scenario = "Diagnostic original ability-row invocation through normal native admission and formula; authored selector200 self; complete original formation and mandatory initialized AI; original encounter factory party/stat/RNG initialization; New Game initialization configuration; not a natural fifteenth turn or campaign continuation", Callback = 1, Offset = pc, AbilitySlot = slot, Command = 236, Action = actionId, Sequence = sequence, TargetSelector = 200, ActualContact = contact.Primary.ActorIndex, OriginalModel = 128, NativeModel = 144, queued.Frame, NativeCallback = queued.NativeGraphics.Callback }));

        bool Queued()
        {
            BattleSnapshot snapshot = session.Capture();
            if (snapshot.NativeGraphics!.OriginalEffect is { } admitted)
            {
                Assert.Equal(effect, admitted.RequestedEffect);
                Assert.Null(admitted.ReturnedDescriptor);
                Assert.Empty(admitted.Pools);
                return admitted.Parameters is not null;
            }
            return snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104);
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            File.WriteAllText(Path.Combine(output, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
        }

        void Pump(Func<bool> complete, string boundary)
        {
            for (int callback = 0; callback < 2400; callback++)
            {
                if (complete())
                {
                    return;
                }
                try
                {
                    session.Tick();
                }
                catch
                {
                    Save(boundary + "-failure", session.Capture());
                    throw;
                }
            }
            Save(boundary + "-budget", session.Capture());
            throw new InvalidDataException("The original component preparation exceeded its bounded callback budget at " + boundary + ".");
        }
    }
}
