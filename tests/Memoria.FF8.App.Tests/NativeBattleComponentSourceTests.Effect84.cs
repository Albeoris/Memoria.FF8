using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect84Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.AllyId != 4)
        {
            throw new InvalidDataException("The component requires its original party caster.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        if (actor.ModelId != 4 || !world.BattleResources.NativeActions.Any(action => action.Command == 19 && action.ActionId == 4 && action.MagicId == 84))
        {
            throw new InvalidDataException("The component requires its installed original caster and limit metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(19, 0, 4, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[actor.RequireNativeActorIndex()];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component caster lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component caster lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(caster, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 19, sequence, -1, 4, 84, null), [contact]);
        return source;
    }
}
