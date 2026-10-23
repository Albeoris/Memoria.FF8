using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315Resources(ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleEffectHeapAllocation Camera, uint SoundA, uint SoundB, NativeVibrationBank Vibration, ImmutableArray<byte> PointRows24, ImmutableArray<byte> VectorRows48, ImmutableArray<byte> CasterColors48, ImmutableArray<byte> RecipientColors96)
{
    public void Validate()
    {
        if (Sprites.IsDefault || Sprites.Length != 3 || Meshes.IsDefault || Meshes.Length != 4)
        {
            throw new InvalidDataException("Effect315 requires its three projected sprites and four addressed meshes.");
        }
        int[] frames = [16, 14, 21];
        uint[] headers = [0x06010524, 0x07010524, 0x08010524];
        for (int index = 0; index < Sprites.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite is null || sprite.Address != SpriteAddresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[index] || Dword(sprite.Bytes, 0) != headers[index] || Word(sprite.Bytes, 8) != frames[index])
            {
                throw new InvalidDataException("Effect315 requires its complete selected projected sprite front.");
            }
            for (int phase = 0; phase < frames[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
                if (frame.Quads.Length != 20 || frame.ExtendedPacket != (index == 0))
                {
                    throw new InvalidDataException("Effect315 sprite frame leaves its selected quad contract.");
                }
            }
        }
        for (int index = 0; index < Meshes.Length; index++)
        {
            ValidateMesh(Meshes[index], index);
        }
        if (Camera is null || Camera.Identity != "FF8_EN:CD2B08" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength || SoundA != 0x641F1 || SoundB != 0x642BC)
        {
            throw new InvalidDataException("Effect315 requires its complete addressed camera and sounds.");
        }
        if (Vibration is null || Vibration.Bytes.IsDefault || Vibration.Bytes.Length != VibrationLength || Vibration.Offsets.IsDefault || !Vibration.Offsets.SequenceEqual(new[] { 0, 256 }))
        {
            throw new InvalidDataException("Effect315 requires its complete vibration bank.");
        }
        Vibration.Validate();
        if (Vibration.ReadWord(256) != 22 || Vibration.ReadWord(258) != 0 || PointRows24.IsDefault || PointRows24.Length != 24 || VectorRows48.IsDefault || VectorRows48.Length != 48 || CasterColors48.IsDefault || CasterColors48.Length != 48 || RecipientColors96.IsDefault || RecipientColors96.Length != 96)
        {
            throw new InvalidDataException("Effect315 requires its selected channel lengths and complete point, vector and color tables.");
        }
    }

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Meshes.SingleOrDefault(mesh => mesh.Address == address) ?? throw new NotSupportedException("Effect315 selected an unsupported static mesh.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException("Effect315 selected an unsupported projected sprite.");
    public uint ReadSound(uint address) => address switch
    {
        SoundAAddress => SoundA,
        SoundBAddress => SoundB,
        _ => throw new NotSupportedException("Effect315 selected an unsupported sound.")
    };

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect315 static read has a negative length.");
        }
        foreach ((uint start, ImmutableArray<byte> data) in StaticFronts())
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - start)), length).ToArray().ToImmutableArray();
            }
        }
        throw new NotSupportedException("Effect315 static read leaves its selected immutable fronts.");
    }

    private IEnumerable<(uint Address, ImmutableArray<byte> Data)> StaticFronts()
    {
        foreach (NativeBattleModuleResource sprite in Sprites)
        {
            yield return (sprite.Address, sprite.Bytes);
        }
        foreach (NativeBattleStaticMeshResource mesh in Meshes)
        {
            yield return (mesh.Address, mesh.Bytes);
        }
        yield return (CameraAddress, Camera.Data);
        yield return (VibrationAddress, Vibration.Bytes);
        yield return (SoundAAddress, BitConverter.GetBytes(SoundA).ToImmutableArray());
        yield return (SoundBAddress, BitConverter.GetBytes(SoundB).ToImmutableArray());
        yield return (PointRowsAddress, PointRows24);
        yield return (VectorRowsAddress, VectorRows48);
        yield return (CasterColorsAddress, CasterColors48);
        yield return (RecipientColorsAddress, RecipientColors96);
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh, int index)
    {
        int vertices = MeshVertexCounts[index];
        if (mesh is null || mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index] || Dword(mesh.Bytes, 0) != 8 + vertices * 8 || Dword(mesh.Bytes, 4) != vertices)
        {
            throw new InvalidDataException("Effect315 requires its complete selected eight-byte geometry front.");
        }
        int[][] expectedGroups = [[0, 0, 0, 64, 0, 0, 0, 0], [0, 0, 0, 0, 0, 33, 0, 0], [0, 0, 0, 64, 0, 0, 0, 0], [0, 0, 24, 72, 0, 0, 0, 24]];
        int cursor = 8 + vertices * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            uint count = Dword(mesh.Bytes, cursor);
            if (count != expectedGroups[index][group])
            {
                throw new InvalidDataException("Effect315 selected geometry group counts changed.");
            }
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                int first = checked(cursor + face * strides[group]);
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    uint offset = (uint)Word(mesh.Bytes, first + 4 + corner * 2) * 4;
                    if ((offset & 7) != 0 || offset >= vertices * 8)
                    {
                        throw new InvalidDataException("Effect315 face index leaves its selected source vertex rows.");
                    }
                }
            }
            cursor = checked(cursor + (int)count * strides[group]);
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Effect315 geometry extent changed.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
}
