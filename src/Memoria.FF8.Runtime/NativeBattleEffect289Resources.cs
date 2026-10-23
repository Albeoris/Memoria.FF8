using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect289Resources(NativeBattleGfAnimationSource MainProgramme, NativeBattleGfAnimationSource ChildProgramme, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> CasterPoint8, ImmutableArray<byte> Lookup8162)
{
    public void Validate()
    {
        RequireProgramme(MainProgramme, MainProgrammeAddress, MainProgrammeLength, MainProgrammeWorkspaceLength, 2, 4, 20);
        RequireProgramme(ChildProgramme, ChildProgrammeAddress, ChildProgrammeLength, ChildProgrammeWorkspaceLength, 1, 1, 15);
        if (Meshes.IsDefault || Meshes.Length != 2 || Sprites.IsDefault || Sprites.Length != 3)
        {
            throw new InvalidDataException("Effect289 requires its two original beam sources and three projected sprites.");
        }
        for (int index = 0; index < Meshes.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = Meshes[index];
            if (mesh is null || mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || Dword(mesh.Bytes, 0) != 8 + MeshVertexCount * 8 || Dword(mesh.Bytes, 4) != MeshVertexCount)
            {
                throw new InvalidDataException("Effect289 requires its complete selected eight-byte beam geometry.");
            }
            int cursor = 8 + MeshVertexCount * 8;
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int expected = group == 3 ? 74 : group == 7 ? 6 : 0;
                if (Dword(mesh.Bytes, cursor) != expected)
                {
                    throw new InvalidDataException("Effect289 original beam group counts changed.");
                }
                cursor += 4;
                for (int face = 0; face < expected; face++)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        uint offset = (uint)Word(mesh.Bytes, cursor + face * strides[group] + 4 + corner * 2) * 4;
                        if ((offset & 7) != 0 || offset >= MeshVertexCount * 8)
                        {
                            throw new InvalidDataException("Effect289 beam corner leaves its original vertex rows.");
                        }
                    }
                }
                cursor = checked(cursor + expected * strides[group]);
            }
            if (cursor != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect289 beam extent changed.");
            }
        }
        uint[] headers = [0x09010524, 0x07010524, 0x07010524];
        int[] frames = [12, 12, 16];
        for (int index = 0; index < Sprites.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite is null || sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Dword(sprite.Bytes, 0) != headers[index] || Word(sprite.Bytes, 8) != frames[index])
            {
                throw new InvalidDataException("Effect289 requires its complete original projected sprite directory.");
            }
            for (int phase = 0; phase < frames[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
                if (frame.Quads.Length != 20 || frame.ExtendedPacket)
                {
                    throw new InvalidDataException("Effect289 sprite frame changed its selected quad contract.");
                }
            }
        }
        if (Camera is null || Camera.Identity != "FF8_EN:10724E0" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 52 || Word(Camera.Data, 6) != CameraLength || Sound != 0x61B85 || CasterPoint8.IsDefault || CasterPoint8.Length != 8 || Word(CasterPoint8, 6) != 9 || Lookup8162.IsDefault || Lookup8162.Length != LookupLength)
        {
            throw new InvalidDataException("Effect289 requires its addressed camera, sound, caster point and bounded lookup.");
        }
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, ChildProgramme })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        MainProgrammeAddress => MainProgramme,
        ChildProgrammeAddress => ChildProgramme,
        _ => throw new NotSupportedException("Effect289 selected an unsupported programme.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Effect289 selected an unsupported beam source.");
    public NativeBattleStaticMeshResource ReadMesh(NativeBattleEffect289State state, uint address)
    {
        NativeBattleStaticMeshResource source = ReadMesh(address);
        return source with { Bytes = state.ReadStatic(this, address, source.Bytes.Length).Read(0, source.Bytes.Length).ToImmutableArray() };
    }
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Effect289 selected an unsupported projected sprite.");
    public short ReadLookup(int index)
    {
        if ((uint)index > 4080)
        {
            throw new InvalidDataException("Effect289 lookup index leaves its exact source W domain.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(Lookup8162.AsSpan().Slice(index * 2, 2));
    }

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, ChildProgramme })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                uint vertices = Dword(source.Data, offset + 4);
                uint shapes = Dword(source.Data, offset + 8);
                int polygon = checked((int)Dword(source.Data, offset));
                if (vertices is 0 or > 68 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Effect289 requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [14, 18, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (4 or 5 or 7))
                    {
                        throw new NotSupportedException("Effect289 selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Effect289 geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect289 face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect289 selected an unsupported addressed geometry front.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect289 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect289 static read leaves its selected addressed resource fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        yield return (MainProgramme.Address, MainProgramme.Data);
        yield return (ChildProgramme.Address, ChildProgramme.Data);
        foreach (NativeBattleStaticMeshResource mesh in Meshes)
        {
            yield return (mesh.Address, mesh.Bytes);
        }
        foreach (NativeBattleModuleResource sprite in Sprites)
        {
            yield return (sprite.Address, sprite.Bytes);
        }
        yield return (CameraAddress, Camera.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (PointAddress, CasterPoint8);
        yield return (LookupAddress, Lookup8162);
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int geometries, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != geometries)
        {
            throw new InvalidDataException("Effect289 requires its complete selected programme source.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Effect289 programme node count or duration changed.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
}
