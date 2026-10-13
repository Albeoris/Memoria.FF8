using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyScrollAndBoundary(string gameDirectory)
    {
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 65551), (Jsm.Opcode.CSCROLLP, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(14), 11);
        EventEngine CreateSynthetic()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { CanSetPartyBackgroundScroll = (slot, actor, duration) => slot == 0 && actor == 0 && duration == 15 };
            engine.LoadField(100, program, []);
            engine.Step();
            engine.Scene.FieldPartyNativeActorIds.AddRange([0, 255, 255]);
            engine.Scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 224, 0, 224, 0, 0, 0, 3, 2);
            return engine;
        }
        EventEngine phase = CreateSynthetic();
        phase.Step();
        BackgroundScrollPosition initial = phase.Scene.BackgroundScrollPositions[0];
        Require(initial.Mode == 2 && initial.Status == 1 && initial.Counter == 1 && initial.Duration == 15 && initial.NativeActorId == 0 && initial.Y == 224 && initial.PreviousY == 224 && initial.TargetY == 224 && phase.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 9, "CSCROLLP lost lowWORD duration, unused target, dense actor, entry phase or immediate continuation.");
        EventEngine independent = CreateSynthetic();
        independent.Restore(phase.Capture());
        for (int elapsed = 1; elapsed <= 15; elapsed++)
        {
            phase.Step();
            independent.Step();
            BackgroundScrollPosition state = phase.Scene.BackgroundScrollPositions[0];
            Require(state.Y == NativeCosineInterpolation.Interpolate(224, 0, 15, elapsed) && state.Counter == Math.Min(elapsed + 1, 15) && state.Status == (elapsed == 15 ? 2 : 1) && JsonElement.DeepEquals(phase.Capture(), independent.Capture()), "Cosine party scroll lost native same-update phase, completion or independent midphase restore.");
        }
        Require(phase.Scene.BackgroundScrollPositions[0].Y == 0 && phase.Scene.BackgroundScrollPositions[0].Counter == 15, "CSCROLLP did not complete after counter0 through15 inclusive.");
        EventEngine refused = CreateSynthetic();
        refused.CanSetPartyBackgroundScroll = (_, _, _) => false;
        try
        {
            refused.Step();
            throw new InvalidOperationException("CSCROLLP ignored a missing projection/renderer certificate.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "CSCROLLP")
        {
        }
        JsonElement refusedFiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(refusedFiber.GetProperty("Position").GetInt32() == 5 && refusedFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65551, 0]) && refused.Scene.BackgroundScrollPositions[0].Mode == 3, "Refused CSCROLLP changed original PC, stack or scroll state.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine CreateActual()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanSetPartyBackgroundScroll = (slot, actor, duration) => slot == 0 && actor == 0 && duration == 15,
                CanSetBackgroundScrollPosition = (layer, x, y) => layer == 0 && x == 0 && y is 0 or 224
            };
            engine.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine actual = CreateActual();
        EventEngine retry = CreateActual();
        bool transient = false;
        actual.CommandExecuted += command =>
        {
            if (command.Name == "CSCROLLP")
            {
                BackgroundScrollPosition pending = actual.Scene.BackgroundScrollPositions[0];
                transient = pending.Mode == 2 && pending.Status == 0 && pending.Duration == 15 && pending.NativeActorId == 0 && pending.Y == 224;
            }
        };
        actual.Step();
        retry.Step();
        BackgroundScrollPosition reached = actual.Scene.BackgroundScrollPositions[0];
        Require(transient && actual.Frame == 8 && reached.Mode == 3 && reached.Status == 2 && reached.Counter == 0 && reached.Duration == 15 && reached.NativeActorId == 0 && reached.Y == 224 && reached.TargetY == 224 && JsonElement.DeepEquals(actual.Capture(), retry.Capture()), $"Actual frame7 ordinary Wait1 failed reached CSCROLLP and later native-order DSCROLL2 overwrite: {reached}, transient {transient}.");
        retry.Restore(actual.Capture());
        Require(JsonElement.DeepEquals(actual.Capture(), retry.Capture()), "Actual CSCROLLP entry phase failed saved VM restore.");
        Console.WriteLine($"Original172 CSCROLLP: ordinary Wait1/frame7→8 sets duration15/dense actor0/mode2; later native-order controller147 DSCROLL2 overwrites mode3/status2/currentY224, retains duration/actor; exact restore. Synthetic cosine completes16 updates with independent midphase restore; next {actual.NextTrigger.Kind}.");
    }
}
