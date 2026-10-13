using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Assets;

public readonly record struct FieldBounds(short Top, short Bottom, short Right, short Left);
public sealed record FieldGateway(Vector3 Start, Vector3 End, short DestinationX, short DestinationY, short DestinationTriangle, ushort DestinationField, uint Extra20, uint Extra24, uint Extra28)
{
    public bool Defined => DestinationField != 0x7FFF && DestinationTriangle != -1;
    public byte Direction => (byte)Extra28;
}
public sealed record FieldAreaTrigger(Vector3 Start, Vector3 End, byte AreaId, byte Extra13, byte Side, byte Extra15);

/// <summary>The selected original PC INF layout; packet fields retain their native widths.</summary>
public sealed record FieldInformation(byte ControlDirection, short CameraHeight, IReadOnlyList<FieldBounds> CameraBounds, IReadOnlyList<FieldBounds> ScreenBounds, IReadOnlyList<FieldGateway> Gateways, IReadOnlyList<FieldAreaTrigger> AreaTriggers)
{
    public byte BackgroundWrapMask { get; init; }

    public static FieldInformation Read(byte[] bytes)
    {
        if (bytes.Length != 676)
        {
            throw new InvalidDataException($"Unverified PC INF layout: {bytes.Length} bytes; selected native layout requires 676.");
        }
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        Vector3 Point(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4));
        FieldBounds Bounds(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4), Word(offset + 6));
        FieldGateway[] gateways = new FieldGateway[12];
        FieldAreaTrigger[] triggers = new FieldAreaTrigger[12];
        for (int index = 0; index < gateways.Length; index++)
        {
            int offset = 100 + index * 32;
            gateways[index] = new FieldGateway(Point(offset), Point(offset + 6), Word(offset + 12), Word(offset + 14), Word(offset + 16), (ushort)Word(offset + 18), Dword(offset + 20), Dword(offset + 24), Dword(offset + 28));
            offset = 484 + index * 16;
            triggers[index] = new FieldAreaTrigger(Point(offset), Point(offset + 6), bytes[offset + 12], bytes[offset + 13], bytes[offset + 14], bytes[offset + 15]);
        }
        return new FieldInformation(bytes[9], Word(18), Enumerable.Range(0, 8).Select(index => Bounds(20 + index * 8)).ToArray(), Enumerable.Range(0, 2).Select(index => Bounds(84 + index * 8)).ToArray(), gateways, triggers) { BackgroundWrapMask = bytes[15] };
    }
}
