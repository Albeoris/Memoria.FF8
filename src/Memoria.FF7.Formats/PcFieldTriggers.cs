using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcFieldGateway(Vector3 A, Vector3 B, short DestinationX, short DestinationY, ushort DestinationTriangle, short DestinationField, int Direction, byte ShowArrow);
public sealed record PcFieldTrigger(Vector3 A, Vector3 B, byte BackgroundParameter, byte State, byte Behavior, byte Sound);
public sealed record PcFieldArrow(Vector3 Position, int Type);
public sealed record PcFieldTriggers(string Name, byte ControlDirection, short CameraFocus, short CameraLeft, short CameraBottom, short CameraRight, short CameraTop, IReadOnlyList<short> LayerDimensions, IReadOnlyList<PcFieldGateway> Gateways, IReadOnlyList<PcFieldTrigger> Triggers, IReadOnlyList<PcFieldArrow> Arrows)
{
    public byte CameraScrollConstraint { get; init; }

    public static PcFieldTriggers Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        string name = cursor.Ascii(9);
        byte direction = cursor.U8();
        short focus = cursor.I16();
        short left = cursor.I16();
        short bottom = cursor.I16();
        short right = cursor.I16();
        short top = cursor.I16();
        byte cameraScrollConstraint = cursor.U8();
        cursor.Skip(3);
        short[] dimensions = [cursor.I16(), cursor.I16(), cursor.I16(), cursor.I16()];
        cursor.Skip(24);
        PcFieldGateway[] gateways = new PcFieldGateway[12];
        for (int i = 0; i < gateways.Length; i++)
        {
            Vector3 a = cursor.Vector16();
            Vector3 b = cursor.Vector16();
            short destinationX = cursor.I16();
            short destinationY = cursor.I16();
            ushort triangle = cursor.U16();
            short map = cursor.I16();
            byte playerDirection = cursor.U8();
            cursor.Skip(3);
            gateways[i] = new PcFieldGateway(a, b, destinationX, destinationY, triangle, map, playerDirection, 0);
        }
        PcFieldTrigger[] triggers = new PcFieldTrigger[12];
        for (int i = 0; i < triggers.Length; i++)
        {
            triggers[i] = new PcFieldTrigger(cursor.Vector16(), cursor.Vector16(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8());
        }
        for (int i = 0; i < gateways.Length; i++)
        {
            gateways[i] = gateways[i] with { ShowArrow = cursor.U8() };
        }
        PcFieldArrow[] arrows = new PcFieldArrow[12];
        for (int i = 0; i < arrows.Length; i++)
        {
            arrows[i] = new PcFieldArrow(cursor.Vector32(), cursor.I32());
        }
        if (cursor.Remaining != 0)
        {
            throw new InvalidDataException("FF7 trigger section has an unsupported record size.");
        }
        return new PcFieldTriggers(name, direction, focus, left, bottom, right, top, Array.AsReadOnly(dimensions), Array.AsReadOnly(gateways), Array.AsReadOnly(triggers), Array.AsReadOnly(arrows)) { CameraScrollConstraint = cameraScrollConstraint };
    }
}
