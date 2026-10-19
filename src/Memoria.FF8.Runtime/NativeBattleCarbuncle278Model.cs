using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278ModelStep(NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Task);
public sealed record NativeBattleCarbuncle278ModelDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);

/// <summary>The creature's retained pose and ordinary6A79B0 draw over its original executable skeleton.</summary>
public static class NativeBattleCarbuncle278Model
{
    public static NativeBattleCarbuncle278CreaturePose Advance6FBDB0(NativeBattleModulePoolTask task, NativeBattleCarbuncleExecutableMemory executable, NativeBattleEffectOwnedModelResource resource)
    {
        if ((task.ReadByte(0x8D) & 3) != 0)
        {
            throw new NotSupportedException("Native Carbuncle pose speed requires its selected current cursor contract.");
        }
        int count = executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0];
        byte selector = task.ReadByte(0x8C);
        byte flags = task.ReadByte(0x8D);
        NativeBattlePoseFirstFrame first;
        if (task.ReadByte(0x92) >= task.ReadByte(0x93))
        {
            first = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), count, selector);
            task = task.WriteByte(0x8D, (byte)((flags & 0xF3) | 4)).WriteByte(0x92, 1).WriteByte(0x93, NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan())[selector]);
        }
        else
        {
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(executable.Skeleton, 0, count);
            first = NativeBattlePoseDecoder.AdvanceRegular508F90(resource.PoseSource.Animations.AsSpan(), count, selector, pose, task.ReadWord(0x8E), task.ReadWord(0x90));
            task = task.WriteByte(0x92, unchecked((byte)(task.ReadByte(0x92) + 1))).WriteByte(0x8D, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        }
        task = task.WriteWord(0x8E, first.ByteOffset).WriteWord(0x90, first.BitOffset);
        return new(task, executable.SelectFirstPose(first));
    }

    public static NativeBattleCarbuncle278ModelDraw Draw6A79B0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, NativeBattleCameraProjection projection, bool hardware)
    {
        NativeBattleCarbuncleExecutableMemory executable = memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle model draw requires its current executable skeleton.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Carbuncle model draw requires its actual projection work arena.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Carbuncle model draw requires its current view matrix.");
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x60 + index * 2))).ToArray();
        NativeBattleActionMatrix model = new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x74)), unchecked((int)task.ReadDword(0x78)), unchecked((int)task.ReadDword(0x7C)), 4096);
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.ComposeStageCamera([model], view, 0)[0];
        NativeBattleEffectArena skeleton = executable.Skeleton;
        int count = executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0];
        for (int index = 0; index < count; index++)
        {
            NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(skeleton, 32 + index * 48);
            skeleton = NativeBattleArenaPose.WriteMatrix(skeleton, 32 + index * 48, NativeBattlePoseMatrices.ComposeStageCamera([bone], modelView, 0)[0], true);
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Carbuncle model draw requires its retained renderer process.");
        var gate = process.Enter5099D0(hardware);
        process = gate.Process;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (gate.Draw)
        {
            uint rgb = task.ReadDword(0x48);
            uint mask = task.ReadDword(0x9C);
            process = process.PublishModelColor(rgb);
            int geometryStart = resource.Section(1);
            ImmutableArray<byte> geometry = resource.Bytes.Slice(geometryStart, resource.Section(2) - geometryStart);
            NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(geometry, selector => NativeBattleArenaPose.ReadMatrix(skeleton, checked(32 + selector * 48)), projection, rgb, mask);
            foreach (NativeBattleGfGeometryAttempt write in batch.VertexScratchWrites)
            {
                arena = arena.Write(checked(0x2E84 + write.ByteOffsetRelative), write.Data.AsSpan());
            }
            foreach (NativeBattleGeometryPrimitive primitive in batch.Primitives)
            {
                byte[] bytes = new byte[primitive.Words.Length * 4];
                for (int index = 0; index < primitive.Words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
                }
                attempts.Add(new(allocated, bytes.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive));
                allocated = checked(allocated + bytes.Length);
            }
            if (batch.SourceEnd is int end)
            {
                uint cursor = checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native Carbuncle6A79B0 requires its current COMMON cursor.")));
                process = process.Publish50FDF0(unchecked(cursor + (uint)allocated), unchecked(resource.Address + (uint)geometryStart + (uint)end));
            }
        }
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, current);
        return new(memory with { Arena = arena, PrimitiveProcess = process, CarbuncleExecutableMemory = executable with { Skeleton = skeleton } }, new(attempts.ToImmutable(), allocated));
    }
}
