using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyTurnReplacementContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach ((Jsm.Opcode oldOpcode, Jsm.Opcode replacementOpcode, bool equal) in new[]
        {
            (Jsm.Opcode.CTURN, Jsm.Opcode.PCTURN, false),
            (Jsm.Opcode.CTURN, Jsm.Opcode.PCTURN, true),
            (Jsm.Opcode.PLTURN, Jsm.Opcode.PCTURN, true),
            (Jsm.Opcode.PCTURN, Jsm.Opcode.PLTURN, true)
        })
        {
            EventEngine Create()
            {
                (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 234), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
                (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, oldOpcode == Jsm.Opcode.CTURN ? 2 : 0), (Jsm.Opcode.PSHN_L, 15), (oldOpcode, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
                (Jsm.Opcode, int)[] replace = [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 65540), (replacementOpcode, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 8)];
                (Jsm.Opcode, int)[] absent = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
                (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 437), (Jsm.Opcode.PSHN_L, -3376), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
                (Jsm.Opcode, int)[] request = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
                GameSession session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 2 };
                // The existing raw-facing certificate is tested separately. Select
                // an equal resolved angle only while replacing an executing turn.
                engine.ResolveNativeActorFacing = query => equal && engine.Actors[0].RotationTarget is not null ? checked((byte)engine.Actors[0].Direction) : FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY);
                engine.LoadField(600, SyntheticObjects([2, 16 << 7, (32 << 7) | 1], [caller, main, replace, absent, target, request]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }

            EventEngine live = Create();
            int replacements = 0;
            int replacementStart = -1;
            byte retainedMode = oldOpcode == Jsm.Opcode.PLTURN ? (byte)1 : (byte)2;
            byte expectedMode = equal ? retainedMode : (byte)2;
            live.CommandExecuted += command =>
            {
                if (command.Name == replacementOpcode.ToString() && command.Arguments.SequenceEqual([0, 65540]))
                {
                    replacements++;
                    ActorState actor = live.Actors[0];
                    replacementStart = checked((byte)actor.Direction);
                    Require(actor.RotationStart == replacementStart && actor.RotationTotalFrames == 4 && actor.RotationFrames == 5 && actor.NativeTurnMode580 == expectedMode && actor.RotationUsesNativeCosine == (expectedMode == 2) && actor.RotationUsesNativeLinear == (expectedMode == 1), "Party-turn replacement lost the shared physical mode, low BYTE duration or elapsed0 issue state.");
                    Require(actor.NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "Party-turn replacement changed independent movement or facing-hold ownership.");
                }
            };
            for (int callback = 0; callback < 12 && replacements == 0; callback++)
            {
                live.Step();
            }
            Require(replacements == 1 && replacementStart != 234 && live.Actors[0].RotationFrames == 4, "Requested party turn did not replace the active CTURN/party turn and take its post-VM elapsed0 sample.");
            JsonElement pending = live.Capture();
            JsonElement fibers = pending.GetProperty("Fibers");
            Require(fibers.EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1 && !fiber.GetProperty("Complete").GetBoolean()), "Replacing a turn discarded its original waiting VM fiber.");
            Require(!CapturedVariables(pending).TryGetProperty("5000", out _) && CapturedVariables(pending).TryGetProperty("5001", out _) == equal, "Equal party turn did not return2 immediately, or the original waiter completed before physical mode3.");
            EventEngine restored = Create();
            restored.Restore(pending);
            Require(JsonElement.DeepEquals(pending, restored.Capture()), "A replaced party turn did not restore independently.");
            for (int elapsed = 1; elapsed <= 4; elapsed++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && !CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "Shared turn wait/physical sampling diverged or released before its inclusive endpoint.");
                if (equal)
                {
                    Require(live.Actors[0].Direction == replacementStart, "Equal-target replacement continued the old angle instead of its newly written constant turn words.");
                }
            }
            Require(live.Actors[0].NativeTurnMode580 == 3 && live.Actors[0].RotationTarget is null, "Replacement did not publish physical mode3 at its inclusive duration endpoint.");
            for (int callback = 0; callback < 4; callback++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Shared turn completion diverged after restore.");
            }
            Require(CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 78 && replacements == 1, "Shared turn completion did not release both surviving VM owners without a second operand pop.");
        }
        Console.WriteLine("Party turn replacement: requested active CTURN/PLTURN/PCTURN, equal mode preservation, BYTE duration, shared completion and independent restore PASS.");
    }
}
