using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int NativeEffect31Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect31 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private NativeBattlePoseSource ReadNativeEffect31Pose(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, int actor)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect31 requires its current addressed pose owner.");
        return NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect31 requires its current physical skeleton.");
    }

    private NativeBattleEffectArena ReadNativeEffect31Physical(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        if (address >= 0x1D972C0 && (ulong)address + (uint)length <= 0x1D972C0ul + (uint)graphics.Models.Length * 156)
        {
            int actor = checked((int)((address - 0x1D972C0) / 156));
            int offset = checked((int)((address - 0x1D972C0) % 156));
            if (length > 156 - offset)
            {
                throw new InvalidDataException("Effect31 physical read crosses a current model row.");
            }
            return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.Models[actor]), offset, result, 0, length);
        }
        if (address >= 0x1D99A78 && (ulong)address + (uint)length <= 0x1D99A80)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect31 requires its current action parameters.");
            NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, [parameters.Actor78, parameters.Flags79]).Write(2, BitConverter.GetBytes(parameters.TargetMask7A)).Write(4, BitConverter.GetBytes(checked(0x1D280C4u + (uint)parameters.HeaderIndex7C * 20)));
            return NativeBattleEffectNodeContextMemory.Copy(bytes, checked((int)(address - 0x1D99A78)), result, 0, length);
        }
        if (address >= 0x1D280C4 && address < 0x1D28344)
        {
            uint relative = address - 0x1D280C4;
            int offset = checked((int)(relative % 20));
            if (length > 20 - offset)
            {
                throw new InvalidDataException("Effect31 physical read crosses a current action header.");
            }
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[checked((int)(relative / 20))];
            return result.Write(0, Enumerable.Range(offset, length).Select(header.ReadByte).ToArray());
        }
        if (address >= 0x1D28344 && (ulong)address + (uint)length <= 0x1D28344ul + NativeBattleActionPacketsState.ContactCapacity * 24)
        {
            uint relative = address - 0x1D28344;
            int offset = checked((int)(relative % 24));
            if (length > 24 - offset)
            {
                throw new InvalidDataException("Effect31 physical read crosses a current contact row.");
            }
            return result.Write(0, nativeActionPackets.ReadContact(checked((int)(relative / 24))).Encode().AsSpan(offset, length));
        }
        for (int bank = 0; bank < 3; bank++)
        {
            uint first = 0x196B018u + (uint)bank * 0x10000;
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + 0x10000)
            {
                NativeBattleModelCpuBank cpu = (NativeModelCpuResources ?? throw new NotSupportedException("Effect31 requires its current CPU banks.")).Cpu.Banks[bank];
                int relative = checked((int)(address - first));
                foreach (NativeBattleEffectMemoryRegion region in cpu.KnownRanges)
                {
                    int begin = Math.Max(relative, region.Offset);
                    int end = Math.Min(relative + length, region.Offset + region.Data.Length);
                    if (begin < end)
                    {
                        result = result.Write(begin - relative, region.Data.AsSpan(begin - region.Offset, end - begin));
                    }
                }
                return result;
            }
        }
        return NativeBattleEffect31Controller.ReadBytes(frame, address, length);
    }

    private NativeBattleResourcePointer ReadNativeEffect31SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? (frame.Memory.Arena ?? throw new NotSupportedException("Effect31 requires its current resource arena.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect31 requires its current skeleton directory.")).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect31 requires its actual staged skeleton pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect31 requires its current CPU resources.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect31 requires its current CPU resource row.")].Require(12), null);
    }

    private uint ReadNativeEffect31BoneAddress(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, int actor, int bone)
    {
        NativeBattleResourcePointer pointer = ReadNativeEffect31SkeletonPointer(graphics, frame, actor);
        uint skeleton = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect31 requires its physical skeleton backing.");
        return checked(skeleton + 32 + (uint)bone * 48);
    }

    private ImmutableArray<NativeBattlePackedMatrix32> ReadNativeEffect31BodyBones(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, int actor, NativeBattlePoseSource source)
    {
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
        ImmutableArray<NativeBattlePackedMatrix32>.Builder result = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(matrices.Length);
        for (int bone = 0; bone < matrices.Length; bone++)
        {
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
            NativeBattleEffectArena padding = ReadNativeEffect31Physical(graphics, frame, checked(ReadNativeEffect31BoneAddress(graphics, frame, actor, bone) + 18), 2);
            (byte[] bytes, bool[] known) = padding.ReadSnapshot(0, 2);
            result.Add(packed with { Bytes = packed.Bytes.SetItem(18, bytes[0]).SetItem(19, bytes[1]), Known = packed.Known.SetItem(18, known[0]).SetItem(19, known[1]) });
        }
        return result.ToImmutable();
    }

    private NativeBattleEffect31Frame ObserveNativeEffect31Point(NativeBattleEffect31Frame frame, NativeBattleBoneAnchorProducer producer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect31 requires its current temporary owner.");
        NativeBattleEffectMemory memory = ObserveNativeBoneAnchor(frame.Memory, producer);
        return frame with { Memory = memory, Staging = NativeFileStaging };
    }

    private NativeBattleEffect31PointStep ReadNativeEffect31Point(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, uint modelAddress, int selector, int fraction, NativeBattleEffectArena retained)
    {
        int actor = NativeEffect31Actor(graphics, modelAddress);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : ReadNativeEffect31Pose(graphics, frame, actor);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
        frame = ObserveNativeEffect31Point(frame, producer);
        NativeBattleVector point = producer.Enabled ? new(unchecked((short)frame.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)frame.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)frame.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
        return new(frame, retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z))));
    }
}
