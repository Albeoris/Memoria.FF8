using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private readonly Dictionary<int, int> nativePreModelCursors = [];
    private long nativePreModelCursorGeneration = -1;

    private int ReadNativePreModelCursor(int fieldId)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native field allocation layout requires the authenticated executable contract.");
        }
        long generation = resources.Generation;
        if (nativePreModelCursorGeneration != generation)
        {
            nativePreModelCursors.Clear();
            nativePreModelCursorGeneration = generation;
        }
        if (overrides is null && nativePreModelCursors.TryGetValue(fieldId, out int cached))
        {
            return cached;
        }
        int cursor = ReadNativeFieldAllocationPrefix(fieldId);
        using IFlArchive archive = OpenNested(fieldNames[fieldId]);
        cursor = ReadNativeProgramAllocationEnd(Read(archive, fieldNames[fieldId] + ".jsm"), cursor);
        cursor = checked(cursor + Read(archive, fieldNames[fieldId] + ".pcb").Length);
        nativePreModelCursors[fieldId] = cursor;
        return cursor;
    }

    private int ReadNativeFieldAllocationPrefix(int fieldId)
    {
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldId));
        }
        string name = fieldNames[fieldId];
        using IFlArchive archive = OpenNested(name);
        return ReadNativeFieldAllocationPrefix(name, member => Read(archive, member));
    }

    private int ReadNativeFieldAllocationPrefix(string name, Func<string, byte[]> readMember)
    {
        byte[] map = readMember(name + ".map");
        byte[] information = readMember(name + ".inf");
        if (information.Length != 676)
        {
            throw UnsupportedRuntime("Native field layout requires the original information record.");
        }
        int polygons = 0;
        while (true)
        {
            int offset = checked(polygons * 16);
            if (offset + 2 > map.Length)
            {
                throw new InvalidDataException("Native field display records have no terminal sentinel.");
            }
            if (BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(offset, 2)) == 0x7FFF)
            {
                break;
            }
            polygons++;
        }
        //471010 reserves two display lists and 80 bytes for the camera record.
        int cursor = checked(44 + AlignNativeFieldAllocation(readMember(name + ".id").Length) + AlignNativeFieldAllocation(map.Length) + 32 * polygons + 80 + AlignNativeFieldAllocation(information.Length));
        if (information[14] == 1)
        {
            byte[] mask = readMember(name + ".msk");
            if (mask.Length < 2)
            {
                throw new InvalidDataException("Native field mask allocation has no record count.");
            }
            int count = BinaryPrimitives.ReadUInt16LittleEndian(mask);
            cursor = checked(cursor + AlignNativeFieldAllocation(mask.Length) + 56 * count);
        }
        cursor = checked(cursor + readMember(name + ".rat").Length + readMember(name + ".mrt").Length);
        //471C56..471CB0 advances by the aligned MSD size twice; the GSM name does not issue another read.
        cursor = checked(cursor + 2 * AlignNativeFieldAllocation(readMember(name + ".msd").Length));
        byte[] presentation = readMember(name + ".pmd");
        if (presentation.Length != 4)
        {
            cursor = checked(cursor + 24356);
        }
        return cursor;
    }

    private static int AlignNativeFieldAllocation(int value)
    {
        return checked(value + 3) & ~3;
    }

    private static int ReadNativeProgramAllocationEnd(byte[] program, int cursor)
    {
        if (program.Length < 8)
        {
            throw new InvalidDataException("Native field program has no allocation header.");
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(4, 2));
        int code = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(6, 2));
        int groups = program[0] + program[1] + program[2] + program[3];
        if (table < 8 + 2 * groups || table > short.MaxValue || code > short.MaxValue || code < table + 4 || code > program.Length || (code - table) % 2 != 0 || (program.Length - code) % 4 != 0)
        {
            throw new NotSupportedException("Native field program allocation offsets are outside the represented format.");
        }
        int pointers = (code - table) / 2;
        if (pointers >= short.MaxValue)
        {
            throw new NotSupportedException("Native field script table exceeds its signed allocation count.");
        }
        (int First, int Last)[] actors = new (int, int)[program[3]];
        for (int i = 0; i < actors.Length; i++)
        {
            int group = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(8 + 2 * (groups - actors.Length + i), 2));
            actors[i] = (group >> 7, (group >> 7) + (group & 127) + 1);
        }
        int copied = 0;
        for (int i = 0; i < pointers - 1; i++)
        {
            int first = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(table + 2 * i, 2));
            int last = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(table + 2 * i + 2, 2)) & 0x7FFF;
            bool admitted = (first & 0x8000) != 0 || actors.Any(actor => i >= actor.First && i <= actor.Last);
            first &= 0x7FFF;
            if (admitted)
            {
                //52B3E0 copies once even for an admitted terminal/padding pair.
                //Only its allocation size is represented here, never the unowned copied payload.
                copied = checked(copied + Math.Max(1, last - first));
            }
        }
        return checked(AlignNativeFieldAllocation(checked(cursor + 2 * pointers)) + 4 * copied);
    }
}
