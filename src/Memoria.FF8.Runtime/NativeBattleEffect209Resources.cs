using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageVariant137Entry(int FileId, int HeaderOffset, uint Color40, uint Color44, uint RotationSource);
public sealed record NativeBattleEffect209StageDepth(int TableBaseBucket, byte DepthShift);

public sealed record NativeBattleEffect209Resources(NativeBattleEffectHeapAllocation EmbeddedTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableArray<uint> InitialColors, ImmutableArray<uint> CycleColors, ImmutableArray<uint> ScrollColors, ImmutableArray<NativeBattleEffect209StageDepth> StageDepthRows, ImmutableArray<uint> Sounds, ImmutableArray<NativeBattleStageVariant137Entry> StageVariants, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    [JsonIgnore]
    public Func<int, byte[], NativeBattleStageSource>? DecodeReloadStage { get; init; }
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect209 mesh 0x{address:X8} is unsupported.");
    public NativeBattleEffectFileAsset ReadFile(int fileId) => Files.TryGetValue(fileId, out NativeBattleEffectFileAsset? source) ? source : throw new NotSupportedException($"Effect209 file {fileId} is unsupported.");
    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect209 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (EmbeddedTim is null || EmbeddedTim.Data.IsDefault || EmbeddedTim.Data.Length != TimLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Meshes is null || Meshes.Count != MeshAddresses.Length || InitialColors.IsDefault || InitialColors.Length != 8 || CycleColors.IsDefault || CycleColors.Length != 24 || ScrollColors.IsDefault || ScrollColors.Length != 18 || StageDepthRows.IsDefault || StageDepthRows.Length != 4 || Sounds.IsDefault || Sounds.Length != 2 || StageVariants.IsDefault || StageVariants.Length != 10 || Files is null || Files.Count != 10 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect209 requires its complete selected original resources.");
        }
        ValidateTim(EmbeddedTim.Data, 9);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect209 mesh leaves its selected physical source.");
            }
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != CameraModelOffset || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect209 camera directory leaves its original source.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte group = 0; group < Word(bank.Data, 0); group++)
        {
            for (byte track = 0; track < 8; track++)
            {
                _ = bank.TrackOffset((byte)(group * 16 + track));
            }
        }
        string hash = Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan()));
        new NativeVibrationBank(hash, [checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4))], Vibration.Data).Validate();
        foreach ((uint address, int length) in StaticRanges)
        {
            _ = ReadStatic(address, length);
        }
        ValidateColors(InitialColors, InitialColorsAddress);
        ValidateColors(CycleColors, CycleColorsAddress);
        ValidateColors(ScrollColors, ScrollColorsAddress);
        for (int index = 0; index < Sounds.Length; index++)
        {
            if (Sounds[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress + (uint)(index * 4), 4)))
            {
                throw new InvalidDataException("Effect209 decoded sound disagrees with its addressed source.");
            }
        }
        for (int index = 0; index < StageDepthRows.Length; index++)
        {
            byte[] bytes = ReadStatic(StageDepthRowsAddress + (uint)(index * 8), 8);
            if (StageDepthRows[index].TableBaseBucket != BinaryPrimitives.ReadInt32LittleEndian(bytes) || StageDepthRows[index].DepthShift != BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)))
            {
                throw new InvalidDataException("Effect209 stage depth row disagrees with its addressed source.");
            }
        }
        foreach (NativeBattleStageVariant137Entry entry in StageVariants)
        {
            NativeBattleEffectFileAsset file = ReadFile(entry.FileId);
            if (file.FileId != entry.FileId || file.Data.IsDefault || entry.HeaderOffset < 0 || entry.HeaderOffset > file.Data.Length - 32 || Dword(file.Data, entry.HeaderOffset) != 6)
            {
                throw new InvalidDataException("Effect209 stage variant leaves its original file.");
            }
            int prefix = checked((int)Dword(file.Data, entry.HeaderOffset + 24));
            int end = checked((int)Dword(file.Data, entry.HeaderOffset + 28));
            if (prefix < 32 || prefix > 0x8000 || entry.HeaderOffset + end != file.Data.Length || prefix > end || Dword(file.Data, entry.HeaderOffset + prefix) != 16)
            {
                throw new InvalidDataException("Effect209 stage variant exceeds its native reserved prefix or TIM source.");
            }
            if (entry.RotationSource is not (0x1D99BE8 or 0x1D99BEA or 0x1D99BEC or 0x1D99BEE))
            {
                _ = ReadStatic(entry.RotationSource, 2);
            }
        }
    }

    private void ValidateColors(ImmutableArray<uint> colors, uint address)
    {
        for (int index = 0; index < colors.Length; index++)
        {
            if (colors[index] != BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(address + (uint)(index * 4), 4)))
            {
                throw new InvalidDataException("Effect209 decoded colors disagree with their addressed sources.");
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > (data.Length - 8) / 8 || cursor != checked(8 + vertices * 8))
        {
            throw new InvalidDataException("Effect209 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect209 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect209 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect209 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect209 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect209 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect209 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect209 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
