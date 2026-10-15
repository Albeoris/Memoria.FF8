using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyActorDirectionContract()
    {
        foreach ((int x, int y, byte expected) in new (int, int, byte)[] { (0, 0, 0), (1, 0, 64), (0, 1, 128), (-1, 0, 192), (0, -1, 0), (1000, 1000, 96), (1000, -1000, 32), (-1000, -1000, 224), (-1000, 1000, 160), (437, -3376, 5) })
        {
            Require(FieldNativeScriptDirectionProbe.ResolveFacing(0, 0, x << 12, y << 12) == expected, "Direct native actor direction used MOVE snap policy or approximate angle math.");
        }
        Require(FieldNativeScriptDirectionProbe.ResolveFacing(4095, 0, -1, 0) == 192 && FieldNativeScriptDirectionProbe.ResolveFacing(short.MinValue << 12, 0, short.MaxValue << 12, 0) is null, "Direct facing lost SAR12 anchors or admitted wrapped native square-root overflow.");
        byte[] Content(int slot)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] turn = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PDIRA, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] absent = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 437), (Jsm.Opcode.PSHN_L, -3376), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] detach = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, turn, absent, target, detach]);
        }
        (GameSession Session, EventEngine Engine) Create(int slot = 0)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 2, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            engine.LoadField(600, Content(slot), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        live.Engine.Step();
        Require(live.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && live.Engine.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]) && live.Engine.Scene.PartyBindingMemory is { First: 1 } && live.Engine.Variables[194] == 1, "Synthetic FOLLOWOFF did not distinguish saved compact binding from detached runtime followers.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Engine.Actors[0].Direction == 5 && live.Engine.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0) && live.Session.State.Variables[5000] == 7 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "PDIRA selected a runtime follower/raw object slot, modified575/576, yielded or diverged after restore.");
        foreach (int slot in new[] { -1, 1, 256 })
        {
            var invalid = Create(slot);
            invalid.Engine.Step();
            invalid.Engine.Step();
            try
            {
                invalid.Engine.Step();
                throw new InvalidOperationException("PDIRA fabricated an absent actor or truncated an out-of-range fullDWORD slot.");
            }
            catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.PDIRA))
            {
            }
            JsonElement fiber = invalid.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Stack")[0].GetInt32() == slot && invalid.Engine.Actors[0].Direction == 0 && invalid.Engine.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "PDIRA refusal consumed its original slot or changed facing/motion authority.");
        }
        Console.WriteLine("PDIRA: saved compact BYTE binding survives FOLLOWOFF, raw SAR12/exact direction table,575/576 preservation, immediate continuation, independent restore and atomic absent/fullDWORD refusal passed.");
    }
}
