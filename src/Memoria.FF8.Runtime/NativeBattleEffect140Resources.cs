using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect140Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectOwnedModelResource Model, NativeBattleEffectHeapAllocation Vibration, ImmutableArray<uint> Sounds, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, NativeBattleModuleResource ProjectedSprite, ImmutableDictionary<int, NativeBattleEffectFileAsset> Files, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleModuleResource? EarlySprite { get; init; }

    public int WorkspaceLength(uint address)
    {
        int index = ProgrammeAddresses.IndexOf(address);
        return index >= 0 ? ProgrammeWorkspaceLengths[index] : throw new NotSupportedException("Effect140 selected an unsupported ACT workspace.");
    }

    public NativeBattleGfAnimationSource Act(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? source) ? source : throw new NotSupportedException($"Effect140 ACT 0x{address:X8} is unsupported.");
    public NativeBattleGfAnimationSource ReadProgramme(uint address) => Act(address);
    public NativeBattleGfAnimationSource ReadProgrammeSource(uint address) => Act(address);
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect140 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => address == SpriteAddress ? ProjectedSprite : address == 0x11D0164 && EarlySprite is not null ? EarlySprite : throw new NotSupportedException("Effect140 selected an unsupported sprite source.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public NativeBattleEffectFileAsset ReadFile(int fileId) => Files.TryGetValue(fileId, out NativeBattleEffectFileAsset? file) ? file : throw new NotSupportedException($"Effect140 file {fileId} is unsupported.");

    public NativeBattleStaticMeshResource ReadCurrentMesh(NativeBattleFileStaging staging, uint address)
    {
        NativeBattleStaticMeshResource source = ReadMesh(address);
        return MutableMeshAddresses.Contains(address) ? source with { Bytes = staging.Read(address, source.Bytes.Length).ToImmutableArray() } : source;
    }

    public byte[] ReadStatic(uint address, int length)
    {
        if (length < 0 || MutableMeshAddresses.Any(start => address < (ulong)start + (uint)ReadMesh(start).Bytes.Length && (ulong)address + (uint)length > start) || address < (ulong)ModelSkeletonAddress + ModelSkeletonLength && (ulong)address + (uint)length > ModelSkeletonAddress)
        {
            throw new NotSupportedException("Effect140 mutable model and mesh bytes require their current addressed owner.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect140 static range 0x{address:X8}/{length} is unsupported.");
    }

    public NativeBattleStaticMeshResource ReadGeometry(uint address)
    {
        foreach (NativeBattleGfAnimationSource source in Programmes.Values)
        {
            int count = checked((int)Dword(source.Data, 0));
            for (int index = 0; index < count; index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                if (checked(source.Address + (uint)offset) != address)
                {
                    continue;
                }
                int end = GeometryEnd(source.Data, offset);
                return new(checked((byte)index), address, source.Data.AsSpan(offset, end - offset).ToArray().ToImmutableArray());
            }
        }
        throw new NotSupportedException($"Effect140 ACT geometry 0x{address:X8} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag139.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Model is null || Model.Address != ModelAddress || Model.Bytes.IsDefault || Model.Bytes.Length != ModelLength || Model.PoseSource.BoneCount != 59 || Model.Geometry.IsDefault || Model.Geometry.Length != 1 || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength || Sounds.IsDefault || Sounds.Length != 3 || Programmes is null || Programmes.Count != ProgrammeAddresses.Length || Meshes is null || Meshes.Count != MeshAddresses.Length || Files is null || Files.Count != 9 || StaticFronts is null)
        {
            throw new InvalidDataException("Effect140 requires its complete selected original resources.");
        }
        ValidateTim(MainTim.Data, 9);
        Model.PoseSource.Validate();
        int[] sections = [24, 2872, 54456, ModelLength, ModelLength];
        for (int index = 0; index < sections.Length; index++)
        {
            if (Model.Section(index) != sections[index])
            {
                throw new InvalidDataException("Effect140 owned model directory leaves its original sections.");
            }
        }
        new NativeVibrationBank(Convert.ToHexString(SHA256.HashData(Vibration.Data.AsSpan())), [checked((int)Dword(Vibration.Data, 0)), checked((int)Dword(Vibration.Data, 4))], Vibration.Data).Validate();
        for (int index = 0; index < SoundAddresses.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddresses[index], 4)) != Sounds[index])
            {
                throw new InvalidDataException("Effect140 sound input differs from its original cell.");
            }
        }
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = Act(ProgrammeAddresses[index]);
            if (source.Address != ProgrammeAddresses[index] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[index])
            {
                throw new InvalidDataException("Effect140 ACT leaves its selected source extent.");
            }
            int count = checked((int)Dword(source.Data, 0));
            if (count <= 0 || count > 5)
            {
                throw new InvalidDataException("Effect140 ACT geometry directory is invalid.");
            }
            for (int geometry = 0; geometry < count; geometry++)
            {
                _ = GeometryEnd(source.Data, checked((int)Dword(source.Data, 8 + geometry * 4)));
            }
            _ = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource source = ReadMesh(MeshAddresses[index]);
            if (source.Address != MeshAddresses[index] || source.Bytes.IsDefault || source.Bytes.Length != MeshLengths[index] || ValidateMesh(source.Bytes, false, null) != source.Bytes.Length)
            {
                throw new InvalidDataException("Effect140 raw mesh leaves its original physical extent.");
            }
        }
        if (ProjectedSprite is null || ProjectedSprite.Address != SpriteAddress || ProjectedSprite.Bytes.IsDefault || ProjectedSprite.Bytes.Length != SpriteLength || Word(ProjectedSprite.Bytes, 8) != 16)
        {
            throw new InvalidDataException("Effect140 requires its complete projected sprite source.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = ProjectedSprite.ProjectedFrame571C80(frame);
        }
        NativeBattleModuleResource early = Sprite(0x11D0164);
        if (early.Bytes.IsDefault || early.Bytes.Length != 848 || Word(early.Bytes, 8) != 26)
        {
            throw new InvalidDataException("Effect140 requires its complete early projected sprite source.");
        }
        for (int frame = 0; frame < 26; frame++)
        {
            _ = early.ProjectedFrame571C80(frame);
        }
        int[] lengths = [33824, 16928, 34400, 33312, 4, 4, 16928, 33312, 33312];
        for (int id = 528; id <= 536; id++)
        {
            NativeBattleEffectFileAsset file = ReadFile(id);
            if (file.FileId != id || file.Data.IsDefault || file.Data.Length != lengths[id - 528])
            {
                throw new InvalidDataException("Effect140 selected file leaves its exact installed extent.");
            }
        }
        _ = ReadStatic(ColorBlendAddress, 4);
    }

    private static int GeometryEnd(ImmutableArray<byte> data, int offset)
    {
        int vertices = checked((int)Dword(data, offset + 4));
        int shapes = checked((int)Dword(data, offset + 8));
        int polygon = checked((int)Dword(data, offset));
        if (vertices <= 0 || vertices > 96 || shapes <= 0 || shapes > 6 || polygon != checked(12 + vertices * shapes * 8))
        {
            throw new InvalidDataException("Effect140 ACT geometry has an invalid shape extent.");
        }
        int cursor = checked(offset + polygon);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect140 ACT polygon group leaves its current input.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect140 ACT index leaves its actual vertex rows.");
                    }
                }
            }
            cursor += count * strides[group];
        }
        return cursor;
    }

    private static int ValidateMesh(ImmutableArray<byte> data, bool shapes, int[]? expected)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapeCount = shapes ? checked((int)Dword(data, 8)) : 1;
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > 264 || shapeCount <= 0 || shapeCount > 6 || cursor != checked((shapes ? 12 : 8) + vertices * shapeCount * 8))
        {
            throw new InvalidDataException("Effect140 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group] || expected is not null && count != expected[group])
            {
                throw new InvalidDataException("Effect140 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect140 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect140 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect140 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect140 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect140 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect140 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
