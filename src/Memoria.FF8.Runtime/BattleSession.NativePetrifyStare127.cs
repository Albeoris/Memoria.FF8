using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativePetrifyStare127(NativeBattleGraphicsState graphics) => RunNativePetrifyStare127(graphics, true);
    private NativeBattleGraphicsState AdvanceNativePetrifyStare127(NativeBattleGraphicsState graphics) => RunNativePetrifyStare127(graphics, false);

    private NativeBattleGraphicsState RunNativePetrifyStare127(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Petrify Stare requires its selected original effect.");
        NativeBattlePetrifyStare127Resources resources = effect.Binding.PetrifyStare127Resources ?? throw new NotSupportedException("Petrify Stare requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Petrify Stare requires its actual process memory.");
        NativeBattlePetrifyStare127Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Petrify Stare requires its CRT stream."), memory.PetrifyStare127 ?? throw new NotSupportedException("Petrify Stare requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Petrify Stare requires its current addressed memory.") };
        NativeBattlePetrifyStare127Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], Bone, Anchor, Execute);
        frame = initialize ? NativeBattlePetrifyStare127Controller.Initialize(frame, resources, inputs) : NativeBattlePetrifyStare127Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Petrify Stare requires its current camera.");

        void Publish(NativeBattlePetrifyStare127Frame current)
        {
            NativeEffectMemory = current.Memory with { PetrifyStare127 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Petrify Stare lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattlePetrifyStare127PointStep Bone(NativeBattlePetrifyStare127Frame current, int actor)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Petrify Stare requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, 0xF0, 0);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Petrify Stare bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattlePetrifyStare127AnchorStep Anchor(NativeBattlePetrifyStare127Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Petrify Stare requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Petrify Stare requires the actual actor heading.")).Angle36 });
        }

        NativeBattlePetrifyStare127Frame Execute(NativeBattlePetrifyStare127Frame current, NativeBattlePetrifyStare127Operation operation)
        {
            switch (operation)
            {
                case NativeBattlePetrifyStare127AdvanceAct act:
                    return AdvanceAct(current, act);
                case NativeBattlePetrifyStare127Flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 128) };
                    return current;
                case NativeBattlePetrifyStare127StageClear:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Petrify Stare requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattlePetrifyStare127Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Packed, 0x100, new(sound.Anchor.X, sound.Anchor.Y, sound.Anchor.Z, 0));
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Petrify Stare sound lost its process memory.") };
                case NativeBattlePetrifyStare127Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Petrify Stare contact lost its process memory."), Staging = NativeFileStaging };
                default:
                    throw new NotSupportedException("Petrify Stare operation has no proved implementation.");
            }
        }

        NativeBattlePetrifyStare127Frame AdvanceAct(NativeBattlePetrifyStare127Frame current, NativeBattlePetrifyStare127AdvanceAct operation)
        {
            NativeBattleModulePoolTask task = NativeBattlePetrifyStare127Controller.Read(current, operation.TaskAddress);
            (NativeBattleGfAnimationSource source, int offset, int length) = operation.Act switch
            {
                0 => (resources.FirstAct, 0x24, 0x1C8),
                1 => (resources.SecondAct, 0x1EC, 0x2D0),
                _ => throw new InvalidDataException("Petrify Stare selected an absent ACT workspace.")
            };
            NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(offset, length).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattlePetrifyStare127Act.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Petrify Stare drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = BinaryPrimitives.ReadUInt32LittleEndian(operation.Context16.Read(12, 4));
                NativeBattlePetrifyStare127ActStep drawn = NativeBattlePetrifyStare127Act.Draw(current.Memory, source, node, operation.Context16, Camera().CapturePackedView1D97778(), temporary, Camera().Projection ?? throw new NotSupportedException("Petrify Stare requires its actual projection."), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Petrify Stare requires its original interpolation constants."), morph);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                current = current with { Memory = drawn.Memory, Staging = published };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Petrify Stare requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattlePetrifyStare127Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattlePetrifyStare127Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            return NativeBattlePetrifyStare127Controller.Write(current, operation.TaskAddress, advanced.Workspace.CopyTo(NativeBattlePetrifyStare127Controller.Read(current, operation.TaskAddress), offset, retainUnknown: false));
        }
    }
}
