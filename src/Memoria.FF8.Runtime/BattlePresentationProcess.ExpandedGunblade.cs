namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal ushort? HostedTriggerOptions => this.resources.TriggerOptions;

    internal void PressHostedGunblade(long token, BattleModelOwner actor, NativeBattleTriggerWidget widget)
    {
        if (!HasBattleEntry || hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation ||
            invocation.Token != token || NativeGraphics is not { } graphics ||
            actor is not BattlePartyModelOwner || !graphics.ActorModels.Contains(actor) || graphics.PendingTrigger is not null ||
            !ReferenceEquals(graphics.TriggerWidget, widget) || widget.Actor != actor ||
            !CanPressNativeTriggerWidget(widget, this.resources.TriggerOptions))
        {
            throw new InvalidOperationException("Gunblade input requires its active hosted widget.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(invocation.Header);
        bool reached = widget.SourceMagicId is ushort resource
            ? header is { Command: 5 } && header.Effect == resource
            : header.Command is 1 or 12 or 28;
        if (header.Source != actor || !reached)
        {
            throw new InvalidDataException("Gunblade input does not belong to the active hosted header.");
        }
        NativeGraphics = graphics with { TriggerWidget = widget with { PendingTrigger = true } };
    }

    private static bool CanPressNativeTriggerWidget(NativeBattleTriggerWidget? widget, ushort? options)
    {
        return widget is { ClosingPhase: 0, PendingTrigger: false, Input.TriggerTiming: 0 } &&
            (options.HasValue ? (options.Value & 1) == 0 : throw new NotSupportedException("Native trigger input requires its saved options WORD."));
    }
}
