using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBackgroundScrollAndGuard(string gameDirectory)
    {
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.PSHN_L, -65535), (Jsm.Opcode.PSHN_L, 65535), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.SCROLLMODE2, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { CanSetBackgroundScroll = (_, _) => true };
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.Scene.BackgroundScrollLayers[1] == new BackgroundScrollSettings(1, 1, -1, 0) && session.State.Variables[512] == 9, "SCROLLMODE2 lost four low signedWORDs, first full layer operand or immediate continuation.");
        JsonElement saved = engine.Capture();
        EventEngine restored = new(new GameSession(new GameState()), new Dictionary<int, int>()) { CanSetBackgroundScroll = (_, _) => true };
        restored.LoadField(100, program, []);
        restored.Restore(saved);
        Require(JsonElement.DeepEquals(saved, restored.Capture()), "Background scroll settings failed independent typed Scene restore.");

        EventEngine blocked = new(new GameSession(new GameState()), new Dictionary<int, int>()) { CanSetBackgroundScroll = (_, _) => false };
        blocked.LoadField(100, program, []);
        try
        {
            blocked.Step();
            throw new InvalidOperationException("SCROLLMODE2 ignored an unsupported renderer/camera state.");
        }
        catch (EventScriptException) when (blocked.LastFault?.Opcode == "SCROLLMODE2")
        {
        }
        JsonElement blockedFiber = blocked.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Position").GetInt32() == 6);
        Require(blocked.Scene.BackgroundScrollLayers.Count == 0 && blockedFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([-65536, 65535, -65535, 65537, 1]), "Background scroll refusal changed original operands, PC or settings.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        FieldInformation information = field.Information ?? throw new InvalidOperationException("Selected172 has no original INF metadata.");
        FieldBounds camera = information.CameraBounds[0];
        FieldBounds screen = information.ScreenBounds[0];
        Require(camera == new FieldBounds(-112, 112, 160, -160) && screen == new FieldBounds(0, 224, 320, 0), "Selected172 INF no longer has its proved native camera/screen bounds.");
        int halfWidth = (screen.Right - screen.Left) / 2;
        int halfHeight = (screen.Bottom - screen.Top) / 2;
        Require(camera.Right - halfWidth == 0 && camera.Left + halfWidth == 0 && camera.Bottom - halfHeight == 0 && camera.Top + halfHeight == 0, "Selected172 native475D00 does not force both camera-centre components to zero.");
        EventEngine Create()
        {
            GameSession source = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(source, source.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanSetBackgroundScroll = (layer, settings) => layer == 1 && settings == new BackgroundScrollSettings(0, 0, 0, 0)
            };
            result.LoadField(172, field.Jsm, field.Messages, source.State.FieldArrival);
            result.Restore(source.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        EventEngine retry = Create();
        foreach (EventEngine actual in new[] { first, retry })
        {
            try
            {
                actual.Step();
            }
            catch (EventScriptException) when (actual.LastFault?.Opcode != "SCROLLMODE2")
            {
            }
        }
        Require(first.Frame == 3 && first.Scene.BackgroundScrollLayers[1] == new BackgroundScrollSettings(0, 0, 0, 0) && JsonElement.DeepEquals(first.Capture(), retry.Capture()), "Actual172 Wait1/frame2 did not preserve reached layer1 settings and independent replay.");
        JsonElement completed = first.Capture();
        retry.Restore(completed);
        Require(JsonElement.DeepEquals(completed, retry.Capture()), "Reached native scroll settings/following boundary failed exact restore.");
        Console.WriteLine($"Original172 SCROLLMODE2: Wait1/frame2→3 consumes layer1+fourzeroWORDs; INF475D00 forces centre(0,0), exact typed restore; next {first.NextTrigger.Kind}, fault {first.LastFault?.Opcode}/{first.LastFault?.Operation}.");
    }
}
