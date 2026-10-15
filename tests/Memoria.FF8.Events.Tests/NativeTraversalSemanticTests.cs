using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTraversalSemanticContract(NativeCharacterVitals vitals, NativeEconomy economy, NativeInputConfiguration input)
    {
        NativeSemanticInputPulseContract(vitals, economy, input);
        int fastCallbacks = CompleteCurrentMode3(28);
        int slowCallbacks = CompleteCurrentMode3(14);
        Require(slowCallbacks > fastCallbacks, "Semantic native traversal replay used an old callback count instead of its current speed.");

        EventEngine reverse = CreateLadderMode3Fixture(vitals, economy, input, true, automatic: false);
        ReachLadderMode3Issue(reverse);
        Require(reverse.ApplySemanticNativeTraversal(reverse.PendingSemanticTraversal!).Accepted, "Native mode3 refused its current forward producer.");
        reverse.Step();
        reverse.Step();
        ReplayNativeTraversal reversed = reverse.ResolvePendingSemanticTraversal(0x4000) ?? throw new InvalidOperationException("Current mode3 reverse input had no actual producer.");
        Require(reversed.Inferred && !reversed.Forward && reverse.ApplySemanticNativeTraversal(reversed).Accepted, "Legacy reverse inference did not bind the current native endpoint.");
        List<ReplayReceipt> reverseReceipts = [];
        reverse.CausalReceiptRecorded += reverseReceipts.Add;
        for (int callback = 0; callback < 32 && reverse.Actors[0].NativeMovementCompletion542 != 2; callback++)
        {
            reverse.Step();
        }
        Require(reverse.Actors[0].NativeMovementCompletion542 == 2 && reverse.Actors[0].NativeTurnAccumulator474 == 0 && reverseReceipts.Count(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal && receipt.Value == -1) == 1, "Semantic reverse did not reach and report its actual entry endpoint exactly once.");

        EventEngine mode4 = CreateNativeLadder(up: true);
        ReachNativeLadderClimb(mode4);
        ReplayNativeTraversal goal4 = mode4.PendingSemanticTraversal ?? throw new InvalidOperationException("Controlled mode4 has no current semantic producer.");
        List<ReplayReceipt> mode4Receipts = [];
        mode4.CausalReceiptRecorded += mode4Receipts.Add;
        Require(goal4.Mode == 4 && mode4.ApplySemanticNativeTraversal(goal4).Accepted, "Semantic mode4 refused its current native producer.");
        for (int callback = 0; callback < 64 && mode4.Actors[0].NativeMovementCompletion542 != 2; callback++)
        {
            mode4.Step();
            Require(mode4.Actors[0].NativeMovementCompletion542 == 2 || !mode4Receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal), "Mode4 reported completion before its actual native exit/drain finished.");
        }
        Require(mode4.Actors[0].NativeMovementCompletion542 == 2 && mode4Receipts.Count(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal && receipt.Arguments is [4, 1, _]) == 1, "Mode4 lost or duplicated its controlled native completion cause.");

        EventEngine automatic = CreateLadderMode3Fixture(vitals, economy, input, true);
        ReachLadderMode3Issue(automatic);
        List<ReplayReceipt> automaticReceipts = [];
        automatic.CausalReceiptRecorded += automaticReceipts.Add;
        Require(automatic.PendingSemanticTraversal is null && automatic.ResolvePendingSemanticTraversal(0x1000) is null, "Automatic traversal fabricated a controlled native input producer.");
        for (int callback = 0; callback < 16 && automatic.Actors[0].NativeMovementCompletion542 != 2; callback++)
        {
            automatic.Step();
        }
        Require(!automaticReceipts.Any(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal), "Automatic traversal fabricated a controlled input completion receipt.");
        Console.WriteLine("Semantic native traversal: current-speed endpoint regeneration, pure legacy direction inference, reverse endpoint and exactly-once controlled mode3/mode4 completion pass.");

        int CompleteCurrentMode3(int speed)
        {
            EventEngine engine = CreateLadderMode3Fixture(vitals, economy, input, true, automatic: false, speed: speed);
            ReachLadderMode3Issue(engine);
            string before = engine.Capture().GetRawText();
            ReplayNativeTraversal current = engine.PendingSemanticTraversal ?? throw new InvalidOperationException("Controlled mode3 has no current semantic producer.");
            ReplayNativeTraversal inferred = engine.ResolvePendingSemanticTraversal(0x1000) ?? throw new InvalidOperationException("Legacy held input lost its current native producer.");
            Require(current.Mode == 3 && current.Forward && inferred.Inferred && inferred.Forward && before == engine.Capture().GetRawText(), "Pending traversal metadata or legacy inference mutated the live native process.");
            List<ReplayReceipt> receipts = [];
            engine.CausalReceiptRecorded += receipts.Add;
            Require(engine.ApplySemanticNativeTraversal(current).Accepted, "Semantic traversal refused its actual issuing owner.");
            int callbacks = 0;
            for (; callbacks < 64 && engine.Actors[0].NativeMovementCompletion542 != 2; callbacks++)
            {
                engine.Step();
            }
            Require(engine.Actors[0].NativeMovementCompletion542 == 2 && receipts.Count(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal && receipt.Value == 1) == 1, "Semantic mode3 failed to report its actual current endpoint once.");
            engine.Step();
            Require(receipts.Count(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal) == 1, "The completed native body duplicated its semantic cause on the next callback.");
            return callbacks;
        }
    }

    private static void NativeSemanticInputPulseContract(NativeCharacterVitals vitals, NativeEconomy economy, NativeInputConfiguration input)
    {
        foreach (ushort pulse in new ushort[] { 0x10, 0x40 })
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            session.InitializeNewGameInputConfiguration(input);
            EventEngine engine = new(session, session.EventMemory);
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, pulse), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.JMP, -4), (Jsm.Opcode.RET, 8)]]);
            engine.LoadField(74, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            engine.Step();
            JsmProgram program = JsmProgram.Read(content);
            EventScript script = program.Objects[0].Scripts.Single(script => script.Id == 1);
            int operation = Enumerable.Range(script.Start, script.End - script.Start).Single(index => program.Operations[index].Opcode == Jsm.Opcode.KEYON);
            ReplayScriptGoal goal = new(74, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), NativeFieldRuntimeProfile.FF8En.ExecutableHash, 0, 1, operation, (int)Jsm.Opcode.KEYON, false);
            long frame = engine.Frame;
            PlayerTrigger trigger = engine.NextTrigger;
            List<ReplayReceipt> receipts = [];
            engine.CausalReceiptRecorded += receipts.Add;
            JsonElement beforeRefusal = engine.Capture();
            foreach ((ReplayScriptGoal invalidGoal, ushort invalidPulse) in new[] { (goal, (ushort)0x80), (goal with { Operation = script.Start, Code = (int)Jsm.Opcode.LBL }, pulse), (goal with { ProgramHash = new string('0', 64) }, pulse) })
            {
                try
                {
                    engine.QueueSemanticInputPulse(invalidGoal, invalidPulse);
                    throw new InvalidOperationException("Semantic pulse accepted an invalid mask or native producer.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(beforeRefusal, engine.Capture()), "Refused semantic pulse mutated the current sampler or VM.");
            }
            engine.QueueSemanticInputPulse(goal, pulse);
            Require(engine.Frame == frame && engine.NextTrigger == trigger && session.State.Variables[5000] == 0 && receipts.Count == 0 && engine.ButtonInput is { PhysicalHeld: 0, RawHeld: 0, PendingPulse: var pending } && pending == input.RawForMappedPulse(pulse) && session.ReplayJournal is { IsComplete: true, Endpoint.Kind: ReplayEntryKind.InputPulse } && session.ReplayJournal.Endpoint.ScriptGoal == goal && session.ReplayJournal.Endpoint.MappedPulse == pulse && session.ReplayJournal.Endpoint.Advanced == 0, "Semantic pulse advanced execution, changed physical held input, lost its authentic zero-callback queue or wrote its KEY result before native sampling.");
            engine.Step();
            Require(session.State.Variables[5000] == 1 && engine.ButtonInput is { PhysicalHeld: 0, PendingPulse: 0 } && receipts.Any(receipt => receipt.Kind == ReplayReceiptKind.InputSample && receipt.Operation == operation && receipt.Value == 0 && receipt.Arguments is { Length: 5 } arguments && arguments[4] == pulse), "Native KEYON did not consume and retain its actual mapped pulse separately from physical held input.");
            engine.Step();
            Require(session.State.Variables[5000] == 0 && engine.ButtonInput is { RawHeld: 0, RawPressed: 0 }, "Semantic pulse fabricated a persistent edge after its ordinary sampled release.");
        }
        Console.WriteLine("Semantic native input pulses: pure queued Cancel/Confirm edges, atomic producer/mask refusal, current KEYON sampling, separate pulse receipt and ordinary release pass.");
    }
}
