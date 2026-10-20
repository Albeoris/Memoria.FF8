using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDirectScrollAndBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        byte[] commands = DefaultScrollComponentProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, 65760), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        GameSession session = CreateScrollComponentSession(assets);
        EventEngine engine = new(session) { CanSetBackgroundScrollPosition = (_, _, _) => true };
        engine.LoadField(100, commands, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2);
        bool pendingObserved = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "DSCROLL2")
            {
                BackgroundScrollPosition pending = engine.Scene.BackgroundScrollPositions[0];
                Require(pending.Mode == 3 && pending.Status == 0, "DSCROLL2 committed before the native post-VM scroll update.");
                pendingObserved = true;
            }
        };
        engine.Step();
        BackgroundScrollPosition first = engine.Scene.BackgroundScrollPositions[0];
        Require(pendingObserved && first == new BackgroundScrollPosition(0, 224, 0, 224, 0, 0, 0, 3, 2) && !session.State.Variables.ContainsKey(512), "DSCROLL2 lost native lowWORD targets, same-update direct commit or return3 instruction-budget yield.");
        JsonElement saved = engine.Capture();
        GameSession resumedSession = CreateScrollComponentSession(assets);
        EventEngine resumed = new(resumedSession) { CanSetBackgroundScrollPosition = (_, _, _) => true };
        resumed.LoadField(100, commands, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Step();
        resumed.Restore(saved);
        engine.Step();
        resumed.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()) && engine.Scene.BackgroundScrollPositions[0] == new BackgroundScrollPosition(0, 0, 0, 0, 0, 224, 0, 3, 2) && !session.State.Variables.ContainsKey(512), "Direct-scroll restore changed previous current position, completion or the next native yield.");
        engine.Step();
        resumed.Step();
        Require(session.State.Variables[512] == 9 && JsonElement.DeepEquals(engine.Capture(), resumed.Capture()), "DSCROLL2 yielded at the same PC or blocked its ordinary following instruction.");

        EventEngine blocked = new(CreateScrollComponentSession(assets), NativeEventMemory.FromDWords(new Dictionary<int, int>())) { CanSetBackgroundScrollPosition = (_, _, _) => false };
        blocked.LoadField(100, commands, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        blocked.Step();
        try
        {
            blocked.Step();
            throw new InvalidOperationException("Direct scroll ignored its renderer/camera refusal.");
        }
        catch (EventScriptException) when (blocked.LastFault?.Opcode == "DSCROLL2")
        {
        }
        JsonElement blockedFiber = blocked.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Position").GetInt32() == 6);
        Require(blocked.Scene.BackgroundScrollPositions.Count == 0 && blockedFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65760, 65536, 0]), "Direct-scroll refusal consumed operands, advanced PC or invented current scroll state.");

        if (!TryGetScrollContinuationFixture("Events.Tests.DirectScrollTests.1", out string sourcePath))
        {
            Console.WriteLine("Direct scroll component: lowWORDs, issue/publication, return3, restore and atomic refusal passed.");
            return;
        }
        FieldAssets field = assets.LoadField(172);
        EventEngine Create()
        {
            GameSession source = new(GameSession.Load(sourcePath));
            EventEngine result = new(source)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanSetBackgroundScroll = (layer, settings) => layer == 1 && settings == new BackgroundScrollSettings(0, 0, 0, 0),
                CanSetBackgroundScrollPosition = (layer, x, y) => layer == 0 && x == 0 && y is 0 or 224
            };
            result.LoadField(172, field.Jsm, field.Messages, source.State.FieldArrival);
            result.Restore(source.State.EventState!.Value);
            return result;
        }
        EventEngine actual = Create();
        EventEngine retry = Create();
        foreach (EventEngine original in new[] { actual, retry })
        {
            original.Step();
        }
        JsonElement actualSaved = actual.Capture();
        JsonElement actualFiber = actualSaved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 88);
        Require(actual.Frame == 4 && actual.Scene.BackgroundScrollPositions[0] == first && actualFiber.GetProperty("Position").GetInt32() == 890 && actualFiber.GetProperty("Stack").GetArrayLength() == 0 && actualFiber.GetProperty("WaitReason").GetString() == "Frames" && actualFiber.GetProperty("WaitUntil").GetInt64() == 5 && JsonElement.DeepEquals(actualSaved, retry.Capture()), "Actual172 Wait1 did not preserve DSCROLL2 packet, same-frame scroll position or PC-advance/yield.");
        retry.Restore(actualSaved);
        Require(JsonElement.DeepEquals(actualSaved, retry.Capture()), "Original172 completed direct-scroll position failed exact saved restore.");
        Console.WriteLine($"Original172 DSCROLL2: ordinary Wait1/frame3→4 layer0 current(0,224), mode3/status2, PC890 yieldto5; relative finalscreen offsets layer0/actors-224 and layer1zero; exact restored VM, next {actual.NextTrigger.Kind}.");
    }
}
