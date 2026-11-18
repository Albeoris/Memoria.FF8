using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect112Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.AllyId != 1)
        {
            throw new InvalidDataException("The component requires its original party caster.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        if (actor.ModelId != 1 || !world.BattleResources.NativeActions.Any(action => action.Command == 239 && action.ActionId == 7 && action.MagicId == 112 && action.KernelSection == 22 && action.HitCount == 1))
        {
            throw new InvalidDataException("The component requires its installed original caster and finisher metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(239, 0, 7, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[3];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 239, sequence, -1, 7, 112, null), [contact]);
        return source;
    }
}
