using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleNorgQuakeTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_encounter63_Quake_preserves_target_sequences_and_cold_next()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original encounter63 and Quake.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        using GameAssets game = new(directory!);
        BattleInput input = new(BattleInputKind.Action, ActorId: "party-4", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 17);
        DiagnosticBattlePreparation preparation = new([new(0, true)], [new(4, 4, [new(17, 5)], [0]) { MagicCommand = new(0, 20) }, new(0, 0, [], []), new(3, 3, [], [])], false) { CombatParty = [4, 0, 3] };
        DiagnosticBattleRequest request = new(63, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Fixture = preparation, InstantGuardianForceCharge = false, PrepareEntryBeforePresentation = true };
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        BattleSession? cold = null;
        BattleAudioPresentation coldAudio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> directDispatches = [];
        List<BattleDiagnosticEffectDispatch> coldDispatches = [];
        battle.DiagnosticEffectDispatched += directDispatches.Add;
        BattleDefinition definition = battle.Capture().Definition;
        string boundary = "entry";
        using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        try
        {
            output.WriteLine(JsonSerializer.Serialize(new { Boundary = "initial-model-inventory", Models = Inventory(battle, definition) }));
            world.PrepareDiagnosticEntryBeforePresentation(request);
            Assert.True(battle.NativeEntryReady);
            Assert.Empty(battle.ObserveDiagnosticDrainBlockers());
            AssertMissingResponses("enemy-0", 84, 13, 3);
            AssertMissingResponses("enemy-1", 97, 16, 1);
            AssertMissingResponses("enemy-2", 96, 16, 1);
            for (int cast = 1; cast <= 2; cast++)
            {
                int turnAdvances = 0;
                for (int ready = 0; ready < 1024 && !battle.CanChoosePlayerCommand("party-4"); ready++)
                {
                    if (battle.ObserveDiagnosticDrainBlockers().Count == 0 && turnAdvances < 16)
                    {
                        battle.AdvanceTurn(1024);
                        turnAdvances++;
                    }
                    battle.Tick();
                    audio.Update(battle);
                }
                Assert.True(battle.CanChoosePlayerCommand("party-4"));
                Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy-0").CurrentHp > 0);
                boundary = "ordinary-magic-input-" + cast;
                world.ApplyBattleInput(input);
                BattleSnapshot submitted = battle.Capture();
                string frozen = JsonSerializer.Serialize(submitted);
                cold = Restore(submitted);
                bool restoredBounce = false;
                bool compareNext = true;
                bool completed = false;
                for (int callback = 0; callback < 1024; callback++)
                {
                    boundary = "cast-" + cast + "-native-callback-" + callback;
                    battle.Tick();
                    audio.Update(battle);
                    cold.Tick();
                    coldAudio.Update(cold);
                    if (compareNext)
                    {
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                        compareNext = false;
                    }
                    if (!restoredBounce && battle.NativeGraphics!.OriginalEffect is { Binding.Effect: 38, ReturnedDescriptor: > 0 } && battle.NativeEffectMemory!.Quake38!.SavedActors.Skip(3).Any(actor => actor.BouncePhase == 1))
                    {
                        BattleSnapshot bounce = battle.Capture();
                        Assert.Equal(JsonSerializer.Serialize(bounce), JsonSerializer.Serialize(cold.Capture()));
                        cold = Restore(bounce);
                        restoredBounce = true;
                        compareNext = true;
                        output.WriteLine(JsonSerializer.Serialize(new { Boundary = "bounce-cold-next", Cast = cast, Callback = battle.NativeGraphics.Callback, Models = Inventory(battle, definition) }));
                    }
                    if (directDispatches.Count(dispatch => dispatch.ExecutedEffect == 38) == cast && battle.ObserveDiagnosticDrainBlockers().Count == 0 && cold.ObserveDiagnosticDrainBlockers().Count == 0)
                    {
                        Assert.True(restoredBounce, "The original target bounce must execute before the mid-effect cold-next comparison.");
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                        Assert.Equal(frozen, JsonSerializer.Serialize(submitted));
                        completed = true;
                        break;
                    }
                }
                Assert.True(completed, "Original encounter63 Quake did not reach its completed native owner boundary within1024 callbacks.");
            }
            Assert.Equal(2, directDispatches.Count(dispatch => dispatch.ExecutedEffect == 38 && dispatch.ActorId == "party-4" && dispatch.ActionId == 17));
            Assert.Equal(2, coldDispatches.Count(dispatch => dispatch.ExecutedEffect == 38 && dispatch.ActorId == "party-4" && dispatch.ActionId == 17));
            output.WriteLine(JsonSerializer.Serialize(new { Boundary = "complete", Models = Inventory(battle, definition), Dispatches = directDispatches }));
        }
        catch (Exception error)
        {
            output.WriteLine(JsonSerializer.Serialize(new { Boundary = boundary, Current = Inventory(battle, definition), Cold = cold is null ? null : Inventory(cold, definition), Dispatches = directDispatches, Error = error.Message }));
            throw;
        }
        finally
        {
            world.AbandonFaultedDiagnosticBattle();
            world.EndDiagnosticBattle();
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            restored.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            restored.DiagnosticEffectDispatched += coldDispatches.Add;
            return restored;
        }

        void AssertMissingResponses(string actorId, byte nativeModel, ushort count, int animations)
        {
            NativeBattleGraphicsResource resource = definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == actorId).Resource;
            int physical = battle.NativeGraphics!.ActorIds.IndexOf(actorId);
            Assert.Equal(nativeModel, battle.NativeGraphics.Models[physical].ModelId4);
            Assert.Equal(count, BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan()));
            Assert.Equal(animations, resource.AnimationFrames.Length);
            Assert.False(resource.Contains(4));
            Assert.False(resource.Contains(5));
            Assert.Equal(0, resource.SequenceStart(4));
            Assert.Equal(0, resource.SequenceStart(5));
        }
    }

    private static object[] Inventory(BattleSession battle, BattleDefinition definition)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        return graphics.Models.Where(model => graphics.ActorIds[model.Index] is not null).Select(model =>
        {
            string actorId = graphics.ActorIds[model.Index]!;
            NativeBattleGraphicsResource resource = definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == actorId).Resource;
            byte? sequence = model.Sequence;
            int? offset = sequence is byte current && current * 2 + 2 <= resource.SequenceSection.Length ? BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan(current * 2, 2)) : null;
            int? directory11 = resource.SequenceSection.Length >= 24 ? BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan(22, 2)) : null;
            int? directory4 = resource.SequenceSection.Length >= 10 ? BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan(8, 2)) : null;
            int? directory5 = resource.SequenceSection.Length >= 12 ? BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan(10, 2)) : null;
            return (object)new { Actor = actorId, PhysicalIndex = model.Index, model.ModelId4, model.Flags0, model.Flags8, model.ResourceFlags44, Sequence = sequence, model.SequenceFrame, State = model.SequenceState is { } state ? new { state.Offset46, state.Default2, state.Pending3, state.Wait1, state.Pose, state.PoseFrame114, state.Callback42 } : null, SequenceCount = BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan()), Directory4 = directory4, Directory5 = directory5, Directory11 = directory11, CurrentSequenceOffset = offset, AnimationCount = resource.AnimationFrames.IsDefault ? -1 : resource.AnimationFrames.Length, Pose13Frames = !resource.AnimationFrames.IsDefault && resource.AnimationFrames.Length > 13 ? resource.AnimationFrames[13] : (byte?)null };
        }).ToArray();
    }
}
