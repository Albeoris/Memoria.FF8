using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Bio's retained settings, lighting and bounded physical vertex-work partition.</summary>
public sealed record NativeBattleBio22ActorMemory([property: JsonRequired] NativeBattleEffectArena Settings, [property: JsonRequired] NativeBattleEffectArena Lighting, [property: JsonRequired] NativeBattleEffectArena Projected)
{
    public const uint SettingsAddress = 0x2714E30;
    public const uint LightingAddress = 0x2714DE8;
    public const uint ProjectedAddress = 0x270E560;
    public const int SettingsLength = 0xF4;
    public const int LightingLength = 0x26;
    public const int ProjectedLength = 0x6400;

    public static NativeBattleBio22ActorMemory FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleBio22ActorMemory Unknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, SettingsLength), NativeBattleEffectArena.Clear().MarkUnknown(0, LightingLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ProjectedLength));

    public void Validate()
    {
        ValidateStorage(Settings, SettingsLength);
        ValidateStorage(Lighting, LightingLength);
        ValidateStorage(Projected, ProjectedLength);
    }

    public byte[] ReadSettings(int offset, int length) => Read(Settings, offset, length, SettingsLength);
    public byte[] ReadLighting(int offset, int length) => Read(Lighting, offset, length, LightingLength);
    public byte[] ReadProjected(int offset, int length) => Read(Projected, offset, length, ProjectedLength);
    public NativeBattleBio22ActorMemory WriteSettings(int offset, ReadOnlySpan<byte> bytes) => this with { Settings = Write(Settings, offset, bytes, SettingsLength) };
    public NativeBattleBio22ActorMemory WriteLighting(int offset, ReadOnlySpan<byte> bytes) => this with { Lighting = Write(Lighting, offset, bytes, LightingLength) };
    public NativeBattleBio22ActorMemory WriteProjected(int offset, ReadOnlySpan<byte> bytes) => this with { Projected = Write(Projected, offset, bytes, ProjectedLength) };
    public ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(ReadSettings(offset, 2));
    public short SignedWord(int offset) => unchecked((short)Word(offset));
    public uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSettings(offset, 4));
    public NativeBattleBio22ActorMemory WriteWord(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return WriteSettings(offset, bytes);
    }
    public NativeBattleBio22ActorMemory WriteDword(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return WriteSettings(offset, bytes);
    }

    private static void ValidateStorage(NativeBattleEffectArena storage, int length)
    {
        if (storage is null)
        {
            throw new InvalidDataException("Native Bio actor work requires each explicit retained physical owner.");
        }
        storage.Validate();
        storage.ValidateExtent(length);
    }
    private static byte[] Read(NativeBattleEffectArena storage, int offset, int length, int extent)
    {
        Range(offset, length, extent);
        return storage.Read(offset, length);
    }
    private static NativeBattleEffectArena Write(NativeBattleEffectArena storage, int offset, ReadOnlySpan<byte> bytes, int extent)
    {
        Range(offset, bytes.Length, extent);
        return storage.Write(offset, bytes);
    }
    private static void Range(int offset, int length, int extent)
    {
        if (offset < 0 || length < 0 || offset > extent - length)
        {
            throw new NotSupportedException("Native Bio actor access crosses a separately owned physical partition; alias mapping is required.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22ActorMemory? Bio22ActorMemory { get; init; }
}
