using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect76Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect76Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Music, NativeBattleEffectHeapAllocation Vibration, uint Sound, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts, NativeBattleEffectFileAsset StageFile38)
{
    [JsonIgnore]
    public Func<byte[], NativeBattleStageSource>? DecodeReloadStage { get; init; }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect76 mesh 0x{address:X8} is unsupported.");
    public NativeBattleEffectFileAsset ReadFile(int fileId) => fileId == StageFileId && StageFile38 is { } file ? file : throw new NotSupportedException("Effect76 requires its selected original stage file input.");

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect76 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag075.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Music is null || Music.Data.IsDefault || Music.Data.Length != MusicLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Meshes is null || Meshes.Count != MeshAddresses.Length || StaticFronts is null)
        {
            throw new InvalidDataException("Effect76 requires its complete original texture, camera, music, vibration and mesh inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = ReadMesh(MeshAddresses[index]);
            if (source.Address != MeshAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != MeshLengths[index] || ValidateMesh(source.Bytes, false, null) != source.Bytes.Length)
            {
                throw new InvalidDataException("Effect76 raw mesh leaves its exact physical input.");
            }
        }
        if (Dword(Music.Data, 0) != 2 || Dword(Music.Data, 4) != 16 || Dword(Music.Data, 8) != 131360 || Dword(Music.Data, 12) != MusicLength)
        {
            throw new InvalidDataException("Effect76 music has an invalid original section directory.");
        }
        NativeBattleMusicLoadSource music = NativeBattleMusicLoadSource.Read501A20(Music.Data);
        if (music.SoundBank.Length != 131344 || music.Music.Length != 6912 || Word(music.Music, 6) != music.Music.Length)
        {
            throw new InvalidDataException("Effect76 music sections leave their complete current input.");
        }
        _ = music.ReadIdentity();
        NativeVibrationBank vibration = new(Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan())), ImmutableArray.CreateRange(new[] { checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4)) }), Vibration.Data);
        vibration.Validate();
        ValidateCamera();
        if (StageFile38 is null || StageFile38.FileId != StageFileId || !string.Equals(StageFile38.Name, "A0STG038.X", StringComparison.OrdinalIgnoreCase) || StageFile38.Data.IsDefault || StageFile38.Data.Length != StageFileLength || Dword(StageFile38.Data, StageDirectoryOffset) != 6 || checked(StageDirectoryOffset + Dword(StageFile38.Data, StageDirectoryOffset + 28)) != StageFileLength)
        {
            throw new InvalidDataException("Effect76 requires its complete original replacement stage input.");
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4)) != Sound)
        {
            throw new InvalidDataException("Effect76 sound input disagrees with its selected cell.");
        }
        if (!ReadStatic(CameraAddress, CameraLength).AsSpan().SequenceEqual(Camera.Data.AsSpan()) || !ReadStatic(MusicAddress, MusicLength).AsSpan().SequenceEqual(Music.Data.AsSpan()) || !ReadStatic(VibrationAddress, VibrationLength).AsSpan().SequenceEqual(Vibration.Data.AsSpan()))
        {
            throw new InvalidDataException("Effect76 addressed static resources disagree with their input owners.");
        }
    }

    private void ValidateCamera()
    {
        int cursor = 0;
        while (cursor < CameraLength)
        {
            ushort command = Word(Camera.Data, cursor);
            cursor += 2;
            int opcode = command & 0xFF3F;
            if (opcode > 10)
            {
                if (command != 0x8000 || cursor != CameraLength)
                {
                    throw new InvalidDataException("Effect76 camera program has an invalid terminal extent.");
                }
                return;
            }
            int words = opcode switch
            {
                0 or 6 or 8 => 1,
                1 => 3,
                2 or 5 => 4,
                3 => 5,
                10 => 0,
                _ => throw new NotSupportedException("Effect76 camera selected an unsupported operand layout.")
            };
            if (cursor > CameraLength - words * 2)
            {
                throw new InvalidDataException("Effect76 camera operands leave their original program.");
            }
            cursor += words * 2;
        }
        throw new InvalidDataException("Effect76 camera program has no terminal instruction.");
    }

    private static int ValidateMesh(ImmutableArray<byte> data, bool shapes, int[]? expected)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapeCount = shapes ? checked((int)Dword(data, 8)) : 1;
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > 128 || shapeCount <= 0 || shapeCount > 4 || cursor != checked((shapes ? 12 : 8) + vertices * shapeCount * 8))
        {
            throw new InvalidDataException("Effect76 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group] || expected is not null && count != expected[group])
            {
                throw new InvalidDataException("Effect76 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect76 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect76 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect76 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect76 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect76 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect76 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
