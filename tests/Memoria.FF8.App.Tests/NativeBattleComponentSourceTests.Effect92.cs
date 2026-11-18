using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect92Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.AllyId != 4)
        {
            throw new InvalidDataException("The component requires its original party recipient.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = Assert.Single(source.Combatants, candidate => candidate.IsPlayer && candidate.IsActive);
        if (actor.ModelId != 4 || actor.NativeActorIndex != 0 || !world.BattleResources.NativeActions.Any(action => action.Command == 240 && action.ActionId == 14 && action.MagicId == 92 && action.KernelSection == 9 && action.TargetFlags == 0x19 && action.HitCount == 1))
        {
            throw new InvalidDataException("The component requires its installed original support action and recipient.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(240, 0, 14, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[0];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(recipient, 240, sequence, -1, 14, 92, null), [contact]);
        return source;
    }
}
