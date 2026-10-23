using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGravija214Resources(NativeBattleEffectHeapAllocation EntryCopySeed, NativeBattleGfAnimationSource Programme, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<ushort> SqrtTable, ImmutableArray<short> SineTable)
{
    public void Validate()
    {
        if (EntryCopySeed is null || EntryCopySeed.Identity != "FF8_EN:1172D00" || EntryCopySeed.Data.IsDefault || EntryCopySeed.Data.Length != EntryCopyLength || Dword(EntryCopySeed.Data, 0) != 1936 || Dword(EntryCopySeed.Data, 4) != VertexCount)
        {
            throw new InvalidDataException("Gravija requires its complete original MOVSD source and241-vertex mesh front.");
        }
        ValidateGroups(EntryCopySeed.Data.AsSpan().Slice(0, MeshLength).ToArray().ToImmutableArray(), 8, [0, 0, 0, 0, 24, 216, 0, 0]);
        if (Programme is null || Programme.Address != ProgrammeAddress || Programme.Data.IsDefault || Programme.Data.Length != ProgrammeLength || Dword(Programme.Data, 0) != 7)
        {
            throw new InvalidDataException("Gravija requires its complete seven-geometry programme.");
        }
        int table = checked((int)Dword(Programme.Data, 4));
        if (Dword(Programme.Data, table) != 11 || Dword(Programme.Data, table + 4) != 74)
        {
            throw new InvalidDataException("Gravija requires its eleven-node74-frame programme directory.");
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(Programme, ProgrammeWorkspaceLength);
        for (int index = 0; index < 7; index++)
        {
            _ = ReadGeometry(ProgrammeAddress + Dword(Programme.Data, 8 + index * 4));
        }
        if (Sprite is null || Sprite.Address != SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != SpriteLength || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 50)
        {
            throw new InvalidDataException("Gravija requires its complete fifty-phase projected sprite.");
        }
        for (int frame = 0; frame < 50; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
        if (Camera is null || Camera.Identity != "FF8_EN:1178938" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(2)) != 8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(4)) != 32 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x642BF || SqrtTable.IsDefault || SqrtTable.Length != 192 || SineTable.IsDefault || SineTable.Length != 4096)
        {
            throw new InvalidDataException("Gravija requires its original camera, positioned sound and bounded signed lookup owners.");
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address == ProgrammeAddress ? Programme : throw new NotSupportedException("Gravija selected an unsupported original programme.");
    public NativeBattleModuleResource ReadSprite(uint address) => address == SpriteAddress ? Sprite : throw new NotSupportedException("Gravija selected an unsupported original projected sprite.");

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        for (int index = 0; index < 7; index++)
        {
            int offset = checked((int)Dword(Programme.Data, 8 + index * 4));
            if (ProgrammeAddress + (uint)offset != address)
            {
                continue;
            }
            int[] lengths = [2348, 2348, 4396, 2348, 628, 628, 628];
            int[] vertices = [72, 72, 128, 72, 25, 25, 25];
            int[] shapes = [2, 2, 2, 1, 1, 1, 1];
            ImmutableArray<byte> bytes = Programme.Data.AsSpan().Slice(offset, lengths[index]).ToArray().ToImmutableArray();
            if (Dword(bytes, 4) != vertices[index] || Dword(bytes, 8) != shapes[index])
            {
                throw new InvalidDataException("Gravija geometry changed its original vertex or shape count.");
            }
            int[] groups = new int[8];
            groups[index == 2 ? 5 : index == 3 ? 7 : 3] = index == 2 ? 96 : index < 4 ? 48 : 16;
            ValidateGroups(bytes, 12, groups);
            return new(checked((byte)index), address, bytes);
        }
        throw new NotSupportedException("Gravija selected an unsupported original geometry front.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length >= 0 && address >= SqrtTableAddress && (ulong)address + (uint)length <= SqrtTableAddress + SqrtTableLength)
        {
            byte[] bytes = new byte[SqrtTableLength];
            for (int index = 0; index < SqrtTable.Length; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), SqrtTable[index]);
            }
            return bytes.AsSpan(checked((int)(address - SqrtTableAddress)), length).ToArray().ToImmutableArray();
        }
        if (length >= 0 && address >= SineTableAddress && (ulong)address + (uint)length <= SineTableAddress + SineTableLength)
        {
            byte[] bytes = new byte[SineTableLength];
            for (int index = 0; index < SineTable.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), SineTable[index]);
            }
            return bytes.AsSpan(checked((int)(address - SineTableAddress)), length).ToArray().ToImmutableArray();
        }
        throw new NotSupportedException("Gravija static lookup read leaves its actual immutable table fronts.");
    }

    private static void ValidateGroups(ImmutableArray<byte> bytes, int header, int[] expected)
    {
        int vertices = checked((int)Dword(bytes, 4));
        int shapes = header == 12 ? checked((int)Dword(bytes, 8)) : 1;
        int cursor = checked((int)Dword(bytes, 0));
        if (vertices <= 0 || shapes <= 0 || cursor != header + vertices * shapes * 8)
        {
            throw new InvalidDataException("Gravija requires its original packed vertex/shape directory.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(bytes, cursor));
            cursor = checked(cursor + 4);
            if (count != expected[group] || cursor > bytes.Length - count * strides[group])
            {
                throw new InvalidDataException("Gravija primitive groups leave their exact selected fronts.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2, 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Gravija primitive index leaves its current source shape.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("Gravija geometry must close its complete original front.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        if (bytes.IsDefault || offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Gravija resource read leaves its complete original source.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}
