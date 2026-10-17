using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record AutoPlayContactSource(int Version, string VisitKey, string NavigationKey, int FieldId, int Story, string ConditionSignature, string? AvailabilityFingerprint, AutoPlayTarget Target, FieldTeleportPosition PlayerPosition, int PushRadius, bool MapJumpDisabled, string GatewaysFingerprint);
internal sealed record AutoPlayContactInterruption(int Version, AutoPlayContactSource Source, int AdmittedGatewayIndex, FieldTransition Destination, WorldTransition? WorldPacket = null);

internal sealed partial class AutoPlayController
{
    private AutoPlayContactSource CaptureContactSource(GameWorld world, string visit, string navigation, AutoPlayTarget target)
    {
        if (!world.HasCurrentFieldAllocation || world.Field is null || world.Events.PlayerActor is not ActorState player)
        {
            throw new NotSupportedException("An automatic contact attempt requires its authentic current field source before placement.");
        }
        if (world.Field.Id != world.Events.FieldId || visit != $"{world.Events.FieldId}:{world.Events.Variables.GetValueOrDefault(256)}" || world.CaptureAutoPlayNavigationReceipt().Key(visit) != navigation)
        {
            throw new NotSupportedException("An automatic contact source must match its actual field, story and navigation component.");
        }
        string signature = world.ReadAutoPlayConditions(target.CoverageId, target.ObjectId).Fingerprint;
        string? availability = run.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == visit)?.Events.SingleOrDefault(item => item.Event.Id == target.CoverageId)?.AvailabilityFingerprint;
        string gateways = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(world.Events.InfGateways.Select(gateway => new { Start = new { gateway.Start.X, gateway.Start.Y, gateway.Start.Z }, End = new { gateway.End.X, gateway.End.Y, gateway.End.Z }, gateway.Destination, gateway.NativeIndex, gateway.NativeRecord }), BeforeSnapshotJsonOptions)));
        return new(1, visit, navigation, world.Events.FieldId, world.Events.Variables.GetValueOrDefault(256), signature, availability, target, new(player.X, player.Y, player.Z, player.WalkmeshTriangle, (int)player.Direction), player.PushRadius, world.Events.Scene.MapJumpDisabled, gateways);
    }

    private IReadOnlyList<AutoPlayTarget> FilterInterruptedContactTargets(GameWorld world, string visit, string navigation, IReadOnlyList<AutoPlayTarget> targets)
    {
        if (run.ContactInterruptions.Count == 0)
        {
            return targets;
        }
        if (run.ContactInterruptions.Any(attempt => attempt.Version != 1 || attempt.Source.Version != 1))
        {
            throw new InvalidDataException("Unsupported automatic contact interruption receipt.");
        }
        return targets.Where(target => target.Kind == AutoPlayTargetKind.Talk || !run.ContactInterruptions.Any(attempt => attempt.Source.NavigationKey == navigation && attempt.Source.Target == target) || !IsContactInterrupted(CaptureContactSource(world, visit, navigation, target), run.ContactInterruptions)).ToArray();
    }

    internal static bool IsContactInterrupted(AutoPlayContactSource source, IReadOnlyList<AutoPlayContactInterruption> attempts)
    {
        AutoPlayContactSource context = ContactGeometryContext(source);
        return attempts.Any(attempt => attempt.Version == 1 && ContactGeometryContext(attempt.Source) == context);
    }

    private static AutoPlayContactSource ContactGeometryContext(AutoPlayContactSource source)
    {
        AutoPlayTarget target = source.Target;
        return target.ApproachX is float x && target.ApproachY is float y ? source with { PlayerPosition = new(x, y, target.ApproachZ ?? target.Z, target.ApproachTriangle ?? target.Triangle, target.Direction) } : source;
    }

    internal static bool IsPlannedGateway(AutoPlayTarget target, int admittedGatewayIndex)
    {
        if (target.Kind != AutoPlayTargetKind.Gateway)
        {
            return false;
        }
        string[] parts = target.CoverageId.Split(':');
        if (parts.Length != 3 || parts[1] != "gateway" || !int.TryParse(parts[2], out int planned) || planned < 0)
        {
            throw new InvalidDataException("An automatic gateway target lacks its original gateway list index.");
        }
        return planned == admittedGatewayIndex;
    }

    private void RecordContactInterruption(GameWorld world, AutoPlayContactSource source, int gatewayIndex)
    {
        if (world.Events.FieldId != source.FieldId || (uint)gatewayIndex >= world.Events.InfGateways.Count || world.Events.NextTrigger.Kind is not (PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap))
        {
            throw new NotSupportedException("The interrupted automatic contact lacks its actual source gateway admission and departure.");
        }
        InfGateway gateway = world.Events.InfGateways[gatewayIndex];
        WorldTransition? worldPacket = world.Events.Scene.WorldTransitionRequest?.Transition;
        AutoPlayContactInterruption attempt = new(1, source, gatewayIndex, gateway.Destination, worldPacket);
        if (!run.ContactInterruptions.Any(known => ContactGeometryContext(known.Source) == ContactGeometryContext(source) && known.AdmittedGatewayIndex == gatewayIndex && known.Destination == attempt.Destination && known.WorldPacket == worldPacket))
        {
            run.ContactInterruptions.Add(attempt);
        }
        ClearNativeUnavailableInteraction();
    }
}
