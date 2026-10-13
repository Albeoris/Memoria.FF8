using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyLinearTurnContract()
    {
        byte[] Content(int start, int duration, int slot, int x, int y)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] turn = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.PLTURN, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] absent = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] detach = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, turn, absent, target, detach]);
        }
        (GameSession Session, EventEngine Engine) Create(int start, int duration, int slot = 0, int x = 437, int y = -3376)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 2, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            engine.LoadField(600, Content(start, duration, slot, x, y), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        foreach ((int start, int rawDuration, int target, int x, int y) in new[] { (234, 65540, 261, 437, -3376), (0, 4, 128, 0, 1), (10, 0, 5, 437, -3376), (5, 4, 5, 437, -3376) })
        {
            var live = Create(start, rawDuration, x: x, y: y);
            live.Engine.Step();
            live.Engine.Step();
            Require(live.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && live.Engine.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]) && live.Engine.Scene.PartyBindingMemory is { First: 1 }, "PLTURN fixture did not distinguish original compact saved party binding from FOLLOWOFF runtime followers.");
            List<int[]> operands = [];
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.PLTURN))
                {
                    operands.Add(command.Arguments.ToArray());
                }
            };
            live.Engine.Step();
            ActorState actor = live.Engine.Actors[0];
            Require(operands.Count == 1 && operands[0].SequenceEqual(new[] { 0, rawDuration }) && actor.Direction == start && actor.NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "PLTURN consumed or narrowed its evidence operands, changed575/576 or changed direction during issue.");
            int duration = unchecked((byte)rawDuration);
            if (start == (byte)target)
            {
                Require(actor.RotationTarget is null && !actor.RotationUsesNativeLinear && live.Session.State.Variables[5000] == 77, "Equal-direction PLTURN did not return2 immediately within the same VM budget.");
                continue;
            }
            Require(actor.RotationStart == start && actor.RotationTarget == target && actor.RotationUsesNativeLinear && !actor.RotationUsesNativeCosine && actor.RotationTotalFrames == duration && actor.RotationFrames == duration + 1 && !live.Session.State.Variables.ContainsKey(5000), "PLTURN lost its signed nearest-angle target, BYTE duration, mode1 or initial return1 budget yield.");
            var restored = Create(start, rawDuration, x: x, y: y);
            restored.Engine.Restore(live.Engine.Capture());
            for (int elapsed = 0; elapsed <= duration; elapsed++)
            {
                live.Engine.Step();
                restored.Engine.Step();
                byte expected = unchecked((byte)NativeJumpMath.Interpolate(start, target, (short)duration, (short)elapsed));
                Require(actor.Direction == expected && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()) && live.Session.State.Variables.ContainsKey(5000) == (elapsed == duration), "PLTURN lost elapsed0/inclusive endpoint, exact integer linear interpolation, restore identity or same-budget completion continuation.");
                if (elapsed == duration / 2 && actor.RotationUsesNativeLinear)
                {
                    JsonElement valid = live.Engine.Capture();
                    JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
                    malformed["Actors"]![0]!["RotationFrames"] = 300;
                    try
                    {
                        live.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                        throw new InvalidOperationException("Malformed native linear rotation restored.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(valid, live.Engine.Capture()), "Invalid linear turn restore partially mutated the VM.");
                    restored = Create(start, rawDuration, x: x, y: y);
                    restored.Engine.Restore(valid);
                }
            }
            Require(actor.RotationTarget is null && !actor.RotationUsesNativeLinear && live.Session.State.Variables[5000] == 77 && operands.Count == 1, "PLTURN did not clear its mode1 rotation or consume its operands exactly once.");
        }
        foreach (int slot in new[] { -1, 1, 256 })
        {
            var invalid = Create(234, 4, slot);
            invalid.Engine.Step();
            invalid.Engine.Step();
            try
            {
                invalid.Engine.Step();
                throw new InvalidOperationException("PLTURN admitted absent or narrowed fullDWORD party slot.");
            }
            catch (EventScriptException) when (invalid.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.PLTURN))
            {
            }
            JsonElement fiber = invalid.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 0 && !item.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 4, slot }) && invalid.Engine.Actors[0].Direction == 234 && invalid.Engine.Actors[0].RotationTarget is null, "PLTURN refusal consumed operands or changed visible facing.");
        }
        Console.WriteLine("PLTURN: compact saved binding, exact native facing, BYTE duration, signed nearest angle and128 tie, integer linear elapsed0/endpoint, immediate equality, full VM restore and atomic refusal passed.");
    }
}
