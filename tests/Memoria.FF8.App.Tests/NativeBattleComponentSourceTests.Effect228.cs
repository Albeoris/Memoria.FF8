using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect228Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.EffectId != 228 || request.AllyId != 7)
        {
            throw new InvalidDataException("The component requires its original party caster and selected limit.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        BattleNativeAction action = world.BattleResources.NativeActions.Single(candidate => candidate.Command == 18 && candidate.CharacterId == 7 && candidate.MagicId == 228 && candidate.KernelSection == 18);
        if (actor.ModelId != 7 || action.HitCount == 0)
        {
            throw new InvalidDataException("The component requires its installed original caster and limit metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[3];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        NativeBattleContactPacket[] contacts = Enumerable.Range(0, action.HitCount).Select(_ => contact).ToArray();
        process.BeginDiagnosticAction(new(caster, action.Command, sequence, -1, action.ActionId, action.MagicId, null), [.. contacts]);
        return source;
    }
}
