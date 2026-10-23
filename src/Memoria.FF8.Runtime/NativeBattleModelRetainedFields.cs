using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The physical rotation, position/scale, normal-color and bounds fields retained by502340's Flags0-only clear.</summary>
public sealed record NativeBattleModelRetainedFields([property: JsonRequired] short? Rotation12, [property: JsonRequired] short? Rotation14, [property: JsonRequired] short? Rotation16, [property: JsonRequired] ushort? RotationPadding18, [property: JsonRequired] NativeBattleModelTransform? Transform, [property: JsonRequired] uint? Color44)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectLocalBounds? GeometryBounds52 { get; init; }

    public static NativeBattleModelRetainedFields FromColdProcess() => FromLoaderBytes(new byte[156]);

    public static NativeBattleModelRetainedFields FromLoaderBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 156)
        {
            throw new InvalidDataException("Native retained graphics fields require their actual156-byte model row.");
        }
        NativeBattleVector initial = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(20)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(22)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(24)));
        NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(28)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(30)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(32)));
        NativeBattleModelTransform transform = new(initial, position, BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(34)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(36)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(38)))
        {
            Scale148 = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(148)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(150)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(152))),
            Scale154 = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(154))
        };
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(12)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(14)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(16)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(18)), transform, BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(44)))
        {
            GeometryBounds52 = new(new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(52)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(54)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(56))), new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(58)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(60)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(62))))
        };
    }

    public static NativeBattleModelRetainedFields Capture(NativeBattleGraphicsModel model) => new(model.Rotation12, model.Rotation14, model.Rotation16, model.RotationPadding18, model.Transform, model.Color44) { GeometryBounds52 = model.GeometryBounds52 };

    public NativeBattleGraphicsModel Restore502340(NativeBattleGraphicsModel model) => model with { Rotation12 = Rotation12, Rotation14 = Rotation14, Rotation16 = Rotation16, RotationPadding18 = RotationPadding18, Transform = Transform, Color44 = Color44, GeometryBounds52 = GeometryBounds52 };
}
