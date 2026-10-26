using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleModulePacketBatch Screen632F50(int red, int green, int blue, int bucketOffset)
    {
        NativeBattleModulePacketBatch rectangle = NativeBattleFlashPackets.Emit571250(red, green, blue);
        int bucket = unchecked(17 + bucketOffset);
        NativeBattleAllocatedPrimitive first = rectangle.Primitives[0];
        first = first with { Primitive = first.Primitive with { Bucket = bucket } };
        NativeBattleGeometryPrimitive mode = new(bucket, [0x01000000, 0xE1000220], [0, 0, 0, 0]) { PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray() };
        return new([first, new(mode, 16, 1)], 24);
    }

    public static int OpeningIntensity632EB0(short age)
    {
        if (age < 4)
        {
            return age * 255 / 4 & 255;
        }
        if (age >= 16)
        {
            return 255 - ((age - 16) * 255 / 8 & 255);
        }
        return 255;
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceOpening632EB0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, NativeBattleModulePacketBatch, NativeBattleEffect218Frame> deliver)
    {
        int intensity = OpeningIntensity632EB0(Signed(Read(frame, address), 12));
        frame = deliver(frame, Screen632F50(intensity, intensity, intensity, 0));
        return AdvanceScreenAge(frame, address, graphicsFlags, 24);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceFixedScreen633680(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, NativeBattleModulePacketBatch, NativeBattleEffect218Frame> deliver)
    {
        frame = deliver(frame, Screen632F50(112, 128, 240, 4096));
        return AdvanceScreenAge(frame, address, graphicsFlags, 20);
    }

    private static (NativeBattleEffect218Frame Frame, int Result) AdvanceScreenAge(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, short limit)
    {
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= limit ? 2 : 0);
    }
}
