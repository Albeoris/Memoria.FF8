using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private void ObserveTravelForwarding(GameWorld world)
    {
        GameState actual = session.State;
        if (run.ActiveTravelRoute is not { } route || actual.Scene != SceneKind.Field)
        {
            return;
        }
        bool forwardedField = run.ActiveField != world.Events.FieldId && world.Field?.Id == world.Events.FieldId && actual.FieldId != world.Events.FieldId && route.DestinationVisitKey == $"{world.Events.FieldId}:{world.Events.Variables.GetValueOrDefault(256)}";
        if (forwardedField || ForwardedWorldGateway(run, actual))
        {
            run.ActiveTravelForwarded = true;
        }
    }

    private static bool ForwardedWorldGateway(AutoPlayRunState state, GameState actual)
    {
        return actual.FieldLoadContext?.Origin == NativeFieldLoadOrigin.WorldReturn && actual.PreviousFieldId == state.ActiveField && actual.DepartingWorldState is not null && state.ActiveEvent is string eventId && eventId.StartsWith($"{state.ActiveField}:gateway:", StringComparison.Ordinal) && state.ActiveTravelRoute?.DestinationVisitKey == $"world:{state.ActiveStory}";
    }

    internal static bool HasNativeGatewayDestinationContext(AutoPlayRunState state, GameState actual, out int gateway)
    {
        gateway = -1;
        if (actual.Scene != SceneKind.Field || actual.PreviousFieldId != state.ActiveField || state.ActiveTravelRoute is not { Selections.Count: 0 } route || state.ActiveSelections is not { Count: 0 } || route.ConditionSignature != state.ActiveConditionSignature || state.ActiveNavigationKey is not string navigation || !state.NavigationReceipts.TryGetValue(navigation, out AutoPlayNavigationReceipt? receipt) || receipt.FieldId != state.ActiveField || state.ActiveVisit is not string visit || receipt.Key(visit) != navigation || state.ActiveEvent is not string eventId || state.ActiveTargetId is not string target)
        {
            return false;
        }
        string prefix = state.ActiveField.ToString(CultureInfo.InvariantCulture) + ":gateway:";
        return eventId.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(eventId.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out gateway) && gateway >= 0 && target.StartsWith(eventId + ":", StringComparison.Ordinal) && route.TargetId is string recordedTarget && recordedTarget.StartsWith(eventId + ":", StringComparison.Ordinal);
    }

    private bool CanCorrectNativeGatewayDestination()
    {
        return HasNativeGatewayDestinationContext(run, session.State, out int gateway) && getWorld().IsAutoPlayNavigationResourceCurrent(run.NavigationReceipts[run.ActiveNavigationKey!]) && getWorld().VerifyAutoPlayGatewayDestination(run.ActiveField, gateway, session.State.FieldId);
    }

    internal static bool HasNativeStoryArrivalContext(AutoPlayRunState state, GameState actual)
    {
        if (actual.Scene != SceneKind.Field || !state.ObservedControlLoss || actual.PreviousFieldId != state.ActiveField || actual.FieldLoadContext is not { Origin: NativeFieldLoadOrigin.FieldRequest } load ||
            load.FieldId != actual.FieldId || actual.FieldArrival?.FieldId != actual.FieldId || state.ActiveStory is not int entryStory ||
            state.ActiveTravelRoute is not { Selections: not null, TargetId: not null } route || state.ActiveTargetId != route.TargetId || route.ConditionSignature != state.ActiveConditionSignature ||
            state.ActiveNavigationKey is not string navigation || !state.NavigationReceipts.TryGetValue(navigation, out AutoPlayNavigationReceipt? receipt) || receipt.FieldId != state.ActiveField ||
            state.ActiveVisit is not string visit || receipt.Key(visit) != navigation)
        {
            return false;
        }
        int separator = route.DestinationVisitKey.IndexOf(':');
        int actualStory = ReadStoryWord(actual);
        return separator > 0 && int.TryParse(route.DestinationVisitKey.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out int field) && field == actual.FieldId &&
            int.TryParse(route.DestinationVisitKey.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int expectedStory) && actualStory > expectedStory && actualStory > entryStory;
    }

    private bool CanCorrectNativeStoryArrival(GameWorld world)
    {
        return HasNativeStoryArrivalContext(run, session.State) && world.IsAutoPlayNavigationResourceCurrent(run.NavigationReceipts[run.ActiveNavigationKey!]);
    }
}
