using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffectArena ReadNativeEffect312Physical(NativeBattleGraphicsState graphics, NativeBattleEffect312Frame frame, uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect312 physical read requires a nonnegative length.");
        }
        if (address >= 0x1D972C0 && (ulong)address + (uint)length <= 0x1D972C0ul + (uint)graphics.Models.Length * 156)
        {
            int actor = checked((int)((address - 0x1D972C0) / 156));
            int offset = checked((int)((address - 0x1D972C0) % 156));
            if (length > 156 - offset)
            {
                throw new InvalidDataException("Effect312 physical read crosses a current model row.");
            }
            return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.Models[actor]), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        if (address >= 0x1D99A78 && (ulong)address + (uint)length <= 0x1D99A80)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect312 requires its current physical action parameters.");
            NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, [parameters.Actor78, parameters.Flags79]).Write(2, BitConverter.GetBytes(parameters.TargetMask7A)).Write(4, BitConverter.GetBytes(checked(0x1D280C4u + (uint)parameters.HeaderIndex7C * 20)));
            return NativeBattleEffectNodeContextMemory.Copy(bytes, checked((int)(address - 0x1D99A78)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        if (address >= 0x1D280C4 && address < 0x1D28344)
        {
            uint relative = address - 0x1D280C4;
            int offset = checked((int)(relative % 20));
            if (length > 20 - offset)
            {
                throw new InvalidDataException("Effect312 physical read crosses a current action header.");
            }
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[checked((int)(relative / 20))];
            return NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(offset, length).Select(header.ReadByte).ToArray());
        }
        if (address >= 0x1D28344 && (ulong)address + (uint)length <= 0x1D28344ul + NativeBattleActionPacketsState.ContactCapacity * 24)
        {
            uint relative = address - 0x1D28344;
            int offset = checked((int)(relative % 24));
            if (length > 24 - offset)
            {
                throw new InvalidDataException("Effect312 physical read crosses a current contact row.");
            }
            byte[] contact = nativeActionPackets.ReadContact(checked((int)(relative / 24))).Encode();
            return NativeBattleEffectArena.Clear().Write(0, contact.AsSpan(offset, length));
        }
        return ReadNativeEffect312Backing(frame, address, length);
    }

    private NativeBattlePackedMatrix32 ReadNativeEffect312Bone(NativeBattleGraphicsState graphics, NativeBattleEffect312Frame frame, uint modelAddress, int selector)
    {
        int actor = NativeEffect301Actor(graphics, modelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect312 requires its current addressed skeleton memory.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect312 requires its current physical skeleton.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        int bone = selector;
        if (selector >= 240)
        {
            int offset = selector - 236;
            if ((uint)offset >= 16)
            {
                throw new InvalidDataException("Effect312 bone selector leaves its skeleton aliases.");
            }
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Effect312 requires its current skeleton alias fields.");
            bone = offset == 4 ? sequence.Geometry4 ?? source.Skeleton[offset] : offset == 5 ? sequence.Geometry5 ?? source.Skeleton[offset] : source.Skeleton[offset];
            if ((bone & 0x80) != 0)
            {
                bone &= 0x7F;
            }
        }
        if ((uint)bone >= matrices.Length)
        {
            throw new InvalidDataException("Effect312 bone selection leaves its current physical rig.");
        }
        NativeBattleResourcePointer skeleton = ReadNativeEffect312SkeletonPointer(graphics, frame, actor);
        uint address = checked((skeleton.Cpu?.PhysicalAddress ?? skeleton.StagingAddress ?? throw new NotSupportedException("Effect312 requires its physical skeleton pointer.")) + 32 + (uint)bone * 48 + 18);
        NativeBattleEffectArena padding = ReadNativeEffect312Backing(frame, address, 2);
        (byte[] bytes, bool[] known) = padding.ReadSnapshot(0, 2);
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        return matrix with { Bytes = matrix.Bytes.SetItem(18, bytes[0]).SetItem(19, bytes[1]), Known = matrix.Known.SetItem(18, known[0]).SetItem(19, known[1]) };
    }

    private NativeBattleResourcePointer ReadNativeEffect312SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect312Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? (frame.Memory.Arena ?? throw new NotSupportedException("Effect312 requires its current resource arena.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect312 requires its current skeleton directory.")).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect312 requires its actual skeleton directory pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect312 requires its current CPU resources.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect312 requires its current physical resource row.")].Require(12), null);
    }

    private NativeBattleEffectArena ReadNativeEffect312Backing(NativeBattleEffect312Frame frame, uint address, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        for (int index = 0; index < 3; index++)
        {
            uint first = 0x196B018u + (uint)index * 0x10000;
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + 0x10000)
            {
                NativeBattleModelCpuBank bank = (NativeModelCpuResources ?? throw new NotSupportedException("Effect312 requires its current CPU skeleton bank.")).Cpu.Banks[index];
                int relative = checked((int)(address - first));
                foreach (NativeBattleEffectMemoryRegion region in bank.KnownRanges)
                {
                    int begin = Math.Max(relative, region.Offset);
                    int end = Math.Min(relative + length, region.Offset + region.Data.Length);
                    if (begin < end)
                    {
                        result = result.Write(begin - relative, region.Data.AsSpan().Slice(begin - region.Offset, end - begin));
                    }
                }
                return result;
            }
        }
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && (ulong)address + (uint)length <= (ulong)pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length))
            {
                return NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), result, 0, length);
            }
        }
        if (IsNativeEffectArenaAddress(address))
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect312 requires its current skeleton arena.");
            return NativeBattleEffectNodeContextMemory.Copy(arena, NativeBattleWaterExecutionFields.ArenaOffset(address, length), result, 0, length);
        }
        (byte[] bytes, bool[] known) = (frame.Staging ?? throw new NotSupportedException("Effect312 requires its current physical shared memory.")).ReadSnapshot(address, length);
        for (int index = 0; index < length; index++)
        {
            if (known[index])
            {
                result = result.Write(index, bytes.AsSpan(index, 1));
            }
        }
        return result;
    }

    private static byte ReadNativeEffect312SequenceByte(NativeBattleGraphicsState graphics, uint modelAddress)
    {
        NativeBattleGraphicsModel model = graphics.Models[NativeEffect301Actor(graphics, modelAddress)];
        _ = model.SequenceState ?? throw new NotSupportedException("Effect312 requires its current resource-header owner.");
        // NativeBattleSequenceSelections and SequenceMachine publish model.Sequence as resource-header BYTE0, addressed by model+74.
        return model.Sequence ?? throw new NotSupportedException("Effect312 requires its actual resource-header sequence BYTE0.");
    }

    private NativeBattleEffect312PointStep ReadNativeEffect312Anchor(NativeBattleGraphicsState graphics, NativeBattleEffect312Frame frame, uint modelAddress, NativeBattleEffectArena retained)
    {
        int actor = NativeEffect301Actor(graphics, modelAddress);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect312 requires its current anchor transform.");
        if ((model.Flags0 & 2) == 0)
        {
            return new(frame, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect312 requires its current anchor resource owner.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect312 requires its current anchor rig.");
        NativeBattleVector first = Observe(0xF0);
        NativeBattleVector second = Observe(0xF1);
        return new(frame, retained.Write(0, BitConverter.GetBytes(unchecked((short)((first.X + second.X) / 2)))).Write(2, BitConverter.GetBytes(unchecked((short)((first.Y + second.Y) / 2)))).Write(4, BitConverter.GetBytes(unchecked((short)((first.Z + second.Z) / 2)))).Write(6, BitConverter.GetBytes(transform.Angle36)));

        NativeBattleVector Observe(byte selector)
        {
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(frame.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0));
            frame = frame with { Memory = observed, Staging = NativeFileStaging };
            return new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
        }
    }

    private static NativeBattleGraphicsState ApplyNativeEffect312ModelBytes(NativeBattleGraphicsState graphics, uint modelAddress, int offset, NativeBattleEffectArena data, int length)
    {
        int actor = NativeEffect301Actor(graphics, modelAddress);
        NativeBattleGraphicsModel updated = ApplyNativeEffect301ModelBytes(graphics.Models[actor], offset, data, length);
        return graphics with { Models = graphics.Models.SetItem(actor, updated) };
    }
}
