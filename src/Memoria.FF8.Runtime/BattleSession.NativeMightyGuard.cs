using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMightyGuard(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Mighty Guard requires its selected original effect.");
        NativeBattleMightyGuardResources resources = effect.Binding.MightyGuardResources ?? throw new NotSupportedException("Mighty Guard requires its original module resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Mighty Guard requires its actual retained process memory.");
        NativeBattleMightyGuardFrame frame = new(memory, effect, memory.RequireMightyGuard(effect.Binding.Effect), nativeCrtRandom ?? throw new NotSupportedException("Mighty Guard requires its actual CRT random owner."));
        if (initialize)
        {
            frame = NativeBattleMightyGuardController.Initialize(frame, resources, nativeActionPackets);
            Publish(frame);
            if ((effect.Parameters!.Flags79 & 1) == 0)
            {
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Mighty Guard requires its current camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
            }
            return graphics with { OriginalEffect = frame.Effect };
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Mighty Guard requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Mighty Guard requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Mighty Guard requires its actual view matrix.");
        Func<int, NativeBattleGraphicsModel> readModel = index => graphics.Models[index];
        NativeBattleMightyGuardInputs inputs = new(nativeActionPackets, view, readModel, PoseSource, Execute, AdvanceNodes) { PackedCamera = camera.CapturePackedView1D97778(), CopiedCameraPadding = camera.CanonicalViewPadding };
        frame = NativeBattleMightyGuardController.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Mighty Guard requires its actual posed model source.");

        void Publish(NativeBattleMightyGuardFrame current)
        {
            NativeEffectMemory = current.Memory.WithMightyGuard(current.State);
            nativeCrtRandom = current.Random;
        }

        NativeBattleMightyGuardFrame AdvanceNodes(NativeBattleMightyGuardFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Mighty Guard requires its current ordering table.");
            NativeBattleCuragaNodePresentationStep drawn = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(current.ToGeneric(), address, current.Random, readModel, projection, camera.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = drawn.Ordering } };
            return current.WithGeneric(drawn.Frame) with { Random = drawn.Random };
        }

        NativeBattleMightyGuardFrame Execute(NativeBattleMightyGuardFrame current, NativeBattleMightyGuardOperation operation)
        {
            switch (operation)
            {
                case NativeBattleMightyGuardSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(sound.Sound);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleMightyGuardContact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Mighty Guard contact lost its actual process memory.") };
                case NativeBattleMightyGuardStage fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Mighty Guard requires its actual four stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Mighty Guard requires all four original stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleMightyGuardBindCaster bind:
                    ImmutableArray<byte> dat = resources.CasterDat ?? throw new NotSupportedException("Blue Magic Mighty Guard requires its original caster resource.");
                    NativeBattleGraphicsModel model = graphics.Models[bind.Actor];
                    NativeBattleFileStaging staging = (NativeFileStaging ?? throw new NotSupportedException("Mighty Guard requires its actual staging owner.")).Write(bind.Resource, dat.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Mighty Guard requires its actual CPU resource rows."), model, bind.SavedBank, bind.Resource);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)bind.Actor), bind.Sequence);
                    return current;
                default:
                    throw new NotSupportedException("Mighty Guard requires its original immediate operation owner.");
            }
        }
    }
}
