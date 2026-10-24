using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect69Resources(NativeBattleGfAnimationSource Programme, ImmutableArray<byte> Selectors, NativeBattleEffectHeapAllocation Camera, NativeVibrationBank Vibration, uint Sound, NativeBattleModuleResource Sprite)
{
    public NativeBattleEffectFileAsset? StageFile37 { get; init; }
    [JsonIgnore]
    public Func<byte[], NativeBattleStageSource>? DecodeReloadStage { get; init; }

    public NativeBattleEffectFileAsset ReadFile(int fileId) => fileId == 37 && StageFile37 is { } file ? file : throw new NotSupportedException("Effect69 requires its selected original stage file input.");

    public void Validate()
    {
        if (Programme is null || Programme.Address != ProgrammeAddress || Programme.Data.IsDefault || Programme.Data.Length != ProgrammeLength || Dword(Programme.Data, 0) != 9 || Selectors.IsDefault || !Selectors.AsSpan().SequenceEqual("000100200"u8))
        {
            throw new InvalidDataException("Effect69 requires its complete nine-geometry programme and ASCII selector front.");
        }
        int table = checked((int)Dword(Programme.Data, 4));
        if (Dword(Programme.Data, table) != 27 || Dword(Programme.Data, table + 4) != 92)
        {
            throw new InvalidDataException("Effect69 requires its original node directory and complete duration.");
        }
        if (StageFile37 is { } file && (file.FileId != 37 || !string.Equals(file.Name, "A0STG037.X", StringComparison.OrdinalIgnoreCase) || file.Data.IsDefault || file.Data.Length != 218152))
        {
            throw new InvalidDataException("Effect69 requires its complete selected stage file input.");
        }
        NativeBattleGfAnimationWorkspace workspace = new(new byte[ProgrammeWorkspaceLength].ToImmutableArray());
        _ = workspace.Initialize7016B0(Programme, ProgrammeWorkspaceLength);
        for (int index = 0; index < 9; index++)
        {
            _ = ReadGeometry(checked(ProgrammeAddress + Dword(Programme.Data, 8 + index * 4)));
        }
        if (Camera is null || Camera.Identity != "FF8_EN:14D0858" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength || Sound != 0x64307)
        {
            throw new InvalidDataException("Effect69 requires its complete addressed camera and sound.");
        }
        if (Vibration is null || Vibration.Bytes.IsDefault || Vibration.Bytes.Length != VibrationLength || Vibration.Offsets.IsDefault || !Vibration.Offsets.SequenceEqual(new[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect69 requires its complete vibration bank.");
        }
        Vibration.Validate();
        if (Vibration.ReadWord(256) != 20 || Vibration.ReadWord(258) != 0 || Sprite is null || Sprite.Address != SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != SpriteLength || Word(Sprite.Bytes, 8) != 18)
        {
            throw new InvalidDataException("Effect69 requires its selected vibration channel and projected sprite.");
        }
        for (int frame = 0; frame < 18; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
    }

    public NativeBattleGfAnimationSource ReadProgramme(uint address) => address == ProgrammeAddress ? Programme : throw new NotSupportedException("Effect69 selected an unsupported programme.");

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        int[] vertices = [97, 49, 49, 120, 100, 21, 84, 33, 25];
        int[] shapes = [1, 1, 1, 2, 1, 2, 1, 1, 1];
        int[][] groups = [[24, 0, 0, 0, 0, 72, 0, 0], [0, 0, 0, 36, 0, 0, 0, 0], [0, 0, 0, 0, 16, 32, 0, 0], [0, 0, 0, 40, 0, 0, 0, 60], [0, 20, 0, 0, 0, 60, 0, 0], [0, 0, 0, 0, 0, 0, 0, 12], [0, 0, 0, 0, 0, 0, 0, 60], [0, 0, 0, 20, 0, 0, 0, 0], [0, 0, 0, 0, 24, 0, 0, 0]];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < 9; index++)
        {
            int offset = checked((int)Dword(Programme.Data, 8 + index * 4));
            if (checked(ProgrammeAddress + (uint)offset) != address)
            {
                continue;
            }
            int polygon = checked(12 + vertices[index] * shapes[index] * 8);
            if (Dword(Programme.Data, offset) != polygon || Dword(Programme.Data, offset + 4) != vertices[index] || Dword(Programme.Data, offset + 8) != shapes[index])
            {
                throw new InvalidDataException("Effect69 geometry changed its selected vertex or shape dimensions.");
            }
            int cursor = checked(offset + polygon);
            for (int group = 0; group < 8; group++)
            {
                int count = groups[index][group];
                if (Dword(Programme.Data, cursor) != count)
                {
                    throw new InvalidDataException("Effect69 geometry changed its selected primitive directory.");
                }
                for (int face = 0; face < count; face++)
                {
                    int first = checked(cursor + 4 + face * strides[group]);
                    for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                    {
                        int physicalOffset = checked(Word(Programme.Data, first + 4 + corner * 2) * 4);
                        if ((physicalOffset & 7) != 0 || physicalOffset > vertices[index] * 8 - 8)
                        {
                            throw new InvalidDataException("Effect69 face leaves its selected physical vertex rows.");
                        }
                    }
                }
                cursor = checked(cursor + 4 + count * strides[group]);
            }
            return new(0, address, Programme.Data.AsSpan().Slice(offset, cursor - offset).ToArray().ToImmutableArray());
        }
        throw new NotSupportedException("Effect69 selected an unsupported geometry address.");
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect69 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect69 static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        yield return (ProgrammeAddress, Programme.Data);
        yield return (SelectorsAddress, Selectors);
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Bytes);
        yield return (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray());
        yield return (SpriteAddress, Sprite.Bytes);
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Require(data, offset, 4));
    private static ushort Word(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Require(data, offset, 2));

    private static ReadOnlySpan<byte> Require(ImmutableArray<byte> data, int offset, int length)
    {
        if (data.IsDefault || offset < 0 || offset > data.Length - length)
        {
            throw new InvalidDataException("Effect69 resource read leaves its complete selected front.");
        }
        return data.AsSpan().Slice(offset, length);
    }
}
