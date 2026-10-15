using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeWorldScriptedPathReader
{
    public static NativeWorldScriptedPathDefinition Read(ReadOnlySpan<byte> rail, ReadOnlySpan<byte> cameraSection, ReadOnlySpan<byte> executable, int trackIndex, int? selectedCameraList = null)
    {
        if (trackIndex is < 0 or >= 14 || rail.Length != 14 * 2048 || Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("Scripted rail requires the original fourteen-chunk member and verified executable.");
        }
        ReadOnlySpan<byte> chunk = rail.Slice(trackIndex * 2048, 2048);
        int pointCount = chunk[0];
        int stopCount = chunk[1];
        if (pointCount is < 2 or > 127 || stopCount > 4 || 12 + pointCount * 16 > chunk.Length)
        {
            throw new InvalidDataException("Native rail header exceeds its fixed original chunk.");
        }

        var points = ImmutableArray.CreateBuilder<NativeWorldRailPoint>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            ReadOnlySpan<byte> point = chunk.Slice(12 + index * 16, 16);
            points.Add(new NativeWorldRailPoint(BinaryPrimitives.ReadInt32LittleEndian(point), BinaryPrimitives.ReadInt32LittleEndian(point[4..]), BinaryPrimitives.ReadInt32LittleEndian(point[8..]), BinaryPrimitives.ReadInt32LittleEndian(point[12..])));
        }
        int cameraList = selectedCameraList ?? trackIndex;
        if (cameraList is < 0 or >= 28)
        {
            throw new InvalidDataException("Native scripted camera list selector is outside its pointer table.");
        }
        int list = ReadPointer(cameraSection, checked(cameraList * 4));
        int script = ReadPointer(cameraSection, list);
        if (ReadInt32(cameraSection, checked(list + 4)) != 0)
        {
            throw new NotSupportedException("Scripted camera selection requires the original single candidate, without guessed process RNG.");
        }
        var records = ImmutableArray.CreateBuilder<NativeWorldPathCameraRecord>();
        int position = script;
        while (position <= cameraSection.Length - 32)
        {
            ReadOnlySpan<byte> raw = cameraSection.Slice(position, 32);
            NativeWorldPathPosition eye = new(BinaryPrimitives.ReadInt32LittleEndian(raw[8..]), BinaryPrimitives.ReadInt32LittleEndian(raw[12..]), BinaryPrimitives.ReadInt32LittleEndian(raw[16..]));
            NativeWorldPathPosition target = new(BinaryPrimitives.ReadInt32LittleEndian(raw[20..]), BinaryPrimitives.ReadInt32LittleEndian(raw[24..]), BinaryPrimitives.ReadInt32LittleEndian(raw[28..]));
            short deadline = BinaryPrimitives.ReadInt16LittleEndian(raw);
            records.Add(new NativeWorldPathCameraRecord(deadline, raw[2], raw[3], raw[4], eye, target));
            position += 32;
            if (deadline == -1)
            {
                break;
            }
        }
        if (records.Count == 0 || records[^1].Deadline != -1)
        {
            throw new InvalidDataException("Native scripted camera has no bounded terminal record.");
        }
        int firstShape = executable[ExecutableOffset(executable, 0xC763B4 + 2 * trackIndex)];
        int secondShape = executable[ExecutableOffset(executable, 0xC763B5 + 2 * trackIndex)];
        short frontLength = BinaryPrimitives.ReadInt16LittleEndian(executable[ExecutableOffset(executable, 0xC76460 + firstShape * 8)..]);
        short rearLength = BinaryPrimitives.ReadInt16LittleEndian(executable[ExecutableOffset(executable, 0xC76460 + secondShape * 8)..]);
        var definition = new NativeWorldScriptedPathDefinition(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.ToHexString(SHA256.HashData(rail)), Convert.ToHexString(SHA256.HashData(chunk)), Convert.ToHexString(SHA256.HashData(cameraSection.Slice(script, position - script))), trackIndex, checked((byte)firstShape), checked((byte)secondShape), frontLength, rearLength, points.MoveToImmutable(), records.ToImmutable()) { CameraListIndex = cameraList, ForwardStops = chunk.Slice(8, stopCount).ToArray().ToImmutableArray(), ReverseStops = chunk.Slice(4, stopCount).ToArray().ToImmutableArray() };
        definition.Validate();
        return definition;
    }

    private static int ReadPointer(ReadOnlySpan<byte> source, int offset)
    {
        int pointer = ReadInt32(source, offset);
        if (pointer <= 0 || pointer > source.Length - 4)
        {
            throw new InvalidDataException("Native scripted camera pointer leaves its original WMSET section.");
        }
        return pointer;
    }

    private static int ReadInt32(ReadOnlySpan<byte> source, int offset)
    {
        if (offset < 0 || offset > source.Length - 4)
        {
            throw new InvalidDataException("Truncated native scripted camera pointer table.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
    }

    private static int ExecutableOffset(ReadOnlySpan<byte> source, int address)
    {
        int pe = ReadInt32(source, 0x3C);
        if (pe < 0 || pe > source.Length - 24)
        {
            throw new InvalidDataException("Invalid original PE header.");
        }
        int sections = BinaryPrimitives.ReadUInt16LittleEndian(source[(pe + 6)..]);
        int optional = BinaryPrimitives.ReadUInt16LittleEndian(source[(pe + 20)..]);
        int rva = address - 0x400000;
        for (int section = 0; section < sections; section++)
        {
            int header = checked(pe + 24 + optional + section * 40);
            int start = ReadInt32(source, header + 12);
            int size = ReadInt32(source, header + 16);
            int raw = ReadInt32(source, header + 20);
            if (rva >= start && (long)rva < (long)start + size)
            {
                int offset = checked(raw + rva - start);
                if (offset < 0 || offset > source.Length - 2)
                {
                    break;
                }
                return offset;
            }
        }
        throw new InvalidDataException("Native scripted path lookup is absent from the original executable.");
    }
}
