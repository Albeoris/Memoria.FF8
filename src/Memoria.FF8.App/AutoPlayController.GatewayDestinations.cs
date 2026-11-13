using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class AutoPlayController
{
    private void ObserveTravelForwarding(GameWorld world)
    {
        if (run.ActiveTravelRoute is { } route && run.ActiveField != world.Events.FieldId && world.Field?.Id == world.Events.FieldId && session.State.Scene == SceneKind.Field && session.State.FieldId != world.Events.FieldId && route.DestinationVisitKey == $"{world.Events.FieldId}:{world.Events.Variables.GetValueOrDefault(256)}")
        {
            run.ActiveTravelForwarded = true;
        }
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
}
