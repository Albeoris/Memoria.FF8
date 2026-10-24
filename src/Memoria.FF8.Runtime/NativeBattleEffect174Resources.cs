using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect174Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> ProjectedSprites, ImmutableArray<uint> HistoryColors, ImmutableArray<uint> MaterialCycle12, uint Sound, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect174 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => ProjectedSprites.TryGetValue(address, out NativeBattleModuleResource? sprite) ? sprite : throw new NotSupportedException($"Effect174 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect174 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag173.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Meshes is null || Meshes.Count != MeshAddresses.Length || ProjectedSprites is null || ProjectedSprites.Count != SpriteAddresses.Length || HistoryColors.IsDefault || HistoryColors.Length != 5 || MaterialCycle12.IsDefault || MaterialCycle12.Length != 12 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect174 requires its complete selected original resources.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = ReadMesh(MeshAddresses[index]);
            if (source.Address != MeshAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != MeshLengths[index] || ValidateMesh(source.Bytes) != source.Bytes.Length)
            {
                throw new InvalidDataException("Effect174 mesh leaves its original physical extent.");
            }
        }
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != SpriteFrameCounts[index])
            {
                throw new InvalidDataException("Effect174 sprite leaves its original physical extent.");
            }
            for (int frame = 0; frame < SpriteFrameCounts[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        for (int index = 0; index < HistoryColors.Length; index++)
        {
            if (HistoryColors[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(HistoryColorsAddress + (uint)index * 4, 4)))
            {
                throw new InvalidDataException("Effect174 history color disagrees with its addressed source.");
            }
        }
        for (int index = 0; index < MaterialCycle12.Length; index++)
        {
            if (MaterialCycle12[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(MaterialCycleAddress + (uint)index * 4, 4)))
            {
                throw new InvalidDataException("Effect174 material cycle disagrees with its addressed source.");
            }
        }
        if (Sound != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)))
        {
            throw new InvalidDataException("Effect174 sound disagrees with its addressed source.");
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != CameraModelOffset || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect174 camera directory leaves its original input.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > (data.Length - 8) / 8 || cursor != checked(8 + vertices * 8))
        {
            throw new InvalidDataException("Effect174 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect174 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect174 face index leaves its physical vertex rows.");
                    }
                }
            }
            cursor += count * strides[group];
        }
        return cursor;
    }

    private static void ValidateTim(ImmutableArray<byte> data, uint flags)
    {
        if (Dword(data, 0) != 16 || Dword(data, 4) != flags)
        {
            throw new InvalidDataException("Effect174 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect174 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect174 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect174 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect174 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
