using System.Text.Json;
using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianPresentationTests
{
    [Fact]
    public void Installed_Quezacotl_fresh_standalone_runs_through_phase318_with_original_stream_receipts_and_restore()
    {
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, InitialAtb = BattleSession.AtbMaximum / 3 }
        ];
        //Declared fresh installed-resource component, not a reconstructed gameplay save. Its explicit playtime-derived UI seed is0.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = BattlePartyProjection.CreateActions(game.ReadNewGameCharacterVitals(), assets.ActionDefinitions),
            NativeActions = assets.NativeActions.ToArray(),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeBattleUiRandom = new(0),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = game.ReadNewGameInputConfiguration().NativeBattleSpeedByte,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([116]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleActionDefinition guardian = Assert.Single(definition.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0);
        Assert.Equal((ushort)116, guardian.NativeAction!.MagicId);
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot before = battle.Capture();
        Assert.NotNull(before.NativeClock);
        Assert.Contains(before.Combatants, actor => actor.NativeAtb is { Current: > 0 } gauge && gauge.Current < gauge.Maximum);
        NativeBattleLogicalResolvedCommand[] entryCommands = before.NativeGraphics!.PrimitiveFrame!.LastCommands.ToArray();
        NativeBattleLogicalResolvedCommand[] shadowModes = entryCommands.Where(command => command.Command.Opcode == 0xE1 && command.Command.Words[0] is 0xE1000620 or 0xE1000640).ToArray();
        foreach (NativeBattleLogicalResolvedCommand command in shadowModes)
        {
            Assert.Equal((ushort)0, command.Command.Flags20);
            Assert.Equal(new[] { 0, 0, 0, 0 }, command.Command.VertexPayload4.ToArray());
        }
        if (entryCommands.Any(command => command.Command.OrderingBucket == 4121 && command.Command.Opcode == 0x32))
        {
            Assert.NotEmpty(shadowModes);
        }
        NativeBattleGraphicsModel[] visibleModels = before.NativeGraphics.Models.Where(model => (model.Flags0 & 6) == 2 && (model.Flags2 & 0x84) == 0).ToArray();
        Assert.All(visibleModels, model => Assert.NotNull(model.GeometryBounds52));
        Console.WriteLine($"Native entry: visiblemodels{visibleModels.Length}, shadowdrawmodes{shadowModes.Length}; zero modes is permitted when the actual camera culls every shadow.");
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        Assert.Equal((byte)0, packets.ActionCount);
        Assert.Equal((byte)0, packets.ContactCount);
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, before.Definition.NativeRetainedMemory!.ColdExecutableSha256);
        byte coldRemainingHits = NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game);
        BattleCombatantSnapshot[] coldActors = before.Combatants.Select(actor => actor.NativeParticipant!.ActorIndex == 3 ? actor with { NativeParticipant = actor.NativeParticipant! with { RemainingHits = coldRemainingHits } } : actor).ToArray();
        Assert.Equal((byte?)0, Assert.Single(coldActors, actor => actor.NativeParticipant!.ActorIndex == 3).NativeParticipant!.RemainingHits);
        //Native FE/action64 selects sequence14 through495070;116 is the original kernel GF effect ID.
        (packets, int header) = packets.Prepare(new(0, 0xFE, 14, -1, 64, 116, null));
        (packets, _) = packets.AppendContact(new(new(3, 5, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsState graphics = before.NativeGraphics!;
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        //The declared cold/manual-header component never invokes48E830/48EB40. This explicit input preserves native actor3's authenticated BSS byte; old snapshots receive no default.
        BattleSnapshot pendingSource = before with { Combatants = coldActors, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(new uint[] { 0x84, 0x85, 0x86 }, pendingSource.Definition.NativeMusicProcess!.Entry!.Sounds.Select(sound => sound.Sound));
        Assert.NotNull(pendingSource.NativeGlobalSoundProcess!.Rotation);
        Assert.All(pendingSource.NativeGlobalSoundProcess.OriginalEffectIds, value => Assert.NotNull(value));
        string sourceOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_FRESH_SOURCE_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "memoria-ff8-gf-fresh-" + Guid.NewGuid().ToString("N") + ".battle.json");
        PublishStandalone(sourceOutput, pendingSource);
        BattleSnapshot sourceFromFile = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(sourceOutput))!;
        Assert.Equal(JsonSerializer.Serialize(pendingSource), JsonSerializer.Serialize(sourceFromFile));
        battle = BattleSession.Restore(sourceFromFile);
        Console.WriteLine($"Fresh GF pending104 source: {Path.GetFullPath(sourceOutput)}; frame{sourceFromFile.Frame}/callback{sourceFromFile.NativeGraphics!.Callback}.");
        NativeBattleAtbState[] animationGauges = sourceFromFile.Combatants.Select(actor => actor.NativeAtb!).ToArray();
        uint initialUiCounter = sourceFromFile.NativeGraphics!.UiServices!.Counter;
        long initialGraphicsCallback = sourceFromFile.NativeGraphics.Callback;
        TimeSpan frame = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        bool selected = false;
        bool restoredBeforeEntry = false;
        bool sawEntry = false;
        bool sawGeometry = false;
        bool sawNativeFlash = false;
        BattleSnapshot? prefixSnapshot = null;
        BattleSnapshot? beforeBoost = null;
        Dictionary<ushort, NativeBattleModuleSound> streamPlays = [];
        HashSet<ushort> restoredStreamPhases = [];
        BattleSession? streamClone = null;
        ushort? clonedStreamPhase = null;
        int streamCountBeforeClone = 0;
        for (int callback = 0; callback < 512; callback++)
        {
            if (!restoredBeforeEntry && battle.NativeGraphics!.Descriptors.Tasks.Any(task => task.Kind == NativeBattleDescriptorTaskKind.Action104 && task.Phase == 2))
            {
                BattleSnapshot pending = battle.Capture();
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
                battle.AdvanceNativeGraphicsPresentation(frame);
                restored.AdvanceNativeGraphicsPresentation(frame);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                AssertFrozenGauges(battle.Capture());
                restoredBeforeEntry = true;
            }
            BattleSnapshot beforeStep = battle.Capture();
            if (ChildPhase(beforeStep) == 319)
            {
                prefixSnapshot = beforeStep;
                Assert.NotNull(streamClone);
                Assert.Equal((ushort)318, clonedStreamPhase);
                BattleSession firstProbe = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(beforeStep))!);
                BattleSession secondProbe = streamClone;
                Exception? firstError = Record.Exception(() => firstProbe.AdvanceNativeGraphicsPresentation(frame));
                Exception? secondError = Record.Exception(() => secondProbe.AdvanceNativeGraphicsPresentation(frame));
                if (firstError is null)
                {
                    Assert.Null(secondError);
                }
                else
                {
                    NotSupportedException firstBoundary = Assert.IsType<NotSupportedException>(firstError);
                    NotSupportedException secondBoundary = Assert.IsType<NotSupportedException>(secondError);
                    Assert.Equal(firstBoundary.Message, secondBoundary.Message);
                }
                Assert.Equal(JsonSerializer.Serialize(firstProbe.Capture()), JsonSerializer.Serialize(secondProbe.Capture()));
                AssertFrozenGauges(firstProbe.Capture());
                Assert.Equal(streamCountBeforeClone, StreamSounds(firstProbe.Capture()).Length);
                restoredStreamPhases.Add(318);
                streamClone = null;
                break;
            }
            if (beforeStep.NativeGuardianBoost is { Phase: 4, Remaining: 1 })
            {
                beforeBoost = beforeStep;
            }
            battle.AdvanceNativeGraphicsPresentation(frame);
            BattleSnapshot afterStep = battle.Capture();
            AssertFrozenGauges(afterStep);
            if (streamClone is not null)
            {
                streamClone.AdvanceNativeGraphicsPresentation(frame);
                Assert.Equal(JsonSerializer.Serialize(afterStep), JsonSerializer.Serialize(streamClone.Capture()));
                Assert.Equal(streamCountBeforeClone, StreamSounds(afterStep).Length);
                restoredStreamPhases.Add(clonedStreamPhase!.Value);
                streamClone = null;
            }
            ushort? phaseBefore = ChildPhase(beforeStep);
            if (phaseBefore is 69 or 212 or 318 && ChildPhase(afterStep) != phaseBefore)
            {
                NativeBattleModuleSound sound = Assert.Single(afterStep.Events.Skip(beforeStep.Events.Length).Select(entry => entry.NativeModuleSound).OfType<NativeBattleModuleSound>(), sound => sound is { Flags: 0, RawPan: 128, Volume: 127 });
                NativeBattleSummonStreamState stream = afterStep.NativeEffectMemory!.SummonStream!;
                Assert.NotNull(stream.Number);
                Assert.NotNull(stream.SourceAddress);
                Assert.Equal(unchecked((uint)stream.Number.Value), sound.Sound);
                NativeGlobalSoundAllocation allocation = Assert.IsType<NativeGlobalSoundAllocation>(sound.Allocation);
                NativeGlobalSoundProcess.ValidateAllocation(sound.Flags, allocation);
                Assert.Equal(beforeStep.NativeGlobalSoundProcess!.Rotation, allocation.BeforeRotation);
                Assert.Equal(afterStep.NativeGlobalSoundProcess!.Rotation, allocation.AfterRotation);
                Assert.Equal(sound.Sound, afterStep.NativeGlobalSoundProcess.OriginalEffectIds[allocation.Voice]);
                Assert.True(streamPlays.TryAdd(phaseBefore.Value, sound));
                streamCountBeforeClone = StreamSounds(afterStep).Length;
                clonedStreamPhase = phaseBefore.Value;
                streamClone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(afterStep))!);
                Assert.Equal(JsonSerializer.Serialize(afterStep), JsonSerializer.Serialize(streamClone.Capture()));
            }
            selected |= battle.NativeGraphics!.ActionGraphics?.Callback == 0x50B2A0;
            sawEntry |= battle.NativeGraphics.OriginalEffect?.ReturnedDescriptor == 0x2521748;
            sawGeometry |= battle.NativeGraphics.PrimitiveFrame?.LastCommands.Any(command => command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8") == true;
            sawNativeFlash |= battle.NativeGraphics.PrimitiveFrame?.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x2A && command.Command.OrderingBucket == 8 && command.Command.Flags20 == 1) == true;
        }
        NativeBattleGraphicsModel finalCaster = battle.NativeGraphics!.Models[0];
        string diagnostic = $"callback={battle.NativeGraphics.Callback}; actionPhase={battle.NativeGraphics.Descriptors.Tasks.FirstOrDefault(task => task.Kind == NativeBattleDescriptorTaskKind.Action104)?.Phase}; casterFlags0={finalCaster.Flags0:X}; sequence={finalCaster.Sequence}; cursor={finalCaster.SequenceState?.Offset46}; resource44={finalCaster.ResourceFlags44:X}; IO={battle.NativeEffectIoResult}; cameraMask={battle.NativeCamera?.Mask}; descriptor={battle.NativeGraphics.OriginalEffect?.ReturnedDescriptor:X}; verifiedPrefix={prefixSnapshot is not null}";
        if (prefixSnapshot is not null && Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_FRESH_BOUNDARY_OUTPUT") is { Length: > 0 } boundaryOutput)
        {
            PublishStandalone(boundaryOutput, prefixSnapshot);
            Assert.Equal(JsonSerializer.Serialize(prefixSnapshot), JsonSerializer.Serialize(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(boundaryOutput))!));
            Console.WriteLine($"Fresh GF bounded prefix snapshot: {Path.GetFullPath(boundaryOutput)}; frame{prefixSnapshot.Frame}/callback{prefixSnapshot.NativeGraphics!.Callback}; phase{ChildPhase(prefixSnapshot)}.");
        }
        Assert.True(selected);
        Assert.True(restoredBeforeEntry);
        Assert.True(prefixSnapshot is not null, diagnostic);
        Assert.True(sawEntry, diagnostic);
        Assert.True(sawGeometry);
        Assert.True(sawNativeFlash);
        Assert.True(battle.NativeGraphics!.UiServices!.Counter > initialUiCounter);
        Assert.True(battle.NativeGraphics.Callback > initialGraphicsCallback);
        Assert.Equal((ushort)116, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0x2521748u, battle.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Contains(battle.NativeGraphics.OriginalEffect.Pools, pool => pool.DescriptorAddress == 0x2521738 && pool.Stride == 0x8D0 && pool.Slots.Single().Callback == 0x6C3940);
        Assert.Equal((byte?)0, battle.NativeEffectMemory!.GuardianCasterBank);
        ushort prefixPhase = battle.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12);
        Assert.Equal((ushort)319, prefixPhase);
        //6C3A42..4D selects6574D0(rig=child+28h,2) at238;509440 stores thatBYTE at rig+6Ch=child+94h.
        Assert.Equal((byte)2, battle.NativeEffectMemory!.Arena!.Read(0x94, 1)[0]);
        Assert.Equal(0, battle.NativeGraphics.Models[3].Flags0 & 4);
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Sound: 240066, Flags: 1, Pan: 128 });
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Sound: 240069, Flags: 1, Pan: 128 });
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Sound: 240072, Flags: 1, Pan: 128 });
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Sound: 240039, Flags: 1, Pan: 128 });
        Assert.Equal(0, battle.NativeEffectMemory.QuezacotlTargetY1774);
        Assert.Equal(26, NativeBattleQuezacotlWorkspace.ReadExecutable(battle.NativeEffectMemory).Frame);
        NativeBattleQuezacotlResources streamResources = battle.NativeGraphics.OriginalEffect.Binding.NativeQuezacotlResources!;
        uint playedStream = StreamNumber(510);
        uint loadedStream = StreamNumber(524);
        Assert.Equal(690118u, playedStream);
        Assert.Equal(690120u, loadedStream);
        Assert.Equal(loadedStream, unchecked((uint)battle.NativeEffectMemory.SummonStream!.Number!.Value));
        Assert.Equal(0x2108954u, battle.NativeEffectMemory.SummonStream.SourceAddress);
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Flags: 0, RawPan: 128, Volume: 127 } sound && sound.Sound == playedStream);
        Assert.Contains(battle.Events, entry => entry.NativeModuleSound is { Flags: 0, RawPan: 128, Volume: 127 } sound && sound.Sound == loadedStream);
        Assert.Equal(new ushort[] { 69, 212, 318 }, streamPlays.Keys.Order().ToArray());
        Assert.Equal(new ushort[] { 69, 212, 318 }, restoredStreamPhases.Order().ToArray());
        Assert.Equal(playedStream, streamPlays[69].Sound);
        Assert.Equal(StreamNumber(520), streamPlays[212].Sound);
        Assert.Equal(loadedStream, streamPlays[318].Sound);
        BattleEvent damage = Assert.Single(battle.Events, entry => entry.Kind == "Damage");
        Assert.Equal("party", damage.ActorId);
        Assert.Equal("enemy", damage.TargetId);
        Assert.Equal(BattleAbility.GuardianForce, damage.Ability);
        Assert.Equal(actors[1].CurrentHp, damage.Amount);
        Assert.True(battle.Capture().NativeActionScratch!.GuardianFormulaE1E);

        void AssertFrozenGauges(BattleSnapshot snapshot)
        {
            Assert.Equal(animationGauges, snapshot.Combatants.Select(actor => actor.NativeAtb!).ToArray());
        }

        uint StreamNumber(short fileId) => unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(streamResources.Files.Single(file => file.FileId == fileId).Data.AsSpan()) + 690000));
        Assert.NotNull(prefixSnapshot);
        Assert.Null(prefixSnapshot.ActiveAction);
        Assert.Null(prefixSnapshot.NativeGuardianBoost);
        Assert.NotNull(beforeBoost);
        NativeBattleGuardianBoost boost = beforeBoost.NativeGuardianBoost!;
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(beforeBoost with { NativeGuardianBoost = boost with { ActionId = 65 } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(beforeBoost with { NativeGuardianBoost = boost with { ComponentSubframeTicks = 1 } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(beforeBoost with { NativeActionScratch = beforeBoost.NativeActionScratch! with { GuardianFormulaE1E = true } }));
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GF_EVIDENCE");
        if (!string.IsNullOrEmpty(evidence))
        {
            Directory.CreateDirectory(evidence);
            string name = "quezacotl116-prefix-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
            using FileStream output = new(Path.Combine(evidence, name + ".battle.json"), FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(output, prefixSnapshot);
        }
    }

    private static ushort? ChildPhase(BattleSnapshot snapshot)
    {
        return snapshot.NativeGraphics?.OriginalEffect?.Pools.FirstOrDefault(pool => pool.DescriptorAddress == 0x2521738)?.Slots.Single().ReadWord(12);
    }

    private static NativeBattleModuleSound[] StreamSounds(BattleSnapshot snapshot)
    {
        return snapshot.Events.Select(entry => entry.NativeModuleSound).OfType<NativeBattleModuleSound>().Where(sound => sound is { Flags: 0, RawPan: 128, Volume: 127 }).ToArray();
    }

    private static void PublishStandalone(string path, BattleSnapshot snapshot)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using FileStream output = new(fullPath, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(output, snapshot);
    }
}
