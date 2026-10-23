using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect283Resources(NativeBattleGfAnimationSource MainProgramme, NativeBattleGfAnimationSource SecondaryProgramme, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<uint> Palette8, ImmutableArray<byte> WarpTables21377)
{
    public void Validate()
    {
        RequireProgramme(MainProgramme, MainProgrammeAddress, MainProgrammeLength, MainProgrammeWorkspaceLength, 5, 17, 60);
        RequireProgramme(SecondaryProgramme, SecondaryProgrammeAddress, SecondaryProgrammeLength, SecondaryProgrammeWorkspaceLength, 2, 4, 36);
        if (Camera is null || Camera.Identity != "FF8_EN:1094FC0" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 32 || Word(Camera.Data, 6) != CameraLength || Sound != 0x61B71)
        {
            throw new InvalidDataException("Effect283 requires its complete addressed camera and sound.");
        }
        uint[] palette = [0x00005000, 0x00500000, 0x00500050, 0x00000050, 0x00005000, 0x00500000, 0x00500050, 0x00000050];
        if (Palette8.IsDefault || !Palette8.SequenceEqual(palette) || WarpTables21377.IsDefault || WarpTables21377.Length != WarpTablesLength)
        {
            throw new InvalidDataException("Effect283 requires its complete palette and overlapping warp tables.");
        }
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, SecondaryProgramme })
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
        SecondaryProgrammeAddress => SecondaryProgramme,
        _ => throw new NotSupportedException("Effect283 selected an unsupported programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { MainProgramme, SecondaryProgramme })
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
                if (vertices is 0 or > 128 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Effect283 requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [14, 18, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (3 or 4 or 5 or 6 or 7))
                    {
                        throw new NotSupportedException("Effect283 selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Effect283 geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Effect283 face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Effect283 selected an unsupported addressed geometry front.");
    }

    public ushort ReadWarpFactor(int selector, int index)
    {
        if (selector < 1 || selector > 20 || (uint)index >= 128)
        {
            throw new InvalidDataException("Effect283 factor pointer leaves its addressed static union.");
        }
        return Word(WarpTables21377, (selector - 1) * 256 + index * 2);
    }

    public byte ReadDistance(int index)
    {
        if ((uint)index >= DistanceLength)
        {
            throw new InvalidDataException("Effect283 distance pointer leaves its addressed static union.");
        }
        return WarpTables21377[19 * 256 + index];
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect283 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect283 static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        yield return (MainProgramme.Address, MainProgramme.Data);
        yield return (SecondaryProgramme.Address, SecondaryProgramme.Data);
        yield return (CameraAddress, Camera.Data);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (PaletteAddress, Palette8.SelectMany(BitConverter.GetBytes).ToImmutableArray());
        yield return (WarpTablesAddress, WarpTables21377);
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int geometries, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != geometries)
        {
            throw new InvalidDataException("Effect283 requires its complete selected programme source.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Effect283 programme node count or duration changed.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
}
