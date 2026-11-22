using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;

internal static class NativeTriggerArrowTailTests
{
    internal static void Run()
    {
        byte[] source = Section();
        byte[] before = source.ToArray();
        PcFieldTriggers basic = PcFieldTriggers.Read(source.AsSpan(0, 536));
        PcFieldTriggers extended = PcFieldTriggers.Read(source);
        Check(basic.Name == "authored" && basic.ControlDirection == 73 && basic.CameraFocus == -12 && basic.CameraLeft == -300 && basic.CameraBottom == 400 && basic.CameraRight == 500 && basic.CameraTop == -600 && basic.CameraScrollConstraint == 2, "A missing arrow tail changed native trigger header fields.");
        Check(basic.LayerDimensions.SequenceEqual(new short[] { 1, 2, 3, 4 }) && basic.InitialLayerScrollOffsets.SequenceEqual(new short[] { -1, -2, -3, -4 }) && basic.LayerCameraFactors.SequenceEqual(new short[] { 7, 8, 9, 10 }), "A missing arrow tail changed layer dimensions, scroll offsets or camera factors.");
        Check(basic.Gateways.Count == 12 && basic.Triggers.Count == 12 && basic.Arrows.Count == 0 && basic.Gateways.All(gateway => gateway.ShowArrow == 0), "Absent presentation bytes fabricated arrows or removed native trigger/gateway records.");
        for (int index = 0; index < 12; index++)
        {
            PcFieldGateway expected = new(new(index, -index, 3), new(10 + index, 20, -30), (short)(-100 - index), (short)(200 + index), (ushort)(50000 + index), (short)(300 + index), 128 + index, 0);
            PcFieldTrigger trigger = new(new(index, 2, 3), new(4, 5, -index), (byte)index, (byte)(index + 1), (byte)(index + 2), (byte)(index + 3));
            Check(basic.Gateways[index] == expected && extended.Gateways[index] == (expected with { ShowArrow = (byte)(index + 1) }), "The full native gateway record or optional arrow flag changed.");
            Check(basic.Triggers[index] == trigger && extended.Triggers[index] == trigger, "Native trigger geometry or background/state/behavior/sound bytes changed.");
            Check(extended.Arrows[index] == new PcFieldArrow(new(100000 + index, -200000 - index, 300000 + index), 0x12340000 + index), "The full optional arrow coordinates or type lost their native 32-bit values.");
        }
        Check(source.SequenceEqual(before), "Parsing native trigger variants changed source bytes.");
        string retained = JsonSerializer.Serialize(extended);
        Array.Clear(source);
        Check(retained == JsonSerializer.Serialize(extended), "Parsed native trigger data retained mutable caller-owned storage.");
        Reject(new byte[535]);
        for (int size = 537; size < 740; size++)
        {
            Reject(new byte[size]);
        }
        Reject(new byte[741]);
        Console.WriteLine("Native trigger sections retain the exact base records with either absent or complete optional arrow presentation.");
    }

    private static byte[] Section()
    {
        byte[] source = new byte[740];
        Encoding.ASCII.GetBytes("authored").CopyTo(source, 0);
        source[9] = 73;
        short[] header = [-12, -300, 400, 500, -600];
        for (int index = 0; index < header.Length; index++)
        {
            Word(source, 10 + index * 2, header[index]);
        }
        source[20] = 2;
        for (int index = 0; index < 4; index++)
        {
            Word(source, 24 + index * 2, (short)(index + 1));
            Word(source, 32 + index * 2, (short)(-index - 1));
            Word(source, 40 + index * 2, (short)(index + 7));
        }
        for (int index = 0; index < 12; index++)
        {
            int gateway = 56 + index * 24;
            short[] values = [(short)index, (short)-index, 3, (short)(10 + index), 20, -30, (short)(-100 - index), (short)(200 + index), unchecked((short)(50000 + index)), (short)(300 + index)];
            for (int field = 0; field < values.Length; field++)
            {
                Word(source, gateway + field * 2, values[field]);
            }
            source[gateway + 20] = (byte)(128 + index);
            int trigger = 344 + index * 16;
            short[] coordinates = [(short)index, 2, 3, 4, 5, (short)-index];
            for (int field = 0; field < coordinates.Length; field++)
            {
                Word(source, trigger + field * 2, coordinates[field]);
            }
            for (int field = 0; field < 4; field++)
            {
                source[trigger + 12 + field] = (byte)(index + field);
            }
            source[536 + index] = (byte)(index + 1);
            int arrow = 548 + index * 16;
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(arrow), 100000 + index);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(arrow + 4), -200000 - index);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(arrow + 8), 300000 + index);
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(arrow + 12), 0x12340000 + index);
        }
        return source;
    }

    private static void Word(byte[] source, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(offset), value);

    private static void Reject(byte[] source)
    {
        bool rejected = false;
        try
        {
            PcFieldTriggers.Read(source);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "A truncated native base, partial arrow tail or extra unknown byte was admitted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
