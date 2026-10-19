using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeStatusMagic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native status entry requires its selected module.");
        NativeBattleStatusMagicResources resources = effect.Binding.StatusMagicResources ?? throw new NotSupportedException("Native status entry requires its original executable resources.");
        NativeBattleStatusMagicFrame frame = NativeBattleStatusMagicController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native status entry requires retained process memory."), effect, resources);
        NativeEffectMemory = frame.Memory;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native status entry requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeStatusMagic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native status callback requires its selected module.");
        NativeBattleStatusMagicResources resources = effect.Binding.StatusMagicResources ?? throw new NotSupportedException("Native status callback requires its executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native status callback requires its published projection.");
        NativeBattleStatusMagicFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native status callback requires its current memory."), effect);
        NativeBattleStatusMagicInputs inputs = new(nativeActionPackets, projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native status packets require their original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native status effect requires its current target pose."), (index, flags) => graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = flags }) }, Execute) { ApplyContact = ApplyContact };
        NativeBattleStatusMagicStep step = NativeBattleStatusMagicController.Advance(frame, resources, inputs);
        NativeEffectMemory = step.Frame.Memory;
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect };

        NativeBattleStatusMagicFrame ApplyContact(NativeBattleStatusMagicFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native status contact lost process memory.") };
        }

        NativeBattleStatusMagicFrame Execute(NativeBattleStatusMagicFrame current, NativeBattleStatusMagicOperation operation)
        {
            if (operation is NativeBattleStatusMagicFade)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 128) };
                return current;
            }
            if (operation is NativeBattleStatusMagicClearFlash)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Petrify flash reset requires its current stage groups.");
                NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = 0 }).ToImmutableArray() };
                return current;
            }
            if (operation is not NativeBattleStatusMagicPackets packets)
            {
                throw new NotSupportedException("Native status operation has no proved implementation.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native status draw requires its current ordering table.");
            int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native status draw requires its executed packet-arena selection.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
