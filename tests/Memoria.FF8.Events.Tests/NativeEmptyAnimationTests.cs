using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEmptyAnimationWriters(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.RANIME, Jsm.Opcode.RANIMEKEEP, Jsm.Opcode.RANIMELOOP })
        {
            foreach (bool setModelLater in new[] { false, true })
            {
                List<(Jsm.Opcode, int)> operations = [];
                if (setModelLater)
                {
                    operations.Add((Jsm.Opcode.SETMODEL, 3));
                }
                operations.AddRange([(Jsm.Opcode.PSHN_L, 77), (opcode, 2), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.ANIMESTOP, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001)]);
                EventEngine Create() => CreateNativeAnimationBackupFixture(vitals, economy, operations.ToArray(), ushort.MaxValue);
                EventEngine live = Create();
                ushort flags = opcode == Jsm.Opcode.RANIME ? (ushort)0x4000 : opcode == Jsm.Opcode.RANIMEKEEP ? (ushort)0x8000 : (ushort)0x2000;
                AdvanceToNativeAnimationIssue(live, 2, flags);
                NativeActorAnimationHeader issued = live.Actors[0].NativeAnimationHeader!;
                JsonElement pending = live.Capture();
                Require(issued is { BoundModelId: -1, Frame: 0, First: 0, Terminal: null, Increment: 16 } && issued.Flags == flags && !CapturedVariables(pending).TryGetProperty("5000", out _) && live.Actors[0].AnimationId == 0 && live.Actors[0].NativeAnimationBackup == new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0), "Empty animation writer invented model metadata, consumed unrelated operands, changed its backup or lost return3 yield.");
                EventEngine restored = Create();
                restored.Restore(pending);
                live.Step();
                restored.Step();
                Require(live.Actors[0].NativeAnimationHeader is { BoundModelId: -1, AnimationId: 0, Frame: 0, First: 0, Terminal: null, Flags: 0x2000 } && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && !CapturedVariables(live.Capture()).TryGetProperty("5001", out _) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Empty ANIMESTOP did not write its real idle header, retain opaque terminal and yield before its successor.");
                EventEngine stoppedCopy = Create();
                stoppedCopy.Restore(live.Capture());
                live.Step();
                restored.Step();
                stoppedCopy.Step();
                Require(live.Actors[0].NativeAnimationHeader is { BoundModelId: -1, Frame: null, Terminal: null, Flags: null } && CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 78 && live.Actors[0].ModelId == (setModelLater ? 3 : ushort.MaxValue) && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), stoppedCopy.Capture()), "Empty native callback guessed its opaque phase, rebound later SETMODEL or diverged after restored writer state.");
            }
        }
        foreach (int selector in new[] { 65538, -1 })
        {
            EventEngine alias = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.RANIME, selector)], ushort.MaxValue);
            AdvanceToNativeAnimationIssue(alias, unchecked((byte)selector), 0x4000);
            Require(alias.Actors[0].NativeAnimationHeader is { BoundModelId: -1, Terminal: null, Frame: 0 }, "Empty resource selection consumed a full WORD lookup instead of only storing its native low BYTE selector.");
        }
        EventEngine observed = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.RANIME, 2), (Jsm.Opcode.ANIMESYNC, 0), (Jsm.Opcode.POPM_B, 5000)], ushort.MaxValue);
        AdvanceToNativeAnimationIssue(observed, 2, 0x4000);
        try
        {
            observed.Step();
            throw new InvalidOperationException("Empty animation inferred its uninitialized model terminal/completion.");
        }
        catch (EventScriptException) when (observed.LastFault?.Opcode == nameof(Jsm.Opcode.ANIMESYNC))
        {
        }
        JsonElement held = observed.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(held.GetProperty("Position").GetInt32() == observed.LastFault!.Operation && held.GetProperty("Stack")[0].GetInt32() == 77 && !CapturedVariables(observed.Capture()).TryGetProperty("5000", out _), "Unsupported empty animation observation advanced its original PC or consumed an unrelated operand.");
        NativeCertifiedEmptyAnimation(vitals, economy);
        Console.WriteLine("Empty native animation: three nonblocking writers and idle reset, exact known stores/opaque terminal, no metadata lookup, retained empty binding after SETMODEL, independent writer restore and completion-observer refusal passed.");
    }
}
