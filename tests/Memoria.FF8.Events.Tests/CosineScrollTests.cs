using System.Text.Json;
using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCosineScrollAndSyncBoundary(string gameDirectory)
    {
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 224), (Jsm.Opcode.DSCROLL2, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 65576), (Jsm.Opcode.CSCROLL2, 0), (Jsm.Opcode.SCROLLSYNC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        EventEngine CreateSynthetic(bool certificate = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables) { CanSetBackgroundScrollPosition = (_, _, _) => true, CanSetCosineBackgroundScroll = (_, _, _, _) => certificate };
            engine.LoadField(100, program, []);
            engine.Step();
            return engine;
        }
        EventEngine engine = CreateSynthetic();
        engine.Step();
        BackgroundScrollPosition start = engine.Scene.BackgroundScrollPositions[0];
        Require(start.Mode == 5 && start.Duration == 40 && start.Counter == 1 && start.Status == 1 && start.Y == 224 && start.TargetY == 0 && start.PreviousY == 224, "CSCROLL2 lost its four original lowWORDs or first counter0 update.");
        JsonElement sync = engine.Capture().GetProperty("Fibers")[0];
        Require(sync.GetProperty("Position").GetInt32() == 10 && sync.GetProperty("WaitReason").GetString() == "BackgroundScroll", "Pending SCROLLSYNC advanced its original PC.");
        EventEngine independent = CreateSynthetic();
        independent.Restore(engine.Capture());
        for (int elapsed = 1; elapsed <= 40; elapsed++)
        {
            engine.Step();
            independent.Step();
            BackgroundScrollPosition state = engine.Scene.BackgroundScrollPositions[0];
            Require(state.Y == NativeCosineInterpolation.Interpolate(224, 0, 40, elapsed) && state.Counter == Math.Min(elapsed + 1, 40) && state.Status == (elapsed == 40 ? 2 : 1) && !engine.Capture().GetProperty("Variables").TryGetProperty("512", out _) && JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Cosine scroll/sync changed phase, resumed VM, heldPC or earliest continuation.");
        }
        engine.Step();
        independent.Step();
        Require(engine.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 9 && JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "SCROLLSYNC did not continue immediately on the first VM update after post-VM completion.");
        EventEngine declined = CreateSynthetic(false);
        try
        {
            declined.Step();
            throw new InvalidOperationException("CSCROLL2 ignored its certificate refusal.");
        }
        catch (EventScriptException) when (declined.LastFault?.Opcode == "CSCROLL2")
        {
        }
        JsonElement refused = declined.Capture().GetProperty("Fibers")[0];
        Require(refused.GetProperty("Position").GetInt32() == 9 && refused.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65576, -65536, 65536, 0]) && declined.Scene.BackgroundScrollPositions[0].Mode == 3, "Refused cosine scroll mutated the instruction, operands or direct-scroll state.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine CreateActual()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanSetCosineBackgroundScroll = (layer, x, y, duration) => layer == 0 && x == 0 && y == 0 && duration == 40
            };
            result.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, result.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            result.ResolvePartyJoinStep = query =>
            {
                FieldNativeWalkmeshPosition? point = FieldNativeWalkmeshProbe.ResolveFlatStep(field.Walkmesh, query.Triangle, query.FixedX, query.FixedY, query.Radius, query.Direction, result.Scene.LockedTriangles);
                return point is FieldNativeWalkmeshPosition resolved && resolved.NativeHeight * 4096 == query.FixedZ ? new WalkmeshPosition(new Vector3(resolved.FixedX / 4096f, resolved.FixedY / 4096f, resolved.NativeHeight), resolved.Triangle) : null;
            };
            result.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine actual = CreateActual();
        actual.Step();
        BackgroundScrollPosition reached = actual.Scene.BackgroundScrollPositions[0];
        Require(actual.Frame == 9 && reached.Mode == 5 && reached.Duration == 40 && reached.NativeActorId == 0 && reached.Counter == 1 && reached.Status == 1 && reached.Y == 224 && reached.TargetY == 0, "Actual frame8 ordinary Wait1 changed reached mode5 packet, retained actor or native entry phase.");
        EventEngine replay = CreateActual();
        replay.Restore(actual.Capture());
        int boundaryFrame = 0;
        for (int frame = 10; frame <= 50; frame++)
        {
            foreach (EventEngine next in new[] { actual, replay })
            {
                try
                {
                    next.Step();
                }
                catch (EventScriptException) when (next.LastFault?.Opcode == "JOIN" && next.LastFault.Operation == 62)
                {
                    boundaryFrame = frame;
                }
            }
            Require(JsonElement.DeepEquals(actual.Capture(), replay.Capture()), "Actual restored cosine phase changed during ordinary replay to the next original boundary.");
            if (boundaryFrame != 0)
            {
                break;
            }
        }
        Require(boundaryFrame != 0 && actual.Frame == boundaryFrame && actual.Scene.BackgroundScrollPositions[0].Mode == 5, "Actual ordinary replay no longer preserves its next bounded original JOIN refusal.");
        Console.WriteLine($"Original172 CSCROLL2: Wait1/frame8→9 mode5/target0/duration40/retained actor0/counter1; exact midcurve restore. Ordinary Wait component stops JOIN{boundaryFrame}/0/7/op62 before scroll update. Synthetic SCROLLSYNC holds PC through41 cosine updates and continues on the next VM tick.");
    }
}
