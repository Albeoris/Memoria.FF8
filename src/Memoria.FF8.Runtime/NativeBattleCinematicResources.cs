using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public interface INativeBattleScriptedTextureProfile
{
    int TextureCount { get; }
    uint InitializationAddress { get; }
    uint OwnerDirectoryAddress { get; }
    void Validate();
    ReadOnlySpan<byte> ReadStatic(uint address, int length);
    uint InitializationOffset(int byteOffset);
    NativeBattleEffectTextureRectangle Rectangle(int index, bool clut);
    byte Source(int index, bool clut);
}

public sealed record NativeBattleCinematicTextureProfile([property: JsonRequired] int Count, [property: JsonRequired] uint InitializationAddress, [property: JsonRequired] uint OwnerDirectoryAddress, [property: JsonRequired] uint PixelRectangleAddress, [property: JsonRequired] uint ClutRectangleAddress, [property: JsonRequired] uint PixelSourceMapAddress, [property: JsonRequired] uint ClutSourceMapAddress);

/// <summary>One immutable static owner supplies the cinematic directories and their selected texture tables.</summary>
public sealed record NativeBattleCinematicResources([property: JsonRequired] uint StaticAddress, [property: JsonRequired] NativeBattleEffectHeapAllocation StaticData, [property: JsonRequired] NativeBattleCinematicTextureProfile TextureProfile, [property: JsonRequired] NativeVibrationBank? Vibration) : INativeBattleScriptedTextureProfile
{
    public ImmutableArray<NativeBattleArchiveFile> Files { get; init; } = [];

    int INativeBattleScriptedTextureProfile.TextureCount => TextureProfile.Count;
    uint INativeBattleScriptedTextureProfile.InitializationAddress => TextureProfile.InitializationAddress;
    uint INativeBattleScriptedTextureProfile.OwnerDirectoryAddress => TextureProfile.OwnerDirectoryAddress;

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefaultOrEmpty || TextureProfile is null || TextureProfile.Count <= 0 || TextureProfile.Count > 256)
        {
            throw new InvalidDataException("Native cinematic resources require their complete static owner, texture descriptor and vibration bank.");
        }
        ReadStatic(TextureProfile.InitializationAddress, 0x20);
        ReadStatic(TextureProfile.OwnerDirectoryAddress, 0x14);
        ReadStatic(TextureProfile.PixelRectangleAddress, checked(TextureProfile.Count * 8));
        ReadStatic(TextureProfile.ClutRectangleAddress, checked(TextureProfile.Count * 8));
        ReadStatic(TextureProfile.PixelSourceMapAddress, TextureProfile.Count);
        ReadStatic(TextureProfile.ClutSourceMapAddress, TextureProfile.Count);
        foreach ((int offset, uint expected) in new[] { (0, TextureProfile.PixelRectangleAddress), (4, TextureProfile.ClutRectangleAddress), (8, TextureProfile.PixelSourceMapAddress), (12, TextureProfile.ClutSourceMapAddress) })
        {
            if (checked(TextureProfile.InitializationAddress + InitializationOffset(offset)) != expected)
            {
                throw new InvalidDataException("Native cinematic texture descriptor differs from its original initialization directory.");
            }
        }
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForInitializationAddress(TextureProfile.InitializationAddress);
        if (profile.HasVibrationDirectory != (Vibration is not null))
        {
            throw new InvalidDataException("Native cinematic vibration availability differs from its selected original resource profile.");
        }
        Vibration?.Validate();
    }

    public ReadOnlySpan<byte> ReadStatic(uint address, int length)
    {
        long offset = (long)address - StaticAddress;
        if (StaticData is null || StaticData.Data.IsDefault || length < 0 || offset < 0 || offset > StaticData.Data.Length - (long)length)
        {
            throw new NotSupportedException("Native cinematic static access leaves its authenticated owner.");
        }
        return StaticData.Data.AsSpan().Slice((int)offset, length);
    }

    public uint InitializationOffset(int byteOffset)
    {
        if (byteOffset < 0 || byteOffset > 0x1C || (byteOffset & 3) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(checked(TextureProfile.InitializationAddress + (uint)byteOffset), 4));
    }

    public NativeBattleEffectTextureRectangle Rectangle(int index, bool clut)
    {
        RequireIndex(index);
        uint pointer = clut ? TextureProfile.ClutRectangleAddress : TextureProfile.PixelRectangleAddress;
        ReadOnlySpan<byte> bytes = ReadStatic(checked(pointer + (uint)index * 8), 8);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
    }

    public byte Source(int index, bool clut)
    {
        RequireIndex(index);
        byte source = ReadStatic(checked((clut ? TextureProfile.ClutSourceMapAddress : TextureProfile.PixelSourceMapAddress) + (uint)index), 1)[0];
        return clut ? source : (byte)(source & 0x7F);
    }

    private void RequireIndex(int index)
    {
        if ((uint)index >= TextureProfile.Count)
        {
            throw new NotSupportedException("Native cinematic texture index leaves its authenticated descriptor.");
        }
    }
}
