namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PublishNativeReadyContact(NativeBattleGraphicsState graphics, NativeBattleContactHalf half)
    {
        //493D80 applies this half's cached HP before493840 and only then refreshes47E2D0.
        PublishNativePresentedContact(half);
        if (half.ActorIndex >= 3)
        {
            return graphics;
        }
        BattleCombatant actor = GraphicsActor(graphics, half.ActorIndex);
        ushort saved = half.SavedStatuses4;
        uint battle = half.BattleStatuses8;
        if ((battle & 0x80000000) != 0)
        {
            ushort gfHp = RequireNativePartyPresentedRow(half.ActorIndex).GuardianForceHitPoints ?? throw new NotSupportedException("Native493840 requires the summoned GF's actual cached HP WORD.");
            if (gfHp == 0 || (saved & 0x35) != 0 || (battle & 0x14000) != 0)
            {
                actor.NativeStatus1 &= 0x7FFFFFFF;
                actor.NativeCombatantFlags &= ~0x400u;
            }
            else if ((battle & 9) != 0)
            {
                actor.NativeCombatantFlags |= 0x400;
            }
            else
            {
                actor.NativeCombatantFlags &= ~0x400u;
            }
        }
        NativeBattleReadyInvalidation decision = NativeBattleReadyInvalidations.Evaluate(half.ActorIndex, saved, battle, nativeCommandBanks.CountActor(half.ActorIndex), nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native493840 requires its original event actor B0F."), nativePartyStatusMirrors.Saved[half.ActorIndex], nativePartyStatusMirrors.Battle[half.ActorIndex]);
        if (decision.ClearReadyBusyFlags)
        {
            actor.NativeCombatantFlags &= ~0xCu;
        }
        if (decision.RemoveUiCommands != 0)
        {
            NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native485030 requires its initialized UI command bank.");
            for (int remove = 0; remove < decision.RemoveUiCommands; remove++)
            {
                ui = ui with { Commands = ui.Commands.Enqueue(half.ActorIndex, 18) };
            }
            graphics = graphics with { UiServices = ui };
        }
        NativeBattleStatusMirror previous = actor.NativePresentationStatus;
        nativePartyStatusMirrors = nativePartyStatusMirrors.Observe(half.ActorIndex, previous.Saved ?? throw new NotSupportedException("Native493840 requires its retained B92 WORD before47E2D0."), previous.Battle ?? throw new NotSupportedException("Native493840 requires its retained B1C DWORD before47E2D0."));
        if (decision.RemoveQueuedCommands)
        {
            RemoveNativeQueuedActorCommands(actor);
        }
        if (decision.ResetGauge)
        {
            actor.Atb = 0;
            actor.IsBusy = false;
        }
        if ((battle & 0x10000) != 0)
        {
            //493840 ->486C70 ->48C5C0 removes this allocation, preserving the graphical row until its own service completes.
            actor.NativeStatus1 &= ~0x10000u;
            actor.NativeStatus0 = (ushort)((actor.NativeStatus0 & ~0x20) | 1);
            actor.NativeCombatantFlags = 0;
        }
        RefreshNativePresentationStatus(actor);
        return graphics;
    }
}
