using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWater222Profile([property: JsonRequired] NativeBattleEffectHeapAllocation Tables, [property: JsonRequired] NativeBattleEffectHeapAllocation InitializationDirectory, [property: JsonRequired] NativeBattleEffectHeapAllocation OwnerDirectory)
{
    public void Validate()
    {
        if (Tables is null || Tables.Data.IsDefault || Tables.Data.Length != 0x70 || InitializationDirectory is null || InitializationDirectory.Data.IsDefault || InitializationDirectory.Data.Length != 0x1C || OwnerDirectory is null || OwnerDirectory.Data.IsDefault || OwnerDirectory.Data.Length != 0x14)
        {
            throw new InvalidDataException("Native Water requires its complete rectangle, source-map and initialization directories.");
        }
    }

    public NativeBattleEffectTextureRectangle Rectangle(int index, bool clut)
    {
        Validate();
        if ((uint)index >= 5)
        {
            throw new NotSupportedException("Native Water texture index leaves its authenticated five-entry profile.");
        }
        int offset = (clut ? 40 : 0) + index * 8;
        return new(Word(offset), Word(offset + 2), Word(offset + 4), Word(offset + 6));
    }

    public byte Source(int index, bool clut)
    {
        Rectangle(index, clut);
        int source = Tables.Data[(clut ? 88 : 80) + index];
        source = clut ? source : source & 0x7F;
        if ((uint)source > 1)
        {
            throw new NotSupportedException("Native Water source map leaves its two current file owners.");
        }
        return (byte)source;
    }

    public uint InitializationOffset(int byteOffset)
    {
        Validate();
        if (byteOffset < 0 || byteOffset > 0x18 || (byteOffset & 3) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(InitializationDirectory.Data.AsSpan().Slice(byteOffset));
    }

    public uint OwnerAddress(int index)
    {
        Validate();
        if ((uint)index >= 5)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(OwnerDirectory.Data.AsSpan().Slice(index * 4));
    }

    private short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Tables.Data.AsSpan().Slice(offset));
}

public sealed record NativeBattleWater222Resources([property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation> Files, [property: JsonRequired] NativeBattleWater222Profile Profile)
{
    public void Validate()
    {
        Profile.Validate();
        if (Files.IsDefault || Files.Length != 2 || Files[0].Data.IsDefault || Files[0].Data.Length != 0x1799C || Files[1].Data.IsDefault || Files[1].Data.Length != 0x26258)
        {
            throw new InvalidDataException("Native Water requires both complete original resource files.");
        }
    }
}

public sealed record NativeBattleWater222UploadPlan(int SourceIndex, int Offset, NativeBattleEffectTextureRectangle Rectangle);
public sealed record NativeBattleWater222TextureStep(byte Count, uint InstructionPointer, bool Yield, ushort? Wait, ImmutableArray<NativeBattleWater222UploadPlan> Uploads);

public static class NativeBattleWater222TexturePair
{
    public static NativeBattleWater222TextureStep Transfer27(NativeBattleWater222Profile profile, ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles, int textureIndex, byte busy, byte count, uint instructionPointer, ushort currentWait, Action<NativeBattleWater222UploadPlan> submit)
    {
        if (busy != 0)
        {
            return new(count, instructionPointer, true, currentWait, []);
        }
        ImmutableArray<NativeBattleWater222UploadPlan>.Builder uploads = ImmutableArray.CreateBuilder<NativeBattleWater222UploadPlan>();
        NativeBattleWater222UploadPlan pixel = Resolve(profile, currentFiles, textureIndex, false);
        if (count >= 12)
        {
            return new(count, instructionPointer, true, currentWait, []);
        }
        count++;
        submit(pixel);
        uploads.Add(pixel);
        NativeBattleWater222UploadPlan clut = Resolve(profile, currentFiles, textureIndex, true);
        if (count >= 12)
        {
            return new(count, instructionPointer, true, currentWait, uploads.ToImmutable());
        }
        count++;
        submit(clut);
        uploads.Add(clut);
        return new(count, unchecked(instructionPointer + 4), false, null, uploads.ToImmutable());
    }

    public static NativeBattleWater222UploadPlan Resolve(NativeBattleWater222Profile profile, ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles, int textureIndex, bool clut)
    {
        NativeBattleEffectTextureRectangle rectangle = profile.Rectangle(textureIndex, clut);
        int source = profile.Source(textureIndex, clut);
        if (currentFiles.IsDefault || currentFiles.Length != 2 || currentFiles[source] is null || currentFiles[source].Data.IsDefault)
        {
            throw new NotSupportedException("Native Water transfer requires the selected current file bytes.");
        }
        ImmutableArray<byte> bytes = currentFiles[source].Data;
        uint directory = Dword(bytes, clut ? 8 : 0x14);
        uint relative = Dword(bytes, checked((int)directory + 4 * textureIndex));
        long offset = (long)directory + (clut ? relative : relative & 0xFFFFFF);
        long length = checked((long)rectangle.Width * rectangle.Height * 2);
        if (rectangle.Width < 0 || rectangle.Height < 0 || offset < 0 || offset > bytes.Length || length > bytes.Length - offset)
        {
            throw new InvalidDataException("Native Water transfer leaves its current source file.");
        }
        return new(source, checked((int)offset), rectangle);
    }

    public static NativeBattleEffectTextureState Enqueue505DF0(NativeBattleEffectTextureState textures, ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles, NativeBattleWater222UploadPlan plan) => textures.EnqueueRectangle505DF0(plan.Rectangle, currentFiles[plan.SourceIndex], plan.Offset);

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Native Water directory read leaves its current source file.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }
}
