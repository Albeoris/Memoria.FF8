using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89CaptureStep(NativeBattleRinoa89Frame Frame, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleRinoa89Capture
{
    public const int TableOffset = 82948;
    public const uint TableAddress = 0x20DFAB8 + TableOffset;

    public static NativeBattleFileStaging PublishMatrix56C2F0(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        matrix.Validate();
        for (int lane = 0; lane < 9; lane++)
        {
            staging = staging.Write(address + (uint)lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
        }
        return staging.MarkUnknown(address + 18, 2).Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));
    }

    public static NativeBattleRinoa89CaptureStep Begin6FBEE0(NativeBattleRinoa89Frame frame, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native6FBEE0 requires its actual private capture table.");
        byte[] roots = new byte[NativeBattlePrimitiveOrdering.BattleTableLength * 4];
        for (int index = 0; index < NativeBattlePrimitiveOrdering.BattleTableLength; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(roots.AsSpan(index * 4), index == 0 ? uint.MaxValue : TableAddress + (uint)(index - 1) * 4);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(TableOffset, roots) } };
        ImmutableArray<byte> previous = CurrentEnvironment(frame);
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(previous.AsSpan().Slice(20));
        uint mode = 0xE1000000u | ((uint)page & 0x9FF) | (previous[22] != 0 ? 0x200u : 0) | (previous[23] != 0 ? 0x400u : 0);
        NativeBattleRinoa89CaptureStep step = AppendProduced(frame, ordering, Words(0x02000000, mode, 0xE2000000), 12, 12, 1);
        return AppendProduced(step.Frame, step.Ordering, Environment(previous, false), 64, 24, 1);
    }

    public static NativeBattleRinoa89CaptureStep End6FBEE0(NativeBattleRinoa89Frame frame, NativeBattleLogicalOrdering ordering)
    {
        return AppendProduced(frame, ordering, Environment(CurrentEnvironment(frame), true), 64, 36, 4385);
    }

    public static NativeBattleRinoa89CaptureStep AppendCommon(NativeBattleRinoa89Frame frame, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, uint tableAddress = TableAddress)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Wishing Star capture requires its actual common packet owner.");
        process.Validate();
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Native Wishing Star capture requires its actual common cursor.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Native Wishing Star capture requires its actual common packet bank.");
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || cursor > process.CommonPacketWriteCapacity - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native Wishing Star capture leaves its aligned common allocation.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star capture requires its private addressed roots.");
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        int acceptedEnd = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.WriteMask.Length != attempt.Data.Length || (attempt.PacketLength & 3) != 0 || cursor + attempt.ByteOffsetRelative > process.CommonPacketWriteCapacity - attempt.PacketLength)
            {
                throw new InvalidDataException("Native Wishing Star capture requires bounded physical packet attempts.");
            }
            int address = checked(cursor + attempt.ByteOffsetRelative);
            NativeBattlePrimitiveReference reference = new(bank, process.PacketBuffer, address);
            for (int offset = 0; offset < attempt.PacketLength; offset++)
            {
                if (attempt.WriteMask[offset] != NativeBattleGfPacketByteState.Untouched)
                {
                    links.InvalidatePacketRange(reference with { ByteOffset = address + offset }, 1);
                }
            }
            stores.Add(new(address, attempt.Data, attempt.WriteMask));
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            if ((uint)primitive.Bucket >= NativeBattlePrimitiveOrdering.BattleTableLength || primitive.Words.IsDefaultOrEmpty || primitive.Words.Length < 1 + (primitive.Words[0] >> 24) || primitive.Words.Length * 4 > attempt.PacketLength || attempt.ByteOffsetRelative + attempt.PacketLength > batch.AllocationBytes)
            {
                throw new InvalidDataException("Native Wishing Star capture packet leaves its actual private table or allocation.");
            }
            acceptedEnd = Math.Max(acceptedEnd, attempt.ByteOffsetRelative + attempt.PacketLength);
            if (links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity)
            {
                int root = NativeBattleWaterExecutionFields.ArenaOffset(checked(tableAddress + (uint)primitive.Bucket * 4), 4);
                uint previous = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(root, 4));
                uint proxy = 0x1C48828u + (uint)links.ProxyCount * 24;
                links.PrependAddressed(previous, reference, primitive.Words, 0, primitive.VertexPayload4, pilotGeometry: primitive.PilotGeometry);
                stores.Add(new(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray(), NativeBattleCommonPacketArena.KnownLinkMask));
                arena = arena.Write(root, BitConverter.GetBytes(proxy));
            }
        }
        if (acceptedEnd != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native Wishing Star capture final cursor differs from its accepted packets.");
        }
        process = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = cursor + batch.AllocationBytes };
        return new(frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = process } }, links.ToImmutable());
    }

    private static ImmutableArray<byte> CurrentEnvironment(NativeBattleRinoa89Frame frame)
    {
        ImmutableArray<byte> environment = frame.Memory.PrimitiveProcess?.Software?.EnvironmentBuffers?.CurrentGlobal ?? throw new NotSupportedException("Native45B640 requires its actual current global drawing environment.");
        if (environment.IsDefault || environment.Length != 92)
        {
            throw new InvalidDataException("Native45B640 requires the complete current drawing environment.");
        }
        return environment;
    }

    private static ImmutableArray<byte> Environment(ImmutableArray<byte> previous, bool restore)
    {
        byte[] environment = new byte[92];
        short x = restore ? (short)384 : BinaryPrimitives.ReadInt16LittleEndian(previous.AsSpan().Slice(8)) != 0 ? (short)0 : (short)320;
        short y = restore ? (short)256 : BinaryPrimitives.ReadInt16LittleEndian(previous.AsSpan().Slice(10));
        short[] rectangle = [x, y, 320, 216, x, y];
        for (int index = 0; index < rectangle.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(index * 2), rectangle[index]);
        }
        previous.AsSpan().Slice(20, 4).CopyTo(environment.AsSpan(20));
        if (!restore)
        {
            environment[22] = 0;
        }
        environment[24] = restore ? (byte)1 : (byte)0;
        return NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment.ToImmutableArray());
    }

    private static NativeBattleRinoa89CaptureStep AppendProduced(NativeBattleRinoa89Frame frame, NativeBattleLogicalOrdering ordering, ImmutableArray<byte> data, int allocation, int written, int bucket)
    {
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, written - 3);
        ImmutableArray<uint> words = Enumerable.Range(0, allocation / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(index * 4))).ToImmutableArray();
        return AppendCommon(frame, ordering, new([new(0, data, mask.ToImmutableArray(), new(bucket, words, [0, 0, 0, 0]))], allocation));
    }

    private static ImmutableArray<byte> Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return bytes.ToImmutableArray();
    }
}
