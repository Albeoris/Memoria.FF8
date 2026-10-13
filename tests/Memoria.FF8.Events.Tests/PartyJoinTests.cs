using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Numerics;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenHarmlessPartyContactBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        string path = Path.Combine("../fixtures/external-fixture.json");
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(path));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle) ? new WalkmeshPosition(position, triangle) : null
            };
            result.LoadField(139, field.Jsm, field.Messages);
            result.ResolvePartyJoinStep = query =>
            {
                FieldNativeWalkmeshPosition? point = FieldNativeWalkmeshProbe.ResolveFlatStep(field.Walkmesh, query.Triangle, query.FixedX, query.FixedY, query.Radius, query.Direction, result.Scene.LockedTriangles);
                return point is FieldNativeWalkmeshPosition native && native.NativeHeight * 4096 == query.FixedZ ? new WalkmeshPosition(new Vector3(native.FixedX / 4096f, native.FixedY / 4096f, native.NativeHeight), native.Triangle) : null;
            };
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        EventEngine resumed = Create();
        Require(first.Frame == 1844 && !first.Actors[5].CollisionEnabled && !first.Actors[5].PushEnabled && first.Scene.PartyTrace!.Cursor == 1, "Actual north contact checkpoint changed.");
        FieldFixedPosition before = first.Scene.NativePlayerPosition!;
        PlayerAction north = new(PlayerActionKind.Move, Vertical: 1);
        first.Apply(north);
        resumed.Apply(north);
        Require(first.Capture().GetRawText() == resumed.Capture().GetRawText() && first.Frame == 1845 && first.Scene.NativePlayerPosition == before with { Y = before.Y + 126864 } && first.Scene.PartyTrace!.Cursor == 2, "Native THROUGHON/PUSHOFF peer contact changed movement or replay.");

        // Enabled peer collision still requires original blocked steering.
        foreach (string property in new[] { "CollisionEnabled" })
        {
            EventEngine bounded = Create();
            var edited = System.Text.Json.Nodes.JsonNode.Parse(bounded.Capture().GetRawText())!;
            edited["Actors"]![5]![property] = true;
            using var document = System.Text.Json.JsonDocument.Parse(edited.ToJsonString());
            bounded.Restore(document.RootElement);
            var original = bounded.Capture();
            try
            {
                bounded.Apply(north);
                throw new InvalidOperationException($"Native contact {property} was silently bypassed.");
            }
            catch (NotSupportedException)
            {
            }
            Require(original.GetRawText() == bounded.Capture().GetRawText(), $"Unsupported contact {property} mutated the VM.");
        }
        Console.WriteLine("Original1844 north contact: ordinary Move1 passes peer THROUGHON/PUSHOFF with exact replay; enabled collision still stops before any VM mutation.");
    }

    private static void NativeBaseAnimationContract()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 11 };
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.BASEANIME, 6), (Jsm.Opcode.RET, 0));
        engine.LoadField(100, program, []);
        Require(engine.Actors[0].BaseAnimationId == 42 && engine.Actors[0].WalkAnimationId == 42 && engine.Actors[0].RunAnimationId == 42, "Native base animation defaults differ from actor bytes591..593.");
        Require(engine.Actors[0].WalkmeshTriangle == 0 && engine.Actors[0].ShadeLevel == 96, "Fresh native actor zeroed triangle506 or shadow-level609 default differs from the original loader.");
        engine.Step();
        ActorState actor = engine.Actors[0];
        Require(actor.BaseAnimationId == 6 && actor.WalkAnimationId == 7 && actor.RunAnimationId == 8 && actor.NativeBaseAnimationVersion == 1, "BASEANIME lost the native immediate/first-pop/second-pop idle/walk/run order.");
        Require(actor.AnimationId == 0 && !actor.AnimationPlaying, "BASEANIME immediately selected an animation instead of only registering the three IDs.");
        var saved = engine.Capture();
        engine.Restore(saved);
        Require(saved.GetRawText() == engine.Capture().GetRawText(), "Native base animation registration failed exact restoration.");
    }

    private static void GardenJoinBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        JsmProgram source = JsmProgram.Read(field.Jsm);
        Console.WriteLine("JOIN original successors: " + string.Join(", ", Enumerable.Range(63, 16).Select(index => $"{index}:{source.Operations[index].Opcode}/{source.Operations[index].Parameter}")));
        string path = Path.Combine("../fixtures/external-fixture.json");
        GameSession firstSession = new(GameSession.Load(path));
        GameSession secondSession = new(GameSession.Load(path));
        EventEngine Create(GameSession session)
        {
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                CanDisableAdditionalControl = _ => true,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle) ? new WalkmeshPosition(position, triangle) : null
            };
            result.LoadField(field.Id, field.Jsm, field.Messages);
            result.ResolvePartyJoinStep = query =>
            {
                FieldNativeWalkmeshPosition? point = FieldNativeWalkmeshProbe.ResolveFlatStep(field.Walkmesh, query.Triangle, query.FixedX, query.FixedY, query.Radius, query.Direction, result.Scene.LockedTriangles);
                return point is FieldNativeWalkmeshPosition native && native.NativeHeight * 4096 == query.FixedZ ? new WalkmeshPosition(new Vector3(native.FixedX / 4096f, native.FixedY / 4096f, native.NativeHeight), native.Triangle) : null;
            };
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create(firstSession);
        EventEngine resumed = Create(secondSession);
        var initial = first.Capture();
        Require(first.Frame == 1839 && first.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 1, 255 }) && first.Scene.PartyTrace is null, "Corrected original JOIN source has changed.");
        PlayerAction wait = new(PlayerActionKind.Wait);
        first.Apply(wait);
        resumed.Restore(first.Capture());
        Require(first.Capture().GetRawText() == resumed.Capture().GetRawText(), "JOIN first-tick target state did not restore exactly.");
        var join = first.Capture().GetProperty("PartyJoin");
        Require(join.GetProperty("Targets")[0].GetProperty("ObjectId").GetInt32() == 5 && join.GetProperty("Targets")[0].GetProperty("Speed").GetInt32() == 2696, "JOIN targeted a raw actor index or changed native walk speed.");
        Require(first.Actors[5].AnimationId == 1 && first.Actors[5].AnimationFrame == 0 && first.Actors[5].AnimationLast == field.GetAnimationFrameCount(5, 1) - 1, "JOIN failed to reset the complete original walk animation.");
        for (int tick = 0; tick < 8 && first.Scene.PartyTrace is null; tick++)
        {
            first.Apply(wait);
            resumed.Apply(wait);
            Require(first.Capture().GetRawText() == resumed.Capture().GetRawText(), "Restored JOIN fixed movement, animation or trace differs on an ordinary wait.");
            if (tick == 0)
            {
                var target = first.Capture().GetProperty("PartyJoin").GetProperty("Targets")[0];
                Require(target.GetProperty("FixedX").GetInt32() == 121827 && target.GetProperty("FixedY").GetInt32() == -26314858 && first.Actors[5].WalkmeshTriangle == 47 && first.Actors[5].Direction == 255, "JOIN first native fixed step or stale-triangle adjacency differs from independent original proof.");
            }
        }
        Require(first.Scene.NativePartyTraceVersion == 1 && first.Scene.PartyTrace is not null && first.LastFault is null, "JOIN failed to initialize the native follower trace.");
        PartyTraceState trace = first.Scene.PartyTrace!;
        Require(trace.Cursor == 0 && trace.FirstOffset == 15 && trace.SecondOffset == 30 && trace.FirstTrail.Take(32).All(sample => sample is null) && trace.FirstTrail.Skip(32).All(sample => sample is not null) && trace.SecondTrail.All(sample => sample is null), "JOIN invented the native untouched prefix or absent-FF third trail.");
        Require(first.Scene.Party.SequenceEqual(new[] { 0, 5 }) && first.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 1, 255 }), "JOIN changed the original party or compact bindings.");
        Console.WriteLine($"Original JOIN completed at frame{first.Frame}, next {first.NextTrigger.Kind}; fixed companion({first.Actors[5].X},{first.Actors[5].Y}) tri{first.Actors[5].WalkmeshTriangle}, trace first[32..63] known/second opaque; exact mid-JOIN restore passed.");

        // Native motion writes the old cursor, selects animation from the old
        // follower offset, then updates follower position from the advanced cursor.
        PartyTraceSample expectedFollower = trace.FirstTrail[50]!;
        first.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        resumed.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Require(first.Capture().GetRawText() == resumed.Capture().GetRawText(), "Post-JOIN native movement differs after a mid-JOIN restore.");
        Require(first.Scene.NativePlayerPosition == new FieldFixedPosition(25 * 4096 + 126864, -6585 * 4096, 8341 * 4096) && first.Scene.PartyTrace!.Cursor == 1 && first.Actors[0].Direction == 64 && first.Actors[0].AnimationId == 2, "Native player default run speed, preserved fixed proposal, direction or selected run animation differs.");
        Require(first.Actors[5].X == expectedFollower.X && first.Actors[5].Y == expectedFollower.Y && first.Actors[5].WalkmeshTriangle == expectedFollower.Triangle, "Native follower used the pre-advance ring cursor for its position.");
        first.Apply(wait);
        resumed.Apply(wait);
        Require(first.Capture().GetRawText() == resumed.Capture().GetRawText() && first.Scene.PartyTrace!.Cursor == 1 && first.Actors[0].AnimationId == 0, "Stationary native trace advanced the ring or failed to select idle.");
        var beforeBlockedMove = first.Capture();
        first.ResolvePartyJoinStep = _ => null;
        try
        {
            first.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
            throw new InvalidOperationException("Post-JOIN unsupported geometry silently used historical movement.");
        }
        catch (NotSupportedException)
        {
        }
        Require(first.Capture().GetProperty("Actors").GetRawText() == beforeBlockedMove.GetProperty("Actors").GetRawText() && first.Capture().GetProperty("Scene").GetRawText() == beforeBlockedMove.GetProperty("Scene").GetRawText() && first.Frame == beforeBlockedMove.GetProperty("Frame").GetInt64(), "Failed native player geometry partially moved the party or advanced the VM.");

        // A geometry service refusal must retain both companions and the original JOIN PC.
        EventEngine bounded = Create(new GameSession(GameSession.Load(path)));
        bounded.Apply(wait);
        var beforeFailure = bounded.Capture();
        bounded.ResolvePartyJoinStep = _ => null;
        try
        {
            bounded.Apply(wait);
            throw new InvalidOperationException("Unsupported native JOIN geometry was silently approximated.");
        }
        catch (EventScriptException) when (bounded.LastFault?.Opcode == nameof(Jsm.Opcode.JOIN))
        {
        }
        Require(bounded.Capture().GetProperty("Actors").GetRawText() == beforeFailure.GetProperty("Actors").GetRawText() && bounded.Capture().GetProperty("PartyJoin").GetRawText() == beforeFailure.GetProperty("PartyJoin").GetRawText(), "Failed JOIN geometry mutated its actors or fixed companion targets.");
        var frozen = bounded.Capture();
        bounded.Restore(frozen);
        Require(frozen.GetRawText() == bounded.Capture().GetRawText() && bounded.LastFault is not null, "Unsupported JOIN movement lost its exact frozen checkpoint on restore.");
    }
}
