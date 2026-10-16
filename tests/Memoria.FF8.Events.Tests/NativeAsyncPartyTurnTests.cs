using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAsyncPartyTurnContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        EventEngine Create(int start, (Jsm.Opcode, int)[] tail)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), .. tail[..^1], (Jsm.Opcode.HALT, 0), tail[^1]];
            (Jsm.Opcode, int)[] absent = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 437), (Jsm.Opcode.PSHN_L, -3376), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] detach = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 2, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            engine.LoadField(600, SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, main, absent, target, detach]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            engine.Step();
            Require(engine.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && engine.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]) && engine.Scene.PartyBindingMemory is { First: 1 }, "Async party-turn fixture lost its compact saved party binding after FOLLOWOFF.");
            return engine;
        }
        static JsonElement Caller(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean());
        static int Mode(EventEngine engine) => engine.Capture().GetProperty("Actors")[0].GetProperty("NativeTurnMode580").GetInt32();
        static bool Marked(EventEngine engine, string address) => CapturedVariables(engine.Capture()).TryGetProperty(address, out _);
        (Jsm.Opcode, int)[] AsyncTail(int duration, int slot = 0) => [(Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.Unknown11, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.Unknown12, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];

        foreach (int rawDuration in new[] { 65540, 65536 })
        {
            (Jsm.Opcode, int)[] tail = AsyncTail(rawDuration);
            EventEngine live = Create(234, tail);
            int issues = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.Unknown11))
                {
                    issues++;
                    Require(command.Arguments.SequenceEqual([0, rawDuration]) && Mode(live) == 2 && live.Actors[0].Direction == 234 && live.Actors[0].RotationTarget == 261 && live.Actors[0].RotationFrames == unchecked((byte)rawDuration) + 1, "Async party turn lost its full operands, BYTE duration, nearest signed target or pre-sample mode2.");
                }
            };
            live.Step();
            int duration = unchecked((byte)rawDuration);
            Require(issues == 1 && Marked(live, "5000") && !Marked(live, "5001") && live.Actors[0].Direction == (duration == 0 ? 5 : 234) && Mode(live) == (duration == 0 ? 3 : 2), "Async turn waited before its successor or sync completed before observing the inclusive post-VM endpoint.");
            JsonElement waiting = Caller(live);
            Require(waiting.GetProperty("WaitReason").GetString() == "NativeTurnCompletion" && waiting.GetProperty("Stack").GetArrayLength() == 0, "Party-turn sync did not retain a zero-operand native mode barrier.");
            int heldPosition = waiting.GetProperty("Position").GetInt32();
            EventEngine restored = Create(234, tail);
            restored.Restore(live.Capture());
            Require(live.Capture().GetRawText() == restored.Capture().GetRawText(), "Pending async turn did not round-trip exactly.");
            for (int elapsed = 1; elapsed <= duration; elapsed++)
            {
                live.Step();
                restored.Step();
                byte expected = unchecked((byte)NativeCosineInterpolation.Interpolate(234, 261, duration, elapsed));
                Require(live.Actors[0].Direction == expected && Mode(live) == (elapsed == duration ? 3 : 2) && !Marked(live, "5001") && Caller(live).GetProperty("Position").GetInt32() == heldPosition && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Async turn/sync lost its inclusive cosine endpoint, held PC or independently restored phase.");
            }
            EventEngine endpoint = Create(234, tail);
            endpoint.Restore(live.Capture());
            live.Step();
            restored.Step();
            endpoint.Step();
            Require(Mode(live) == 3 && Marked(live, "5001") && issues == 1 && live.Actors[0].RotationTarget is null && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), endpoint.Capture()), "Completed native turn mode did not release sync exactly once on its next callback.");
        }

        EventEngine equal = Create(5, AsyncTail(4));
        equal.Step();
        Require(Mode(equal) == 3 && equal.Actors[0].RotationTarget is null && Marked(equal, "5000") && Marked(equal, "5001"), "Equal-direction async party turn did not explicitly set mode3 and release same-budget sync.");
        foreach (bool synchronousEqual in new[] { false, true })
        {
            (Jsm.Opcode, int)[] tail = synchronousEqual ? [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.Unknown12, 0), (Jsm.Opcode.PSHN_L, 79), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)] : [(Jsm.Opcode.Unknown12, 0), (Jsm.Opcode.PSHN_L, 79), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
            EventEngine idle = Create(5, tail);
            Require(Mode(idle) == 0, "Fresh native actor invented a completed turn mode.");
            idle.Step();
            JsonElement waiting = idle.Capture();
            EventEngine restored = Create(5, tail);
            restored.Restore(waiting);
            int position = Caller(idle).GetProperty("Position").GetInt32();
            for (int callback = 0; callback < 3; callback++)
            {
                idle.Step();
                restored.Step();
                Require(Mode(idle) == 0 && !Marked(idle, "5001") && Caller(idle).GetProperty("Position").GetInt32() == position && JsonElement.DeepEquals(idle.Capture(), restored.Capture()), "Sync accepted allocated mode0 as completed, or synchronous equal PCTURN overwrote its retained native mode.");
            }
        }

        (Jsm.Opcode, int)[] overwriteTail = [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.Unknown11, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 258), (Jsm.Opcode.Unknown11, 0), (Jsm.Opcode.Unknown12, 0), (Jsm.Opcode.PSHN_L, 80), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
        EventEngine overwritten = Create(234, overwriteTail);
        overwritten.Step();
        overwritten.Step();
        float replacementStart = overwritten.Actors[0].Direction;
        overwritten.Step();
        Require(replacementStart != 234 && overwritten.Actors[0].RotationStart == replacementStart && overwritten.Actors[0].RotationTotalFrames == 2 && overwritten.Actors[0].RotationFrames == 2 && Mode(overwritten) == 2 && !Marked(overwritten, "5001"), "Async turn refused an active turn or failed to replace its start, low BYTE duration and elapsed0 phase.");
        EventEngine overwrittenCopy = Create(234, overwriteTail);
        overwrittenCopy.Restore(overwritten.Capture());
        for (int callback = 0; callback < 3; callback++)
        {
            overwritten.Step();
            overwrittenCopy.Step();
            Require(JsonElement.DeepEquals(overwritten.Capture(), overwrittenCopy.Capture()), "Replaced native turn diverged after snapshot restore.");
        }
        Require(Mode(overwritten) == 3 && Marked(overwritten, "5001") && overwritten.LastFault is null, "Replacement native turn failed to release its sync barrier.");

        foreach (int slot in new[] { -1, 1, 256 })
        {
            EventEngine invalid = Create(234, AsyncTail(4, slot));
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Async party turn admitted an absent or narrowed full DWORD party slot.");
            }
            catch (EventScriptException) when (invalid.LastFault?.Opcode == nameof(Jsm.Opcode.Unknown11))
            {
            }
            Require(Caller(invalid).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([4, slot]) && Mode(invalid) == 0 && invalid.Actors[0].Direction == 234, "Rejected async party turn consumed its full operands or mutated native turn state.");
        }
        Console.WriteLine("Async party turn368/sync369: compact saved binding, full operands/BYTE duration, immediate successor, inclusive cosine/mode3 barrier, allocated0/equal behavior, active overwrite and independent restore PASS.");
    }
}
