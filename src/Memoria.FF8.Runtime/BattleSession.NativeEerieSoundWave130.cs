using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeEerieSoundWave130(NativeBattleGraphicsState graphics) => RunNativeEerieSoundWave130(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeEerieSoundWave130(NativeBattleGraphicsState graphics) => RunNativeEerieSoundWave130(graphics, false);

    private NativeBattleGraphicsState RunNativeEerieSoundWave130(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Eerie Sound Wave requires its selected original effect.");
        NativeBattleEerieSoundWave130Resources resources = effect.Binding.EerieSoundWave130Resources ?? throw new NotSupportedException("Eerie Sound Wave requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Eerie Sound Wave requires its actual process memory.");
        NativeBattleEerieSoundWave130Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Eerie Sound Wave requires its CRT stream."), memory.EerieSoundWave130 ?? throw new NotSupportedException("Eerie Sound Wave requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Eerie Sound Wave requires its current addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Eerie Sound Wave requires its original interpolation constants.");
        NativeBattleEerieSoundWave130Inputs inputs = new(nativeActionPackets, actor => graphics.Models[actor], () => Camera().CapturePackedView1D97778(), Bone, Anchor, AdvanceAct, Execute, colors);
        frame = initialize ? NativeBattleEerieSoundWave130Controller.Initialize(frame, resources, inputs) : NativeBattleEerieSoundWave130Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Eerie Sound Wave requires its current camera.");

        void Publish(NativeBattleEerieSoundWave130Frame current)
        {
            NativeEffectMemory = current.Memory with { EerieSoundWave130 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Eerie Sound Wave lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEerieSoundWave130PointStep Bone(NativeBattleEerieSoundWave130Frame current, int actor, byte selector, short distance)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Eerie Sound Wave requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], pose, selector, distance);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Eerie Sound Wave bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleEerieSoundWave130AnchorStep Anchor(NativeBattleEerieSoundWave130Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Eerie Sound Wave requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Eerie Sound Wave requires the actual actor heading.")).Angle36 });
        }

        NativeBattleEerieSoundWave130Frame Execute(NativeBattleEerieSoundWave130Frame current, NativeBattleEerieSoundWave130Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEerieSoundWave130Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Eerie Sound Wave sound lost its process memory.") };
                case NativeBattleEerieSoundWave130Contact contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Eerie Sound Wave contact lost its process memory."), Staging = NativeFileStaging };
                default:
                    throw new NotSupportedException("Eerie Sound Wave operation has no proved implementation.");
            }
        }

        NativeBattleEerieSoundWave130ActAdvanceStep AdvanceAct(NativeBattleEerieSoundWave130Frame current, uint taskAddress, byte kind, NativeBattleEffectArena context)
        {
            NativeBattleModulePoolTask task = NativeBattleEerieSoundWave130Controller.Read(current, taskAddress);
            (NativeBattleGfAnimationSource source, int offset, int length) = kind switch
            {
                0 => (resources.MainAct, 0x20, 0x4AC),
                1 => (resources.TravelAct, 0x1C, 0x58),
                2 => (resources.ImpactAct, 0x18, 0xA8),
                _ => throw new InvalidDataException("Eerie Sound Wave selected an absent ACT workspace.")
            };
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, length);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (next, node) =>
            {
                if (!NativeBattleEerieSoundWave130Act.Visible(node))
                {
                    return next;
                }
                NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Eerie Sound Wave drawing requires its addressed temporary memory."), 88);
                NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                uint morph = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(36, 4));
                NativeBattleEerieSoundWave130ActStep drawn = NativeBattleEerieSoundWave130Act.Draw(current.Memory, source, node, context, temporary, Camera().Projection ?? throw new NotSupportedException("Eerie Sound Wave requires its actual projection."), colors, morph);
                NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88);
                current = current with { Memory = drawn.Memory, Staging = published };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Eerie Sound Wave requires its current ordering table.");
                uint cursor = current.State.ReadDword(NativeBattleEerieSoundWave130Profile.CursorGlobal);
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEerieSoundWave130Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
                return next;
            });
            current = NativeBattleEerieSoundWave130Controller.Write(current, taskAddress, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleEerieSoundWave130Controller.Read(current, taskAddress), offset, advanced.Workspace));
            return new(current, advanced.Remaining);
        }
    }
}
