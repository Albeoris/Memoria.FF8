using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private sealed class PartyActionObserverStop : Exception
    {
    }

    private static void NativePartyActionObservationContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        EventEngine Create(Jsm.Opcode opcode, int slot = 0, int priority = 1, int selector = 4)
        {
            byte[] content = SyntheticObjects([1, (2 << 7) | 2, (5 << 7) | 1],
            [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, selector), (opcode, slot), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 4), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 5), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)],
                [(Jsm.Opcode.LBL, 6), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]
            ]);
            EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy())) { AnimationFrameCount = (_, _) => 1 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: Memoria.FF8.Runtime.NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return engine;
        }
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.PREQ, Jsm.Opcode.PREQSW, Jsm.Opcode.PREQEW })
        {
            EventEngine stopped = Create(opcode);
            PartyActionObserverStop stop = new();
            NativePartyActionRequest? observed = null;
            JsonElement? atBoundary = null;
            stopped.BeforeNativePartyActionRequest += request =>
            {
                observed = request;
                atBoundary = stopped.Capture();
                throw stop;
            };
            try
            {
                stopped.Step();
                throw new InvalidOperationException("Native party action observer did not stop at its pre-execution boundary.");
            }
            catch (PartyActionObserverStop exception)
            {
                Require(ReferenceEquals(exception, stop), "Party observer exception was wrapped or replaced.");
            }
            JsonElement diagnostic = stopped.Capture();
            Require(observed is { FieldId: 600, CallerObjectId: 0, ScriptId: 1, PartySlot: 0, RawPriority: 1, LocalSelector: 4, ReceiverObjectId: 1, ReceiverCharacterId: 0, PhysicalBinding: 1, DeclaredScriptCount: 3, PhysicalPointerIndex: 6, PhysicalCodeObjectId: 2, PhysicalScriptId: 6 } && observed.ProgramHash.Length == 64 && observed.Frame == stopped.Frame && observed.FiberSequence > 0 && stopped.LastFault is null && atBoundary is not null && JsonElement.DeepEquals(atBoundary.Value, diagnostic), "Party observer changed native PC/stack/slots, created a fault, or supplied an invented receiver/code identity.");
            JsonElement caller = diagnostic.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(caller.GetProperty("Position").GetInt32() == observed!.Operation && caller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([4, 1]) && !diagnostic.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.TryGetProperty("NativePartyRequestPointer", out _)), "Party observer consumed operands or queued ownership before policy could inspect the actual request.");
            // This is diagnostic state inside a callback, not a resumable checkpoint.
            EventEngine plain = Create(opcode);
            EventEngine passive = Create(opcode);
            int observations = 0;
            passive.BeforeNativePartyActionRequest += _ => observations++;
            plain.Step();
            passive.Step();
            Require(observations == 1 && JsonElement.DeepEquals(plain.Capture(), passive.Capture()), "Read-only request observation changed ordinary native execution.");
            foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
            {
                EventEngine ignored = Create(opcode, slot, priority, 99999);
                int ignoredObservations = 0;
                ignored.BeforeNativePartyActionRequest += _ => ignoredObservations++;
                ignored.Step();
                Require(ignoredObservations == 0 && ignored.LastFault is null, "An absent participant or occupied priority emitted a fictitious free request observation.");
            }
            EventEngine invalid = Create(opcode, selector: 99999);
            int invalidObservations = 0;
            invalid.BeforeNativePartyActionRequest += _ => invalidObservations++;
            try
            {
                invalid.Step();
                throw new InvalidOperationException("Invalid native pointer reached the automation observer before genuine preflight.");
            }
            catch (EventScriptException)
            {
                Require(invalidObservations == 0 && invalid.LastFault?.Opcode == opcode.ToString(), "Policy observation masked a genuine unsupported native pointer.");
            }
        }
        Console.WriteLine("Native party action observation: exact free receiver/raw pointer payload, pre-PC/operand/queue boundary, unchanged exception/no native fault, transparent passive observer and busy/absent/invalid preflight exclusions passed.");
    }
}
