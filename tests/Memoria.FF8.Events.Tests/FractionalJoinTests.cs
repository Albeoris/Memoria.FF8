using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenFractionalLeaderJoinBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z)
            };
            result.LoadField(137, field.Jsm, field.Messages);
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
        JsonElement? joined = null;
        first.CommandExecuted += command =>
        {
            if (command.Name == "JOIN" && first.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null)
            {
                joined = first.Capture();
            }
        };
        FieldFixedPosition raw = new(0, -10650608, 34164736);
        Require(first.Frame == 300 && first.Scene.NativePlayerPosition == raw && first.Actors[0].Y == raw.Y / 4096f && first.Actors[5].X == -7 && first.Actors[5].Y == -2826, "Actual fractional elevator JOIN source changed.");
        PlayerAction wait = new(PlayerActionKind.Wait);
        first.Apply(wait);
        JsonElement initialized = first.Capture();
        JsonElement target = initialized.GetProperty("PartyJoin").GetProperty("Targets")[0];
        Require(first.Frame == 301 && target.GetProperty("TargetX").GetInt32() == raw.X && target.GetProperty("TargetY").GetInt32() == raw.Y && target.GetProperty("TargetZ").GetInt32() == raw.Z && target.GetProperty("Speed").GetInt32() == 2696 && first.Actors[0].Y == raw.Y / 4096f && first.Scene.NativePlayerPosition == raw, "JOIN rounded the leader target or used coordinate SAR for its truncating speed choice.");
        resumed.Restore(initialized);
        FieldNativeMovementResult independent = FieldNativeMovementProbe.Resolve(field.Walkmesh, new FieldNativeMovementRequest(44, -7 * 4096, -2826 * 4096, raw.Z, 127, 2696, 48, false, true, 0, true), first.Scene.LockedTriangles);
        Require(independent.Status == FieldNativeMovementStatus.Accepted && independent.FixedX == -27619 && independent.FixedY == -11532182 && independent.FixedZ == raw.Z, "Independent native fractional-target JOIN first proposal differs.");
        int ticks = 0;
        while (first.Capture().GetProperty("PartyJoin").ValueKind != JsonValueKind.Null && ticks++ < 32)
        {
            AdvanceToComponentSplit(first, wait);
            AdvanceToComponentSplit(resumed, wait);
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()) && (first.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null || first.Scene.NativePlayerPosition == raw && first.Actors[0].Y == raw.Y / 4096f), "Fractional leader, companion fixed motion or trace differed after independent mid-JOIN restore.");
            if (ticks == 1)
            {
                target = first.Capture().GetProperty("PartyJoin").GetProperty("Targets")[0];
                Require(target.GetProperty("FixedX").GetInt32() == independent.FixedX && target.GetProperty("FixedY").GetInt32() == independent.FixedY && first.Actors[5].Direction == 127 && first.Actors[5].WalkmeshTriangle == independent.Triangle, "JOIN direction/first fixed proposal did not use separately shifted negative target/current coordinates.");
            }
        }
        Require(joined is JsonElement completion && completion.GetProperty("Scene").GetProperty("NativePlayerPosition").GetProperty("Y").GetInt32() == raw.Y && completion.GetProperty("Actors")[5].GetProperty("X").GetSingle() == -21301 / 4096f && completion.GetProperty("Actors")[5].GetProperty("Y").GetSingle() == -11273498 / 4096f && first.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && first.LastFault is null, "Fractional JOIN did not preserve raw leader/final stopped follower coordinates before the next original SPLIT entry.");
        PartyTraceState trace = first.Scene.PartyTrace!;
        Require(trace.Cursor == 0 && trace.FirstOffset == 15 && trace.SecondOffset == 30 && trace.FirstTrail[63]!.Y == -2600 && trace.FirstTrail[52]!.Y == -2716 && trace.FirstTrail[50]!.Y == -2737 && trace.FirstTrail[49]!.Y == -2752 && trace.FirstTrail[49]!.Animation == 2, "Native JOIN trace reconstructed integer leader coordinates instead of starting from the raw fixed position.");

        // Synthetic malformed-provenance case, not ordinary route evidence.
        EventEngine unknown = Create();
        JsonNode variant = JsonNode.Parse(unknown.Capture().GetRawText())!;
        variant["Scene"]!["NativePlayerPosition"]!["Y"] = raw.Y + 4096;
        using JsonDocument variantDocument = JsonDocument.Parse(variant.ToJsonString());
        unknown.Restore(variantDocument.RootElement);
        JsonElement before = unknown.Capture();
        try
        {
            unknown.Apply(wait);
            throw new InvalidOperationException("JOIN guessed a fractional leader position without matching raw provenance.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "JOIN")
        {
        }
        Require(unknown.Actors[0].Y == raw.Y / 4096f && unknown.Actors[5].X == -7 && unknown.Actors[5].Y == -2826 && unknown.Actors[5].MovementSpeed == before.GetProperty("Actors")[5].GetProperty("MovementSpeed").GetInt32() && unknown.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && unknown.Scene.NativePlayerPosition == new FieldFixedPosition(raw.X, raw.Y + 4096, raw.Z), "Unsupported fractional JOIN changed actor coordinates, companion speed/targets or raw position before refusal.");
        JsonElement frozen = first.Capture();
        EventEngine outOfRange = Create();
        JsonNode rangeVariant = JsonNode.Parse(outOfRange.Capture().GetRawText())!;
        rangeVariant["Scene"]!["NativePlayerPosition"]!["Y"] = int.MaxValue;
        rangeVariant["Actors"]![0]!["Y"] = int.MaxValue / 4096f;
        using JsonDocument rangeDocument = JsonDocument.Parse(rangeVariant.ToJsonString());
        outOfRange.Restore(rangeDocument.RootElement);
        try
        {
            outOfRange.Apply(wait);
            throw new InvalidOperationException("JOIN accepted raw leader coordinates outside the proved native signed field bounds.");
        }
        catch (EventScriptException) when (outOfRange.LastFault?.Opcode == "JOIN")
        {
        }
        Require(outOfRange.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && outOfRange.Scene.NativePlayerPosition!.Y == int.MaxValue && outOfRange.Actors[5].X == -7 && outOfRange.Actors[5].Y == -2826, "Out-of-range raw JOIN validation changed leader provenance or companion targets.");
        first.Restore(frozen);
        Require(JsonElement.DeepEquals(frozen, first.Capture()), "Next original SPLIT entry failed preserved raw-target/wait restore.");
        Console.WriteLine($"Original137 fractional elevator JOIN: Wait1/frame300→301 targetrawY{raw.Y}, walk2696/direction127, firstfixed(-27619,-11532182); completed frame{first.Frame}, next {first.NextTrigger.Kind}; trace52.Y=-2716/50.Y=-2737, independent mid-JOIN restore and unknown-fraction refusal pass.");
    }

    private static void AdvanceToComponentSplit(EventEngine engine, PlayerAction wait)
    {
        try
        {
            engine.Apply(wait);
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == "SPLIT" && engine.LastFault.Operation == 36 && engine.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null)
        {
            // This successor is ordinary component evidence; a live player
            // must independently cross JOIN before SPLIT implementation.
        }
    }
}
