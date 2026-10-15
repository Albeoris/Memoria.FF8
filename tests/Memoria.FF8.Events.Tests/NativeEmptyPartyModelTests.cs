using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEmptyPartyModelContract()
    {
        byte[] Content(bool modeled)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, 0)];
            if (modeled)
            {
                init.Add((Jsm.Opcode.SETMODEL, 0));
            }
            init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)]);
            (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1], [init.ToArray(), main]);
        }
        int metadataCalls = 0;
        EventEngine Create(bool modeled = false)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) =>
                {
                    metadataCalls++;
                    return 0;
                }
            };
            engine.LoadField(600, Content(modeled), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        void PrepareSyntheticTrace(EventEngine engine)
        {
            PartyTraceSample sample = new(10, 20, 0, 0, 0);
            engine.Scene.GetType().GetProperty(nameof(SceneEventState.PartyTrace))!.SetValue(engine.Scene, new PartyTraceState(0, 15, 30, Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray(), Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray()));
            engine.Scene.GetType().GetProperty(nameof(SceneEventState.NativePartyTraceVersion))!.SetValue(engine.Scene, 1);
            engine.Scene.GetType().GetProperty(nameof(SceneEventState.NativePlayerPosition))!.SetValue(engine.Scene, new FieldFixedPosition(10 << 12, 20 << 12, 0));
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        PrepareSyntheticTrace(live);
        Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 255, 255]) && live.Scene.PartyBindingMemory is { First: 0, Second: 255, Third: 255 } && live.Actors[0].ModelId == -1, "Native dummy SETPC leader must retain its party binding without a model resource.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        for (int callback = 0; callback < 4; callback++)
        {
            live.Apply(new PlayerAction(PlayerActionKind.Wait));
            restored.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Empty native model party trace diverged after independent restore.");
        }
        Require(metadataCalls == 0 && live.Actors[0].AnimationId == 0 && live.Actors[0].AnimationFrame == 0 && !live.Actors[0].AnimationPlaying && live.Actors[0].BaseAnimationId == 42 && live.Scene.NativePlayerPosition == new FieldFixedPosition(10 << 12, 20 << 12, 0), "Empty-model selection looked up or fabricated an animation resource, changed leader pose or manufactured default frames.");
        EventEngine modeled = Create(true);
        modeled.Step();
        modeled.Step();
        PrepareSyntheticTrace(modeled);
        int beforeMetadata = metadataCalls;
        int beforeModel = modeled.Actors[0].ModelId;
        int beforeAnimation = modeled.Actors[0].AnimationId;
        bool beforePlaying = modeled.Actors[0].AnimationPlaying;
        try
        {
            modeled.Step();
            throw new InvalidOperationException("Modeled party leader accepted missing frame metadata.");
        }
        catch (NotSupportedException)
        {
        }
        Require(beforeModel >= 0 && metadataCalls == beforeMetadata + 1 && modeled.Actors[0].ModelId == beforeModel && modeled.Actors[0].AnimationId == beforeAnimation && modeled.Actors[0].AnimationPlaying == beforePlaying, "Empty-resource branch admitted a modeled actor or changed animation state before metadata refusal.");
        Console.WriteLine("Empty native party model: SETPC binding/trace persists,532890 skips resource lookup without fabricated frames, ordinary waits/independent restore and modeled missing-metadata refusal passed.");
    }
}
