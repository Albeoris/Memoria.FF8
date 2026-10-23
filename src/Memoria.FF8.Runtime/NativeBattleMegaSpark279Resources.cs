using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMegaSpark279Resources(NativeBattleGfAnimationSource ProgrammeA, NativeBattleGfAnimationSource ProgrammeB, NativeBattleGfAnimationSource ProgrammeC, NativeBattleGfAnimationSource ProgrammeD, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> SourcePoints24, ImmutableArray<byte> ExtraPoint8, ImmutableArray<byte> AttachmentTables, ImmutableArray<byte> Distance16384, ImmutableArray<byte> NormalizationConsumed384, ImmutableArray<byte> SinePrefix4098)
{
    public void Validate()
    {
        RequireProgramme(ProgrammeA, ProgrammeAAddress, ProgrammeALength, ProgrammeAWorkspaceLength, 27, 90);
        RequireProgramme(ProgrammeB, ProgrammeBAddress, ProgrammeBLength, ProgrammeBWorkspaceLength, 5, 16);
        RequireProgramme(ProgrammeC, ProgrammeCAddress, ProgrammeCLength, ProgrammeCWorkspaceLength, 2, 12);
        RequireProgramme(ProgrammeD, ProgrammeDAddress, ProgrammeDLength, ProgrammeDWorkspaceLength, 4, 18);
        if (Camera is null || Camera.Identity != "FF8_EN:10B1A2C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 32 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641A3)
        {
            throw new InvalidDataException("Mega Spark requires its complete original camera and positioned sound descriptor.");
        }
        if (SourcePoints24.IsDefault || SourcePoints24.Length != 24 || ExtraPoint8.IsDefault || ExtraPoint8.Length != 8 || AttachmentTables.IsDefault || AttachmentTables.Length != 576 || Distance16384.IsDefault || Distance16384.Length != DistanceLength || NormalizationConsumed384.IsDefault || NormalizationConsumed384.Length != NormalizationLength || SinePrefix4098.IsDefault || SinePrefix4098.Length != SineLength)
        {
            throw new InvalidDataException("Mega Spark requires its complete packed points, attachment rows and consumed lookup fronts.");
        }
        int[] selectors = [17, 18, 21];
        for (int index = 0; index < selectors.Length; index++)
        {
            if (BinaryPrimitives.ReadInt16LittleEndian(SourcePoints24.AsSpan().Slice(index * 8 + 6)) != selectors[index])
            {
                throw new InvalidDataException("Mega Spark packed point bone selectors changed.");
            }
        }
        if (BinaryPrimitives.ReadInt16LittleEndian(ExtraPoint8.AsSpan().Slice(6)) != 10)
        {
            throw new InvalidDataException("Mega Spark requires its selected extra-point bone.");
        }
        RequireSprite(Sprite, SpriteAddress, SpriteLength, 10);
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                _ = ReadGeometry(checked(source.Address + Dword(source.Data, 8 + index * 4)));
            }
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address switch
    {
        ProgrammeAAddress => ProgrammeA,
        ProgrammeBAddress => ProgrammeB,
        ProgrammeCAddress => ProgrammeC,
        ProgrammeDAddress => ProgrammeD,
        _ => throw new NotSupportedException("Mega Spark selected an unsupported original programme.")
    };

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
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
                if (vertices is 0 or > 64 || shapes is 0 or > 16 || polygon != checked(12 + (int)vertices * (int)shapes * 8))
                {
                    throw new InvalidDataException("Mega Spark requires its original twelve-byte multi-shape geometry front.");
                }
                int cursor = checked(offset + polygon);
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(source.Data, cursor);
                    if (count > 1000 || count != 0 && group is not (2 or 3 or 4 or 5 or 6 or 7))
                    {
                        throw new NotSupportedException("Mega Spark selected an unsupported original geometry group.");
                    }
                    int end = checked(cursor + 4 + (int)count * strides[group]);
                    if (end > source.Data.Length)
                    {
                        throw new InvalidDataException("Mega Spark geometry leaves its original programme source.");
                    }
                    for (int face = 0; face < count; face++)
                    {
                        int first = checked(cursor + 4 + face * strides[group]);
                        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                        {
                            uint vertexOffset = (uint)BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(first + 4 + corner * 2, 2)) * 4;
                            if ((vertexOffset & 7) != 0 || vertexOffset > vertices * 8 - 8)
                            {
                                throw new InvalidDataException("Mega Spark face index leaves its selected original vertex shape.");
                            }
                        }
                    }
                    cursor = end;
                }
                return new(0, address, source.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException("Mega Spark selected an unsupported addressed geometry front.");
    }

    public NativeBattleModuleResource ReadSprite(uint address) => address == SpriteAddress ? Sprite : throw new NotSupportedException("Mega Spark selected an unsupported projected sprite.");

    public ImmutableArray<byte> ReadAttachmentRow(int table, int row)
    {
        if ((uint)table >= 3 || (uint)row >= 8)
        {
            throw new NotSupportedException("Mega Spark attachment rows leave their selected tables.");
        }
        return AttachmentTables.AsSpan().Slice(table * AttachmentTableLength + row * AttachmentRowLength, AttachmentRowLength).ToArray().ToImmutableArray();
    }

    public byte ReadDistanceByte(int row, int column)
    {
        if ((uint)row >= 128 || (uint)column >= 128)
        {
            throw new NotSupportedException("Mega Spark distance coordinates require the native clamped domain.");
        }
        return Distance16384[(row << 7) + column];
    }

    public ushort ReadNormalizationWord(int index)
    {
        if (index is < 64 or > 255)
        {
            throw new NotSupportedException("Mega Spark normalization index leaves the consumed nonzero domain.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(NormalizationConsumed384.AsSpan().Slice((index - 64) * 2, 2));
    }

    public short ReadSineWord(int index)
    {
        if (index is < 256 or > 2048 || (index & 255) != 0)
        {
            throw new NotSupportedException("Mega Spark sine index has no supported progress producer.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(SinePrefix4098.AsSpan().Slice(index * 2, 2));
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Mega Spark static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Mega Spark static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        foreach (var source in new[] { ProgrammeA, ProgrammeB, ProgrammeC, ProgrammeD })
        {
            yield return (source.Address, source.Data);
        }
        yield return (SpriteAddress, Sprite.Bytes);
        yield return (CameraAddress, Camera.Data);
        yield return (SourcePointsAddress, SourcePoints24);
        yield return (ExtraPointAddress, ExtraPoint8);
        yield return (AttachmentTablesAddress, AttachmentTables);
        yield return (DistanceAddress, Distance16384);
        yield return (NormalizationAddress, NormalizationConsumed384);
        yield return (BodyNormalizationAddress, NormalizationConsumed384);
        yield return (SineAddress, SinePrefix4098);
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, byte kind)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || Dword(sprite.Bytes, 0) != ((uint)kind << 24 | 0x010524) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 8)
        {
            throw new InvalidDataException("Mega Spark requires its original selected projected sprite front.");
        }
        for (int frame = 0; frame < 8; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
    }

    private static void RequireProgramme(NativeBattleGfAnimationSource source, uint address, int length, int workspace, int nodes, int frames)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length || Dword(source.Data, 0) != (address == ProgrammeAAddress ? 5u : address == ProgrammeBAddress ? 3u : 2u))
        {
            throw new InvalidDataException("Mega Spark requires complete original addressed programme sources.");
        }
        int table = checked((int)Dword(source.Data, 4));
        if (Dword(source.Data, table) != nodes || Dword(source.Data, table + 4) != frames)
        {
            throw new InvalidDataException("Mega Spark requires its original selected node counts and programme durations.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Mega Spark resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
