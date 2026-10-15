using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAnimationLoopYieldContract()
    {
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.RANIMELOOP, 65537), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RANIMELOOP, 1), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.SetWorldControls(NativeWorldControlState.CreateNewGame());
            session.SetEventRandom(session.EventRandom!.ClearSavedStateForNewGame());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (model, animation) => model == 2 && animation == 1 ? 4 : throw new InvalidOperationException($"RANIMELOOP did not select the signed low-WORD model animation: model{model}, animation{animation}.")
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        JsonElement Fiber(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
        bool HasVariable(EventEngine engine, string address) => engine.Capture().GetProperty("Variables").TryGetProperty(address, out _);

        EventEngine live = Create();
        int issued = 0;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.RANIMELOOP))
            {
                issued++;
                Require(command.Arguments.Count == 0 && live.Actors[0].AnimationId == 1 && live.Actors[0].AnimationFrame == 0, "RANIMELOOP popped stack operands, lost its inline WORD or failed to reset the selected full animation.");
            }
        };
        live.Step();
        live.Step();
        ActorState actor = live.Actors[0];
        int firstLoop = JsmProgram.Read(content).Operations.First(operation => operation.Opcode == Jsm.Opcode.RANIMELOOP).Index;
        JsonElement held = Fiber(live);
        Require(issued == 1 && held.GetProperty("Position").GetInt32() == firstLoop + 1 && held.GetProperty("WaitReason").GetString() == "Frames" && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([77]) && !HasVariable(live, "5000"), "RANIMELOOP did not advance its PC and end this actor's VM budget while preserving its unrelated stack value.");
        Require(actor.AnimationId == 1 && actor.AnimationFirst == 0 && actor.AnimationLast == 3 && actor.AnimationFrame == 0 && actor.AnimationLoop && !actor.AnimationKeep && actor.AnimationPlaying, "RANIMELOOP did not select the complete resource animation with native zero starts and persistent loop mode.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        live.Step();
        restored.Step();
        Require(issued == 1 && actor.AnimationFrame == 1 && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77 && !HasVariable(live, "5001") && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "RANIMELOOP repeated the issue, waited for animation completion, lost its original operand or diverged across a yielded-PC restore.");
        live.Step();
        restored.Step();
        Require(issued == 2 && actor.AnimationId == 1 && actor.AnimationFrame == 0 && !HasVariable(live, "5001") && Fiber(live).GetProperty("Stack").GetArrayLength() == 0 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Repeated RANIMELOOP of the current animation skipped its unconditional restart or failed to yield before its successor.");
        EventEngine restartedCopy = Create();
        restartedCopy.Restore(live.Capture());
        live.Step();
        restored.Step();
        restartedCopy.Step();
        Require(actor.AnimationFrame == 1 && live.Capture().GetProperty("Variables").GetProperty("5001").GetInt32() == 88 && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), restartedCopy.Capture()), "Second RANIMELOOP did not resume its successor in the next callback or restore its restart independently.");
        foreach (int expectedFrame in new[] { 2, 3, 0, 1 })
        {
            live.Step();
            restored.Step();
            Require(actor.AnimationFrame == expectedFrame && actor.AnimationId == 1 && actor.AnimationLoop && actor.AnimationPlaying && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.Direction == 0 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Full-resource animation loop failed its terminal wrap, changed physical pose or diverged after restore.");
        }
        Require(issued == 2, "RANIMELOOP was reissued by animation looping or a completed VM turn.");
        Console.WriteLine("RANIMELOOP: signed inline WORD, zero stack consumption, full-resource loop range, unconditional same-ID restart, return3 PC/yield, next-callback successor, loop wrap and independent yielded/restarted restore passed.");
    }
}
