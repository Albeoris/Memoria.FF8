using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFollowOnContract()
    {
        (Jsm.Opcode, int)[] Player(int label, int character) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1, (16 << 7) | 1], [Player(0, 0), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.RET, 0)], Player(16, 1), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FOLLOWON, 0), (Jsm.Opcode.RET, 0)]]);
        EventEngine Create()
        {
            var session = new GameSession(new GameState());
            var engine = new EventEngine(session, session.State.Variables) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 255, 255]) && live.Scene.PartyBindingMemory is { First: 0, Second: 1, Third: 255 }, "FOLLOWOFF fixture did not detach only the runtime slot while preserving native saved party bytes.");
        JsonElement detached = live.Capture();
        EventEngine restored = Create();
        restored.Restore(detached);
        NativeMotionAuthority? authority = live.Actors[1].NativeMotionAuthority;
        int animation = live.Actors[1].AnimationId;
        live.Step();
        restored.Step();
        live.Step();
        restored.Step();
        Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 255]) && live.Scene.PartyBindingMemory is { First: 0, Second: 1, Third: 255 } && live.Actors[1].NativeMotionAuthority == authority && live.Actors[1].AnimationId == animation && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "FOLLOWON changed saved binding bytes, animation/motion scalars or restored continuation instead of writing the original runtime follow slot.");
        Console.WriteLine("Native FOLLOWON: character membership lookup, retained compact BYTE598 runtime slot restoration, preserved B194..196/animation/motion and independent detached-state restore passed.");
    }
}
