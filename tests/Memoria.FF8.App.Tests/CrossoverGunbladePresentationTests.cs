using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGunbladePresentationTests
{
    [Fact]
    public void Original_Revolver_sequence_approaches_applies_one_trigger_contact_and_returns_across_JSON()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the installed original FFVIII resources.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(game.ReadNewGameCharacterVitals(), assets, 0);
        NativeBattleGraphicsResource weapon = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        uint duration = game.ReadNativeBattleGraphicsRules().UiPublicationDuration!.Value;
        NativeBattleVector origin = new(0, 0, 2200);
        NativeBattleVector target = new(0, 0, -3800);
        const short scale = 4096;
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel unloaded = new(0, 0)
        {
            Transform = new(origin, origin, 0, 0, scale),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        };
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(unloaded, 0, 0, ref random);
        model = NativeBattleGraphicsModels.SelectSequence(model, 1);
        model = NativeBattleSequenceMachine.Advance(model, weapon, new(0, 0)).Model;
        CrossoverGunbladePresentationState action = CrossoverGunbladePresentation.Begin(model, weapon, game.ReadNativeBattleCommandSequences(), new(0));
        Assert.Equal((byte)13, action.Model.Sequence);
        int distance = NativeBattleActionGeometryRules.ContactDistance(NativeBattleActionGeometryRules.DistanceScale(origin, target), scale, scale);
        Trace uninterrupted = Play(false, true, 0);
        Trace restored = Play(true, true, 0);
        Assert.Equal(uninterrupted, restored);
        Assert.Equal((16, 21, 36), (restored.SoundCallback, restored.ContactCallback, restored.CompletedCallback));
        Assert.Equal((short)20, restored.Timing);
        Assert.Equal(1201, restored.ContactZ);
        Assert.Equal(12, restored.ParticleResource);
        Assert.True(restored.SoundParticles > 0);
        Assert.Equal(origin.Z, restored.StandingZ);
        Assert.Equal((short)0, Play(true, false, 2).Timing);
        Assert.Equal(13, Play(false, true, 2).ParticleResource);

        Trace Play(bool cold, bool trigger, int contactFlags)
        {
            CrossoverGunbladePresentationState state = action;
            NativeGlobalRandomState global = random;
            NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
            NativeBattleTriggerReceipt? prepared = null;
            int published = 0;
            int resource = -1;
            int emissions = 0;
            int soundCallback = 0;
            int contactCallback = 0;
            int contactZ = 0;
            bool restoredSnapshot = false;
            NativeBattleSequenceInputs inputs = new(0, 0)
            {
                ReadModelExternal = (_, variable) => variable switch
                {
                    17 => distance,
                    25 => contactFlags,
                    46 => 0,
                    18 or 19 => memory.ReadSequenceGlobal5044B0(variable),
                    _ => throw new NotSupportedException($"Unexpected sequence variable {variable}.")
                },
                WriteExternal = (variable, value) => memory = memory.WriteSequenceGlobal5048E0(variable, value),
                ScheduleSoundParticle = (program, two, bone) =>
                {
                    (auxiliary, global, bool allocated) = auxiliary.AppendSoundParticle(0, two, bone, program, global);
                    Assert.True(allocated);
                },
                ApplyActionContacts = current =>
                {
                    Assert.NotNull(prepared);
                    published++;
                    return current;
                },
                ScheduleContactEffect = (current, program) =>
                {
                    Assert.Equal(1, published);
                    NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(program, 0);
                    resource = (byte)particle.PackedParameters;
                    return (current, particle.NextOffset);
                }
            };
            for (int callback = 1; callback <= 90; callback++)
            {
                NativeBattleAuxiliaryStep tasks = auxiliary.Advance(new()
                {
                    ReadBone = (_, selector, fraction) => NativeBattlePoseMatrices.ReadBone(state.Model, weapon.PoseSource!, selector, fraction),
                    ReadPan = _ => 64
                });
                auxiliary = tasks.State;
                emissions += tasks.Emissions.Length;
                bool press = trigger && state.TriggerWindowOpen;
                if (cold && state.TriggerWindowOpen && !restoredSnapshot)
                {
                    state = RoundTrip(state);
                    auxiliary = RoundTrip(auxiliary);
                    global = RoundTrip(global);
                    restoredSnapshot = true;
                }
                CrossoverGunbladePresentationStep step = CrossoverGunbladePresentation.Advance(state, weapon, inputs, 0, duration, press, receipt =>
                {
                    Assert.Null(prepared);
                    prepared = receipt;
                });
                state = step.State;
                if (step.TriggerSound)
                {
                    Assert.Equal(0, soundCallback);
                    soundCallback = callback;
                }
                if (step.Contact is { } contact)
                {
                    Assert.Equal(prepared, contact);
                    Assert.Equal(0, contactCallback);
                    Assert.NotNull(state.Model.CurrentBodyBoneMatrices);
                    contactCallback = callback;
                    contactZ = state.Model.GeometryWorldMatrix!.Z;
                }
                if (state.Complete)
                {
                    Assert.Equal(cold, restoredSnapshot);
                    Assert.Equal(1, published);
                    Assert.Equal((short)0, state.Model.SequenceState!.Header40);
                    return new(soundCallback, contactCallback, callback, prepared!.Timing, contactZ, resource, emissions, state.Model.GeometryWorldMatrix!.Z);
                }
            }
            throw new InvalidOperationException("The original Attack sequence did not return to standing within the test bound.");
        }
    }

    private static T RoundTrip<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;

    private sealed record Trace(int SoundCallback, int ContactCallback, int CompletedCallback, short Timing, int ContactZ, int ParticleResource, int SoundParticles, int StandingZ);
}
