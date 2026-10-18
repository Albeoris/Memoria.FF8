using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareDrawingEnvironmentApplication(NativeSoftwareDrawingEnvironmentBuffers Buffers, NativeSoftwareDrawingEnvironmentState Environment, NativeSoftwareTextureCacheInvalidation? Invalidation);

public sealed record NativeSoftwareDrawingEnvironmentBuffers([property: JsonRequired] ImmutableArray<byte> Buffer0, [property: JsonRequired] ImmutableArray<byte> Buffer1, [property: JsonRequired] ImmutableArray<byte>? CurrentGlobal)
{
    public const int RecordLength = 92;

    public static NativeSoftwareDrawingEnvironmentBuffers Initialize500400(ImmutableArray<byte>? retainedCurrentGlobal)
    {
        ValidateOptionalRecord(retainedCurrentGlobal);
        byte[] record = new byte[RecordLength];
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(6), 216);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x14), 10);
        record[0x16] = 1;
        record[0x17] = 1;
        ImmutableArray<byte> initialized = record.ToImmutableArray();
        return new(initialized, initialized, retainedCurrentGlobal);
    }

    public NativeSoftwareDrawingEnvironmentBuffers ReplaceBuffer(int index, ImmutableArray<byte> record)
    {
        Validate();
        ValidateRecord(record);
        return index switch
        {
            0 => this with { Buffer0 = record },
            1 => this with { Buffer1 = record },
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    public NativeSoftwareDrawingEnvironmentApplication Apply45B660(int index, NativeSoftwareDrawingEnvironmentState environment)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(environment);
        ImmutableArray<byte> record = index switch
        {
            0 => Buffer0,
            1 => Buffer1,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        short left = ReadWord(record, 0);
        short top = ReadWord(record, 2);
        NativeSoftwareDrawingEnvironmentState updated = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(environment with
        {
            TopLeftX = left,
            TopLeftY = top,
            RightX = unchecked((uint)(left + ReadWord(record, 4) - 1)),
            BottomY = unchecked((uint)(top + ReadWord(record, 6) - 1)),
            OffsetX = ReadWord(record, 8),
            OffsetY = ReadWord(record, 10)
        });
        NativeSoftwareEnvironmentDerived derived = updated.Derived!;
        NativeSoftwareDrawingRectangle rectangle = derived.Rectangle;
        NativeSoftwareTextureCacheInvalidation? invalidation = (derived.Mode & 2) != 0 || updated.Debug != 0 ? new(rectangle.X, rectangle.Y, unchecked(rectangle.X + rectangle.Width - 1), unchecked(rectangle.Y + rectangle.Height - 1)) : null;
        return new(this with { CurrentGlobal = record }, updated, invalidation);
    }

    public void Validate()
    {
        ValidateRecord(Buffer0);
        ValidateRecord(Buffer1);
        ValidateOptionalRecord(CurrentGlobal);
    }

    private static short ReadWord(ImmutableArray<byte> record, int offset) => BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(offset, 2));

    private static void ValidateOptionalRecord(ImmutableArray<byte>? record)
    {
        if (record is ImmutableArray<byte> known)
        {
            ValidateRecord(known);
        }
    }

    private static void ValidateRecord(ImmutableArray<byte> record)
    {
        if (record.IsDefault || record.Length != RecordLength)
        {
            throw new InvalidDataException("Native drawing environment requires its complete retained 92-byte record.");
        }
    }
}
