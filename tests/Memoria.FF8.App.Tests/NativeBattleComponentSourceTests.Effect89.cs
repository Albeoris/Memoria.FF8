using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal static BattleSession PrepareOriginalEffect89Component(GameWorld world, EffectPreviewRequest request)
    {
        if (request.AllyId != 4)
        {
            throw new InvalidDataException("The component requires its original party caster.");
        }
        BattleSession source = world.StartEffectPreview(request);
        BattleCombatant actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeActorIndex == 0);
        BattleNativeAction action = world.BattleResources.NativeActions.Single(candidate => candidate.Command == 19 && candidate.ActionId == 3);
        if (actor.ModelId != 4 || action.MagicId != 89 || action.CharacterId != 4 || action.KernelSection != 25 || action.KernelIndex != 3 || action.HitCount != 8)
        {
            throw new InvalidDataException("The component requires its original caster, kernel action and eight-contact contract.");
        }
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The component requires its initialized presentation process.");
        BattleModelOwner caster = process.FindOriginalActor(NativeBattleModelSlot.Decode(0)) ?? throw new InvalidDataException("The component caster has no original model binding.");
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3)) ?? throw new InvalidDataException("The component recipient has no original model binding.");
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(actor.Id)).PoseSource ?? throw new InvalidDataException("The component caster lacks its original skeleton.");
        if (pose.BoneCount <= 25 || NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(caster), pose).Length != pose.BoneCount)
        {
            throw new InvalidDataException("The component caster lacks its original body anchors.");
        }
        byte sequence = NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, source.ReadDefinition().NativeCommandSequences);
        NativeBattleStatusMirror cached = source.NativePresentationStatusMemory.Rows[3];
        ushort saved = cached.Saved ?? throw new InvalidDataException("The component recipient lacks its current physical saved-status owner.");
        uint battle = cached.Battle ?? throw new InvalidDataException("The component recipient lacks its current physical battle-status owner.");
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, saved, 0, battle), null);
        NativeBattleContactPacket[] contacts = Enumerable.Range(0, action.HitCount).Select(_ => contact).ToArray();
        process.BeginDiagnosticAction(new(caster, action.Command, sequence, -1, checked((ushort)action.ActionId), action.MagicId, null), [.. contacts]);
        return source;
    }
}
