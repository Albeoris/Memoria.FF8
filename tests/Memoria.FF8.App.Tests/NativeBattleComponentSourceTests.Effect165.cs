using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect165Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.AllyId != 0)
        {
            throw new InvalidDataException("The component requires its original party caster.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        BattleNativeAction action = world.BattleResources.NativeActions.Single(candidate => candidate.Command == 249 && candidate.ActionId == 3);
        if (actor.ModelId != 0 || action.MagicId != 165 || action.KernelSection != 5 || action.KernelIndex != 3 || action.HitCount != 17)
        {
            throw new InvalidDataException("The component requires its installed original finisher and seventeen-contact contract.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster lacks its original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component recipient lacks its original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(249, 0, 3, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[3];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        NativeBattleContactPacket[] contacts = Enumerable.Repeat(contact, action.HitCount).ToArray();
        process.BeginDiagnosticAction(new(caster, action.Command, sequence, -1, checked((ushort)action.ActionId), action.MagicId, null), [.. contacts]);
        return source;
    }
}
