using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedGuardianBoostRequest(long Token, BattleActionHeaderReference Header, BattlePartyModelOwner Caster, byte Percent);

    private ushort hostedGuardianBoostMappedButtons;
    private HostedPresentationInvocation? hostedGuardianBoostWidgetOwner;
    private HostedGuardianBoostRequest? pendingHostedGuardianBoost;
    private long lastHostedGuardianBoostAcknowledged;

    internal NativeBattleGuardianBoost? HostedGuardianBoost =>
        hostedGuardianBoostWidgetOwner is not null && ReferenceEquals(hostedGuardianBoostWidgetOwner, hostedPresentationInvocation) ? nativeGuardianBoost : null;

    internal HostedGuardianBoostRequest? PendingHostedGuardianBoost => pendingHostedGuardianBoost;



    internal void SetHostedGuardianBoostMappedButtons(ushort mappedButtons)
    {
        hostedGuardianBoostMappedButtons = mappedButtons;
    }

    internal bool PressHostedGuardianBoost()
    {
        return HostedGuardianBoost is not null && PressGuardianBoostWidget();
    }

    private void BindHostedGuardianBoostWidget(ushort action)
    {
        var (invocation, header, _) = RequireHostedGuardianBoostInvocation();
        if (header.Action != action || pendingHostedGuardianBoost is not null || ReferenceEquals(hostedGuardianBoostWidgetOwner, invocation))
        {
            throw new InvalidDataException("The native Boost widget does not belong to a new hosted Guardian Force invocation.");
        }
        hostedGuardianBoostWidgetOwner = invocation;
    }

    private void PublishHostedGuardianBoostRequest(byte percent)
    {
        var (invocation, header, caster) = RequireHostedGuardianBoostInvocation();
        if (!ReferenceEquals(hostedGuardianBoostWidgetOwner, invocation) || nativeGuardianBoost?.ActionId != header.Action ||
            pendingHostedGuardianBoost is not null || invocation.Token <= lastHostedGuardianBoostAcknowledged)
        {
            throw new InvalidOperationException("The hosted Guardian Force invocation cannot publish another Boost request.");
        }
        pendingHostedGuardianBoost = new(invocation.Token, invocation.Header, caster, percent);
    }

    private (HostedPresentationInvocation Invocation, NativeBattleActionHeaderSlot Header, BattlePartyModelOwner Caster) RequireHostedGuardianBoostInvocation()
    {
        if (!HasBattleEntry || hostedPresentationInvocation is not { } invocation || NativeGraphics is not { EntryReady: true } graphics ||
            nativeActionPackets.ActiveHeader != invocation.Header)
        {
            throw new InvalidOperationException("Hosted Boost requires the current active action and its header.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(invocation.Header);
        if (header.Command != 254 || header.Action is < 64 or >= 80 || header.Source is not BattlePartyModelOwner caster || !graphics.ActorModels.Contains(caster))
        {
            throw new InvalidDataException("Hosted Boost requires the active Guardian Force header and its party caster.");
        }
        return (invocation, header, caster);
    }

    private void ValidateHostedGuardianBoostRequest(HostedGuardianBoostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (invocation, _, caster) = RequireHostedGuardianBoostInvocation();
        if (!ReferenceEquals(request, pendingHostedGuardianBoost) || request.Token != invocation.Token ||
            request.Header != invocation.Header || request.Caster != caster || request.Token <= lastHostedGuardianBoostAcknowledged)
        {
            throw new InvalidOperationException("The Boost request no longer belongs to the active hosted Guardian Force invocation.");
        }
    }
}
