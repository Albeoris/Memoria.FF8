using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation EmbeddedTim, NativeBattleEffectHeapAllocation Camera, NativeBattleEffectHeapAllocation Vibration, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect66 mesh 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect66 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    public byte[] ReadStatic(uint address, int length)
    {
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect66 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag065.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || EmbeddedTim is null || EmbeddedTim.Data.IsDefault || EmbeddedTim.Data.Length != EmbeddedTimLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Vibration is null || Vibration.Data.IsDefault || Vibration.Data.Length != VibrationLength)
        {
            throw new InvalidDataException("Effect66 requires its complete texture, camera and vibration inputs.");
        }
        ValidateTim(MainTim.Data, 9);
        ValidateTim(EmbeddedTim.Data, 9);
        _ = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), Camera.Data, 0);
        int first = checked((int)Dword(Vibration.Data, 0));
        int second = checked((int)Dword(Vibration.Data, 4));
        new NativeVibrationBank(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Vibration.Data.AsSpan())), [first, second], Vibration.Data).Validate();
        if (Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length || StaticFronts is null)
        {
            throw new InvalidDataException("Effect66 resource directories have different dimensions.");
        }
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || ValidateMesh(mesh.Bytes, false, null) != mesh.Bytes.Length)
            {
                throw new InvalidDataException("Effect66 raw mesh leaves its exact physical source.");
            }
        }
        int[] frames = [16, 16, 16, 15, 12];
        for (int index = 0; index < SpriteAddresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[index]);
            if (sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Word(sprite.Bytes, 8) != frames[index])
            {
                throw new InvalidDataException("Effect66 projected sprite dimensions changed.");
            }
            for (int frame = 0; frame < frames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        foreach ((uint address, int length) in new (uint, int)[] { (0xDEA344, 24), (0xDEA360, 116), (0xDEA3DC, 72), (0xDEA424, 48), (0xDEA454, 48) })
        {
            if (ReadStatic(address, length).Length != length)
            {
                throw new InvalidDataException("Effect66 static front dimensions changed.");
            }
        }
        foreach ((uint address, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (bytes.IsDefault || address < InitializedBase + InitializedLength && (ulong)address + (uint)bytes.Length > InitializedBase)
            {
                throw new InvalidDataException("Effect66 readonly resources overlap current writable pointer owners.");
            }
        }
    }

    private static int ValidateMesh(ImmutableArray<byte> data, bool shapes, int[]? expected)
    {
        int vertices = checked((int)Dword(data, 4));
        int shapeCount = shapes ? checked((int)Dword(data, 8)) : 1;
        int cursor = checked((int)Dword(data, 0));
        if (vertices <= 0 || vertices > 304 || shapeCount <= 0 || shapeCount > 4 || cursor != checked((shapes ? 12 : 8) + vertices * shapeCount * 8))
        {
            throw new InvalidDataException("Effect66 geometry has invalid physical shape rows.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(data, cursor));
            cursor += 4;
            if (count < 0 || count > (data.Length - cursor) / strides[group] || expected is not null && count != expected[group])
            {
                throw new InvalidDataException("Effect66 geometry group leaves its current source.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect66 face index leaves its physical vertex rows.");
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
            throw new InvalidDataException("Effect66 texture input has an unexpected TIM header.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect66 TIM block leaves its input.");
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect66 TIM input has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect66 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect66 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
