using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGroundCaptureMemory([property: JsonRequired] NativeBattleEffectArena Statics)
{
    public const uint Address = 0x2795920;
    public const int Length = 0x280;
    public const string PacketBank = "2795920";
    public static NativeBattleGroundCaptureMemory FromColdProcess() => new(NativeBattleEffectArena.Clear());

    public void Validate()
    {
        Statics.Validate();
        if (Statics.Regions.Any(region => region.Offset > Length - region.Data.Length) || Statics.UnknownRegions.Any(region => region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Native stage ground capture leaves its shared physical static window.");
        }
    }

    public byte[] Read(int offset, int count)
    {
        RequireRange(offset, count);
        return Statics.Read(offset, count);
    }

    public NativeBattleGroundCaptureMemory Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        return this with { Statics = Statics.Write(offset, bytes) };
    }

    private void RequireRange(int offset, int count)
    {
        Validate();
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native stage ground capture access leaves its physical static window.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGroundCaptureMemory? GroundCapture { get; init; }
}

public sealed record NativeBattleGroundCaptureWrite(NativeBattleEffectMemory Memory, NativeBattleLogicalOrdering Ordering);

/// <summary>8DE1F0 inserts the restore environment, current ground draw, and capture commands in native order.</summary>
public static class NativeBattleGroundCapturePackets
{
    public static NativeBattleThundaga105Bounds Bounds8DE160(ReadOnlySpan<byte> geometry)
    {
        if (geometry.Length < 12)
        {
            throw new InvalidDataException("Native ground capture bounds require their actual geometry header.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(geometry.Slice(4));
        if (count < 2 || count > (geometry.Length - 12) / 8)
        {
            throw new NotSupportedException("Native ground capture bounds require at least two currently supplied vertex records.");
        }
        short[] minimum = new short[3];
        short[] maximum = new short[3];
        for (int vertex = 0; vertex < count; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(geometry.Slice(12 + vertex * 8 + axis * 2));
                minimum[axis] = vertex == 0 ? value : Math.Min(minimum[axis], value);
                maximum[axis] = vertex == 0 ? value : Math.Max(maximum[axis], value);
            }
        }
        return new(new(minimum[0], minimum[1], minimum[2]), new(maximum[0], maximum[1], maximum[2]));
    }

    public static NativeBattleGroundCaptureWrite Append8DE1F0(NativeBattleEffectMemory memory, NativeBattleLogicalOrdering ordering, byte stageResourceIndex, Func<NativeBattleGroundCaptureWrite, NativeBattleGroundCaptureWrite> drawGround)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native ground capture requires its actual primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native ground capture requires the current physical environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native ground capture requires one of two physical environment buffers.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native ground capture requires both current DRAWENV records.");
        ImmutableArray<byte> source = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        if (source.IsDefault || source.Length != 92)
        {
            throw new InvalidDataException("Native ground capture requires its complete alternate DRAWENV.");
        }
        NativeBattleGroundCaptureWrite current = new(memory, ordering);
        if (stageResourceIndex == 0x8E)
        {
            current = AppendArena(current, NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), source), 64, source[0x18] == 0 ? 24 : 36);
            current = drawGround(current);
            current = AppendArena(current, Black(true), 24, 24, 1);
            current = AppendArena(current, Words(0x02000000, 0xE1000020, 0), 12, 12);
            current = AppendArena(current, Black(false), 24, 24, 1);
            current = AppendArena(current, NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), 384, 256, 128, 256), 12, 12);
            return AppendArena(current, NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[8].ToImmutableArray(), 384, 256), 12, 8);
        }
        NativeBattleGroundCaptureMemory retained = memory.GroundCapture ?? throw new NotSupportedException("Native ordinary ground capture requires its retained shared static window.");
        int parity = (buffer - 1) & 1;
        int restorePacket = parity * 0x40;
        int captureEnvironment = 0x88 + parity * 92;
        int restoreEnvironment = 0x140 + parity * 92;
        int capturePacket = 0x200 + parity * 0x40;
        byte[] restore = source.ToArray();
        restore[0x18] = 0;
        retained = retained.Write(restoreEnvironment, restore);
        byte[] capture = source.ToArray();
        short[] rectangle = [384, 256, 128, 256, 384, 256];
        for (int index = 0; index < rectangle.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(capture.AsSpan(index * 2), rectangle[index]);
        }
        capture[0x18] = 1;
        capture[0x19] = 0;
        capture[0x1A] = 0;
        capture[0x1B] = 0;
        retained = retained.Write(captureEnvironment, capture);
        current = PublishStatic(current, retained, restorePacket, restore.ToImmutableArray());
        current = drawGround(current);
        retained = current.Memory.GroundCapture ?? throw new NotSupportedException("Native ground drawing lost the shared capture static owner.");
        return PublishStatic(current, retained, capturePacket, capture.ToImmutableArray());
    }

    private static NativeBattleGroundCaptureWrite PublishStatic(NativeBattleGroundCaptureWrite current, NativeBattleGroundCaptureMemory retained, int offset, ImmutableArray<byte> environment)
    {
        ImmutableArray<byte> generated = NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment);
        int length = environment[0x18] == 0 ? 24 : 36;
        retained = retained.Write(offset + 3, generated.AsSpan().Slice(3, length - 3));
        NativeBattleLogicalOrdering.Builder links = new(current.Ordering);
        NativeBattlePrimitiveReference reference = new(NativeBattleGroundCaptureMemory.PacketBank, 0, offset);
        links.InvalidatePacketRange(reference, length);
        bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
        links.Prepend(17, reference, ReadWords(generated.AsSpan().Slice(0, length)), 0, [0, 0, 0, 0]);
        if (linked)
        {
            retained = retained.Write(offset, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
        }
        return new(current.Memory with { GroundCapture = retained }, links.ToImmutable());
    }

    private static NativeBattleGroundCaptureWrite AppendArena(NativeBattleGroundCaptureWrite current, ImmutableArray<byte> data, int allocation, int writtenLength, ushort flags = 0)
    {
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, writtenLength - 3);
        byte[] bytes = new byte[allocation];
        data.AsSpan().CopyTo(bytes);
        NativeBattleGeometryPrimitive primitive = new(17, ReadWords(bytes), [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native ground capture allocation requires its current COMMON packet owner.");
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, current.Ordering, new NativeBattleGfGeometryBatch([attempt], allocation), flags);
        return new(current.Memory with { PrimitiveProcess = written.Process }, written.Ordering);
    }

    private static ImmutableArray<byte> Black(bool semi) => Words(0x05000000, semi ? 0x2A000000u : 0x28000000u, 0, 127, 255u << 16, 255u << 16 | 127);

    private static ImmutableArray<byte> Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<uint> ReadWords(ReadOnlySpan<byte> bytes)
    {
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(bytes.Length / 4);
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset)));
        }
        return words.MoveToImmutable();
    }
}
