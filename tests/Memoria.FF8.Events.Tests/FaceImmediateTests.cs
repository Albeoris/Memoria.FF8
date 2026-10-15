using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFaceImmediateContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] Content(int duration, bool asynchronous, bool unknown = false)
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, 65542), (Jsm.Opcode.PSHN_L, 65543), (Jsm.Opcode.PSHN_L, 65528)];
            if (unknown)
            {
                main.AddRange([(Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0)]);
            }
            else
            {
                main.Add((Jsm.Opcode.PSHN_L, duration));
            }
            main.AddRange([(asynchronous ? Jsm.Opcode.RFACEDIRI : Jsm.Opcode.FACEDIRI, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 75), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], main.ToArray()]);
        }
        EventEngine Create(byte[] content)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        foreach (bool asynchronous in new[] { false, true })
        {
            foreach (int duration in new[] { 0, 65538, 65535 })
            {
                byte[] content = Content(duration, asynchronous);
                EventEngine live = Create(content);
                Jsm.Opcode opcode = asynchronous ? Jsm.Opcode.RFACEDIRI : Jsm.Opcode.FACEDIRI;
                Require(JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == opcode).Parameter == (int)opcode, "Immediate face fixture did not retain the decoded unencoded opcode parameter.");
                live.Step();
                ActorState actor = live.Actors[0];
                JsonElement pending = live.Capture();
                int ticks = duration & 65535;
                Require(actor.FaceDuration == ticks && actor.FaceProgress == (ticks == 0 ? 0 : 1) && actor.FaceTargetPoint is null && actor.FacePitchTarget == 6 && actor.FacePendingBaseline == 7 && actor.FaceYawTarget == -8 && actor.Direction == 0 && actor.X == 0 && actor.Y == 0 && actor.Z == 0, "Immediate face did not consume signed target/unsigned duration WORDs independently of body pose, target point and geometry providers.");
                Require(CapturedVariables(pending).TryGetProperty("5000", out _) == (asynchronous || ticks == 0), "FACEDIRI/RFACEDIRI lost blocking versus immediate return2 continuation.");
                Require(CapturedVariables(pending).TryGetProperty("5001", out _) == (ticks == 0), "Face sync completed before the VM observed the post-body duration equality.");
                if (ticks == 2)
                {
                    Vector3 midpoint = actor.FirstControlRotation!.Value;
                    Require(Math.Abs(midpoint.Y - 3 * MathF.Tau / 256) < 0.00001 && Math.Abs(midpoint.Z + 252 * MathF.Tau / 256) < 0.00001, "Immediate angles did not share the original signed-word cosine interpolation and low-BYTE output.");
                }
                EventEngine restored = Create(content);
                restored.Restore(pending);
                for (int callback = 0; callback < 3; callback++)
                {
                    live.Step();
                    restored.Step();
                    Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Immediate face pending/completion/override-release continuation diverged after independent restore.");
                }
                if (ticks < 3)
                {
                    JsonElement complete = live.Capture();
                    JsonElement fiber = complete.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
                    Require(CapturedVariables(complete).GetProperty("5000").GetInt32() == 73 && CapturedVariables(complete).GetProperty("5001").GetInt32() == 75 && fiber.GetProperty("Results").GetProperty("7").GetInt32() == 77 && actor.FaceProgress == ticks && !actor.FaceReturning && actor.FirstControlRotation is null && (ticks == 0 || actor.FaceElapsed == 6 && actor.FaceSecondAngle == -8 && actor.FaceFirstAngle == 7), "Immediate face lost untouched stack state, final native word copies or the following draw's override release.");
                }
            }
            byte[] invalid = Content(2, asynchronous, unknown: true);
            EventEngine refused = Create(invalid);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Immediate face accepted an unknown consumed WORD.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == (asynchronous ? "RFACEDIRI" : "FACEDIRI"))
            {
            }
            JsonElement failed = refused.Capture();
            JsonElement faultedFiber = failed.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(faultedFiber.GetProperty("Stack").GetArrayLength() == 5 && faultedFiber.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && refused.Actors[0].FaceDuration == 0 && refused.Actors[0].FaceProgress == 0 && !refused.Actors[0].FaceReturning && refused.Actors[0].FirstControlRotation is null, "Refused immediate face consumed operands, changed its PC or partially published target/phase state.");
        }
        Console.WriteLine("FACEDIRI/RFACEDIRI: four low WORDs, ignored raw opcode parameter, direct cosine targets, sync/async timing, zero/full duration, retained stack, override release and pending restore passed.");
    }
}
