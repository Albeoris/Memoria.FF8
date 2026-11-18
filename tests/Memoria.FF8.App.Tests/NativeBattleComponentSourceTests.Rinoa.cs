using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalRinoaComponent(GameWorld world, EffectPreviewRequest request)
    {
        ushort effect = request.EffectId ?? throw new InvalidDataException("The component requires its selected native effect.");
        if (request.AllyId != 4 || effect is < 86 or > 88)
        {
            throw new InvalidDataException("The component requires its original party caster and supported limit.");
        }
        ushort action = checked((ushort)(effect - 86));
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        if (actor.ModelId != 4 || !world.BattleResources.NativeActions.Any(native => native.Command == 19 && native.ActionId == action && native.MagicId == effect && native.KernelSection == 25))
        {
            throw new InvalidDataException("The component requires its installed original caster and limit metadata.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        byte targetIndex = effect == 88 ? (byte)0 : (byte)3;
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(targetIndex)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(19, 0, action, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[targetIndex];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(target, 0, 1, 0, saved, 0, battle), null);
        process.BeginDiagnosticAction(new(caster, 19, sequence, -1, action, effect, null), [contact]);
        return source;
    }
}
