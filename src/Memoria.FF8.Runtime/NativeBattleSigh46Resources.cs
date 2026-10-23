using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSigh46Resources(NativeBattleCuragaExecutableSource Executable, NativeBattleModuleResource StaticData, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> MeshSeeds, ImmutableArray<NativeBattleModuleResource> ColorTables, ImmutableArray<NativeBattleModuleResource> ColorStreams, ImmutableArray<short> Delays, ImmutableArray<short> Fade, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    private static readonly uint[] NodeAddresses = [0x15c2a30, 0x15c2b88, 0x15c2ce0, 0x15c2e38, 0x15c2f90, 0x15c30e8, 0x15c3240];
    private static readonly uint[] SpriteAddresses = [0x15b82a4, 0x15b8438, 0x15b85cc, 0x15b8830, 0x15b8a94, 0x15b8ba4];
    private static readonly int[] SpriteLengths = [404, 404, 612, 612, 272, 428];
    private static readonly int[] SpriteFrames = [15, 15, 23, 23, 10, 16];

    public void Validate()
    {
        if (Executable is null || Executable.ResourceAddress != SourceAddress || Executable.Data.IsDefault || Executable.Data.Length != SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 1 || StaticData is null || StaticData.Address != StaticAddress || StaticData.Bytes.IsDefault || StaticData.Bytes.Length != StaticLength || Sprites.IsDefault || Sprites.Length != 6 || MeshSeeds.IsDefault || MeshSeeds.Length != 2 || ColorTables.IsDefault || ColorTables.Length != 2 || ColorStreams.IsDefault || ColorStreams.Length != 34 || Delays.IsDefault || !Delays.SequenceEqual(new short[] { 2, 3, 4, 5, 4, 3, 2, 1, 0 }) || Fade.IsDefault || !Fade.SequenceEqual(new short[] { 512, 1024, 1536, 1792, 2048, 2304, 2560, 2816, 3072, 3328, 3456, 3584, 3712, 3840, 3968, 4096 }))
        {
            throw new InvalidDataException("Sigh requires its original flag-one source and complete bounded static fronts.");
        }
        if (Camera is null || Camera.Identity != "FF8_EN:15C349C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 24 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61AB4)
        {
            throw new InvalidDataException("Sigh requires its original camera and packed sound.");
        }
        RequireSeed(SourceAddress, Executable.Data);
        RequireSeed(CameraAddress, Camera.Data);
        RequireSeed(DelayAddress, Delays.SelectMany(BitConverter.GetBytes).ToImmutableArray());
        RequireSeed(FadeAddress, Fade.SelectMany(BitConverter.GetBytes).ToImmutableArray());
        for (int index = 0; index < Sprites.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != SpriteFrames[index])
            {
                throw new InvalidDataException("Sigh sprite selection leaves its original frame directory.");
            }
            RequireSeed(sprite.Address, sprite.Bytes);
            for (int phase = 0; phase < SpriteFrames[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
                for (int quad = 0; quad < frame.Quads.Length / 20; quad++)
                {
                    if ((BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan().Slice(quad * 20 + 14)) >> 12) >= 4)
                    {
                        throw new InvalidDataException("Sigh sprite palette selection exceeds the original four lanes.");
                    }
                }
            }
        }
        MeshResource(MeshSeeds[0], MeshAAddress, MeshLength, 77, [0, 0, 0, 0, 0, 0, 0, 60]);
        MeshResource(MeshSeeds[1], MeshBAddress, MeshLength, 77, [0, 0, 0, 0, 0, 0, 0, 60]);
        foreach (NativeBattleStaticMeshResource mesh in MeshSeeds)
        {
            RequireSeed(mesh.Address, mesh.Bytes);
        }
        for (int tableIndex = 0; tableIndex < 2; tableIndex++)
        {
            uint address = tableIndex == 0 ? ColorTableAAddress : ColorTableBAddress;
            NativeBattleModuleResource table = ColorTables[tableIndex];
            if (table.Address != address || table.Bytes.IsDefault || table.Bytes.Length != 68)
            {
                throw new InvalidDataException("Sigh requires both original seventeen-row color pointer tables.");
            }
            RequireSeed(address, table.Bytes);
            for (int row = 0; row < 17; row++)
            {
                uint expected = (tableIndex == 0 ? 0x15B8D50u : 0x15BCD54u) + (uint)(row * 960);
                if (BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan().Slice(row * 4)) != expected)
                {
                    throw new InvalidDataException("Sigh material row lost its actual original color-stream pointer.");
                }
                NativeBattleModuleResource stream = ColorStreams[tableIndex * 17 + row];
                if (stream.Address != expected || stream.Bytes.IsDefault || stream.Bytes.Length != 960)
                {
                    throw new InvalidDataException("Sigh material stream requires all sixty four-color faces.");
                }
                RequireSeed(stream.Address, stream.Bytes);
                for (int face = 0; face < 60; face++)
                {
                    if (stream.Bytes[face * 16 + 3] != 0x3E)
                    {
                        throw new InvalidDataException("Sigh current material colors require the original GT4 command byte.");
                    }
                }
            }
        }
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan().Slice(4 + table * 64 + index * 4));
                if (table < 2 && index < 7)
                {
                    if (table == 0)
                    {
                        if (pointer != NodeAddresses[index])
                        {
                            throw new InvalidDataException("Sigh source index leaves its original absolute node directory.");
                        }
                        byte[] node = ReadStatic(pointer, 344);
                        if (node[0x11] == 4 || node[0x12] > 40)
                        {
                            throw new NotSupportedException("Sigh source requires an unsupported animated-emitter channel path.");
                        }
                    }
                    else
                    {
                        _ = ReadSprite(pointer);
                    }
                }
                else if (pointer != 0)
                {
                    throw new InvalidDataException("Sigh generic mesh, shape and unused source directories must retain their original null entries.");
                }
            }
        }
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => MeshSeeds.FirstOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Sigh selected an unsupported original mesh seed.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.FirstOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Sigh selected an unsupported original sprite.");
    public ImmutableArray<byte> ReadResource(uint address) => ReadSprite(address).Bytes;
    public ImmutableArray<byte> ReadColorStream(uint tableAddress, int selector)
    {
        if ((uint)selector >= 17)
        {
            throw new InvalidDataException("Sigh signed color selector leaves its seventeen original rows.");
        }
        NativeBattleModuleResource table = ColorTables.FirstOrDefault(table => table.Address == tableAddress) ?? throw new NotSupportedException("Sigh selected an unsupported color pointer table.");
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan().Slice(selector * 4));
        return ColorStreams.FirstOrDefault(stream => stream.Address == pointer)?.Bytes ?? throw new InvalidDataException("Sigh color pointer lacks its complete original addressed stream.");
    }
    public byte[] ReadStatic(uint address, int length)
    {
        if (length < 0 || Overlaps(MeshAAddress, MutablePeLength) || Overlaps(SourceAddress, SourceLength) || !ImmutableRegions.Any(region => address >= region.Address && (ulong)address + (uint)length <= (ulong)region.Address + (uint)region.Length))
        {
            throw new InvalidDataException($"Sigh immutable read {address:X8}/{length} excludes current PE, loader channels and unobserved gaps.");
        }
        return StaticData.Bytes.AsSpan().Slice(checked((int)(address - StaticAddress)), length).ToArray();

        bool Overlaps(uint first, int count) => address < (ulong)first + (uint)count && (ulong)address + (uint)Math.Max(length, 0) > first;
    }
    private void RequireSeed(uint address, ImmutableArray<byte> bytes)
    {
        int offset = checked((int)(address - StaticAddress));
        if (offset < 0 || bytes.IsDefault || offset > StaticData.Bytes.Length - bytes.Length || !StaticData.Bytes.AsSpan().Slice(offset, bytes.Length).SequenceEqual(bytes.AsSpan()))
        {
            throw new InvalidDataException("Sigh original front disagrees with its addressed executable seed.");
        }
    }
    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Sigh requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Sigh mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Sigh mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Sigh mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
