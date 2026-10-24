using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect68Resources(NativeBattleEffectInitializerAsset MainTim, uint Sound, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> VertexBanks, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect68 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect68 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public ImmutableArray<byte> ReadVertexBank(uint address) => VertexBanks.TryGetValue(address, out ImmutableArray<byte> source) ? source : throw new NotSupportedException($"Effect68 vertex source 0x{address:X8} is unsupported.");

    public NativeBattleStaticMeshResource ReadCurrentMesh(NativeBattleEffectMemory memory, uint address)
    {
        NativeBattleStaticMeshResource original = ReadMesh(address);
        if (!MutableMeshAddresses.Contains(address))
        {
            return original;
        }
        NativeBattleScanProcess scan = memory.Scan ?? throw new NotSupportedException("Effect68 requires its current canonical Scan mesh owners.");
        if (!scan.MutableMeshes.TryGetValue(address, out NativeBattleScanByteOwner? current))
        {
            throw new NotSupportedException("Effect68 mutable mesh requires its retained current bytes, not an original template fallback.");
        }
        current.RequireLength(original.Bytes.Length);
        if (current.Known.Any(known => !known))
        {
            throw new NotSupportedException("Effect68 raw mesh dispatch requires its current complete physical byte owner.");
        }
        return original with { Bytes = current.Bytes };
    }

    public NativeBattleScanCameraProfile ReadCameraProfile(ushort category)
    {
        if (category >= ProfileCount)
        {
            throw new InvalidDataException("Effect68 camera category leaves its original profile directory.");
        }
        uint address = checked(ProfileAddress + (uint)category * 16);
        return new(category, address, ReadStatic(address, 16).ToImmutableArray());
    }

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect68 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag039-067.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length || VertexBanks is null || VertexBanks.Count != VertexBankAddresses.Length || StaticFronts is null)
        {
            throw new InvalidDataException("Effect68 requires its complete original texture and selected resource directories.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes, false, null) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect68 raw mesh leaves its original physical input.");
            }
        }
        for (int index = 0; index < VertexBankAddresses.Length; index++)
        {
            ImmutableArray<byte> source = ReadVertexBank(VertexBankAddresses[index]);
            if (source.IsDefault || source.Length != VertexBankLengths[index] || Dword(source, 0) != source.Length || Dword(source, 0) != checked(8 + Dword(source, 4) * 8))
            {
                throw new InvalidDataException("Effect68 morph vertex bank leaves its exact packed rows.");
            }
        }
        int[] frames = [32, 4];
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != frames[index])
            {
                throw new InvalidDataException("Effect68 projected sprite dimensions changed.");
            }
            for (int frame = 0; frame < frames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(CameraPointerAddress, 4)) != CameraAddress || BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)) != Sound)
        {
            throw new InvalidDataException("Effect68 pointer and sound inputs disagree with their original cells.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (bytes.IsDefault || MutableMeshAddresses.Any(address => start < (ulong)address + (uint)ReadMesh(address).Bytes.Length && (ulong)start + (uint)bytes.Length > address))
            {
                throw new InvalidDataException("Effect68 immutable fronts overlap a current mutable mesh owner.");
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data, bool shapes, int[]? expected)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapeCount = shapes ? checked((int)Dword(data, 8)) : 1;
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > 160 || shapeCount <= 0 || shapeCount > 4 || cursor != checked((shapes ? 12 : 8) + vertices * shapeCount * 8))
        {
            throw new InvalidDataException("Effect68 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group] || expected is not null && count != expected[group])
            {
                throw new InvalidDataException("Effect68 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect68 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect68 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect68 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect68 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect68 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect68 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
