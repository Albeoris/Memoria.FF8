using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48Resources(NativeBattleCuragaExecutableSource Executable, NativeBattleModuleResource StaticData, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> MeshSeeds, ImmutableArray<NativeBattleModuleResource> Shapes, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<uint> ColorScript)
{
    private static readonly uint[] NodeAddresses = [0x15b7320, 0x15b7478, 0x15b75d0, 0x15b7728, 0x15b7880, 0x15b79d8, 0x15b7b30];
    private static readonly uint[] SpriteAddresses = [0x15adc6c, 0x15ade18, 0x15adfc4, 0x15ae170, 0x15ae31c];
    private static readonly int[] SpriteLengths = [428, 428, 428, 428, 480];
    private static readonly int[] SpriteFrames = [16, 16, 16, 16, 18];
    private static readonly uint[] ShapeAddresses = [0x15b252c, 0x15b309c, 0x15b3654, 0x15b3c0c, 0x15b41c4, 0x15b477c, 0x15b4d34, 0x15b52ec, 0x15b58a4, 0x15b5e5c];

    public void Validate()
    {
        if (Executable is null || Executable.ResourceAddress != SourceAddress || Executable.Data.IsDefault || Executable.Data.Length != SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 1 || StaticData is null || StaticData.Address != StaticAddress || StaticData.Bytes.IsDefault || StaticData.Bytes.Length != StaticLength || Sprites.IsDefault || Sprites.Length != 5 || MeshSeeds.IsDefault || MeshSeeds.Length != 2 || Shapes.IsDefault || Shapes.Length != 10 || ColorScript.IsDefault || ColorScript.Length != 16)
        {
            throw new InvalidDataException("Magma Breath requires its actual flag-one absolute source and complete addressed resource fronts.");
        }
        if (Camera is null || Camera.Identity != "FF8_EN:15B7D8C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 52 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641B8 || ColorScript[7] != 0xFE320000 || ColorScript[15] != 0xFF000000)
        {
            throw new InvalidDataException("Magma Breath requires its complete camera, sound and bounded color programme.");
        }
        for (int index = 0; index < Sprites.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != SpriteFrames[index])
            {
                throw new InvalidDataException("Magma Breath sprite front disagrees with its actual addressed frame directory.");
            }
            RequireSeed(sprite.Address, sprite.Bytes);
            for (int phase = 0; phase < SpriteFrames[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
                for (int quad = 0; quad < frame.Quads.Length / 20; quad++)
                {
                    if ((BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan().Slice(quad * 20 + 14)) >> 12) >= 4)
                    {
                        throw new InvalidDataException("Magma Breath sprite palette selector exceeds its original four lanes.");
                    }
                }
            }
        }
        MeshResource(MeshSeeds[0], MeshAAddress, MeshLength, 182, [0, 0, 0, 0, 0, 0, 24, 168]);
        MeshResource(MeshSeeds[1], MeshBAddress, MeshLength, 182, [0, 0, 0, 0, 0, 0, 24, 168]);
        foreach (NativeBattleStaticMeshResource mesh in MeshSeeds)
        {
            RequireSeed(mesh.Address, mesh.Bytes);
        }
        for (int index = 0; index < Shapes.Length; index++)
        {
            NativeBattleModuleResource shape = Shapes[index];
            if (shape.Address != ShapeAddresses[index] || shape.Bytes.IsDefault || shape.Bytes.Length != 1464 || BinaryPrimitives.ReadUInt32LittleEndian(shape.Bytes.AsSpan()) != 1464 || BinaryPrimitives.ReadUInt32LittleEndian(shape.Bytes.AsSpan().Slice(4)) != 182)
            {
                throw new InvalidDataException("Magma Breath requires its ten original vertex-only shape fronts.");
            }
            RequireSeed(shape.Address, shape.Bytes);
        }
        int[] populated = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan().Slice(4 + table * 64 + index * 4));
                if (pointer == 0)
                {
                    continue;
                }
                populated[table]++;
                switch (table)
                {
                    case 0:
                        if (!NodeAddresses.Contains(pointer))
                        {
                            throw new InvalidDataException("Magma Breath node selector leaves its original absolute directory.");
                        }
                        _ = ReadStatic(pointer, 344);
                        break;
                    case 1:
                        _ = ReadSprite(pointer);
                        break;
                    case 2:
                        _ = ReadMesh(pointer);
                        break;
                    case 3:
                        _ = ReadShape(pointer);
                        break;
                }
            }
        }
        if (!populated.SequenceEqual(new[] { 7, 6, 2, 10 }))
        {
            throw new InvalidDataException("Magma Breath absolute directories lost their original nullable entries.");
        }
        RequireSeed(SourceAddress, Executable.Data);
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => MeshSeeds.FirstOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Magma Breath selected an unsupported original mesh seed.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.FirstOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Magma Breath selected an unsupported original sprite.");
    public NativeBattleModuleResource ReadShape(uint address) => Shapes.FirstOrDefault(shape => shape.Address == address) ?? throw new NotSupportedException("Magma Breath selected an unsupported original shape.");
    public ImmutableArray<byte> ReadResource(uint address) => Sprites.Concat(Shapes).FirstOrDefault(resource => resource.Address == address)?.Bytes ?? throw new NotSupportedException("Magma Breath immutable resource lookup excludes current mutable mesh owners.");
    public byte[] ReadStatic(uint address, int length)
    {
        if (length >= 0 && address >= ColorScriptAddress && (ulong)address + (uint)length <= ColorScriptAddress + 64UL)
        {
            return ColorScript.SelectMany(BitConverter.GetBytes).Skip(checked((int)(address - ColorScriptAddress))).Take(length).ToArray();
        }
        if (length < 0 || Overlaps(MeshAAddress, MutablePeLength) || Overlaps(SourceAddress, SourceLength) || !ImmutableRegions.Any(region => address >= region.Address && (ulong)address + (uint)length <= (ulong)region.Address + (uint)region.Length))
        {
            throw new InvalidDataException($"Magma Breath immutable read {address:X8}/{length} excludes current PE, loader channels and unobserved static gaps.");
        }
        return StaticData.Bytes.AsSpan().Slice(checked((int)(address - StaticAddress)), length).ToArray();

        bool Overlaps(uint first, int count) => address < (ulong)first + (uint)count && (ulong)address + (uint)Math.Max(length, 0) > first;
    }
    private void RequireSeed(uint address, ImmutableArray<byte> bytes)
    {
        int offset = checked((int)(address - StaticAddress));
        if (offset < 0 || bytes.IsDefault || offset > StaticData.Bytes.Length - bytes.Length || !StaticData.Bytes.AsSpan().Slice(offset, bytes.Length).SequenceEqual(bytes.AsSpan()))
        {
            throw new InvalidDataException("Magma Breath original seed disagrees with its actual immutable executable source.");
        }
    }
    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Magma Breath requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Magma Breath mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Magma Breath mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Magma Breath mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
