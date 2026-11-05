using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A resource-local byte authority; its offsets are never global guest addresses.</summary>
public sealed record NativeFieldResourceOwner([property: JsonRequired] NativeBattleFileStaging Storage, [property: JsonRequired] int Length)
{
    public void Validate()
    {
        if (Storage is null || Storage.BaseAddress != 0 || Length <= 0 || Storage.ResourcePointers.Count != 0)
        {
            throw new InvalidDataException("Field resource storage requires a positive bounded local owner.");
        }
        Storage.Validate();
        if (Storage.KnownExtentEnd > (uint)Length)
        {
            throw new InvalidDataException("Field resource storage exceeds its declared local bounds.");
        }
    }

    internal static NativeFieldResourceOwner Create(int length) => new(new([]) { BaseAddress = 0 }, length);

    internal static NativeFieldResourceOwner Borrow(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || length <= 0 || offset < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Field input requires a nonempty immutable member slice.");
        }
        NativeFieldResourceOwner owner = Create(length);
        owner.Storage.BindResource(0, source, offset, length);
        return owner;
    }
}

/// <summary>Selects one instance's bounded animation cursors and optional graphics authority.</summary>
public sealed record NativeFieldInstanceResourceReference([property: JsonRequired] int ResourceIndex, [property: JsonRequired] int InstanceIndex)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? GraphicsLength { get; init; }
}

/// <summary>Resource ownership on an authenticated numeric registration-table receipt.</summary>
public sealed record NativeFieldResourcePointerStorage([property: JsonRequired] int ResourceIndex)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldResourceOwner? Resource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldResourceOwner? InstanceBindings { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? GraphicsLength { get; init; }

    internal bool IsValidAt(uint address) => Resource is not null ? address == 0x1DCB0F0u + (uint)ResourceIndex * 4 : address >= 0x1DCB340u && address < 0x1DCB3C0u && (address - 0x1DCB340u) % 4 == 0;

    internal bool IsValid
    {
        get
        {
            if (ResourceIndex is < 0 or >= 64 || (Resource is null) == (InstanceBindings is null) || Resource is not null && GraphicsLength is not null || InstanceBindings is { } instance && (GraphicsLength is int length ? length < 152 || instance.Length != length : instance.Length != 152))
            {
                return false;
            }
            (Resource ?? InstanceBindings)!.Validate();
            return Resource is null || Resource.Length >= 96;
        }
    }
}

internal readonly record struct NativeFieldResourceCursor(NativeFieldResourceOwner? Owner, uint Offset)
{
    internal static NativeFieldResourceCursor Guest(uint address) => new(null, address);
    public static NativeFieldResourceCursor operator +(NativeFieldResourceCursor cursor, uint offset) => cursor with { Offset = checked(cursor.Offset + offset) };
    public static NativeFieldResourceCursor operator -(NativeFieldResourceCursor cursor, int offset) => cursor + checked(-offset);
    public static NativeFieldResourceCursor operator +(NativeFieldResourceCursor cursor, int offset) => cursor with { Offset = checked((uint)((long)cursor.Offset + offset)) };
}
