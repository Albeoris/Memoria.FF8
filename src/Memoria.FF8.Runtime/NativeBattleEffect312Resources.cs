using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect312Resources(NativeBattleEffectHeapAllocation MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? source) ? source : throw new NotSupportedException($"Effect312 mesh 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? source) ? source : throw new NotSupportedException($"Effect312 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public ImmutableArray<byte> CapturePacket => StaticFronts.TryGetValue(CapturePacketAddress, out ImmutableArray<byte> packet) && packet.Length == CapturePacketLength ? packet : throw new InvalidDataException("Effect312 requires its original capture packet template.");

    public byte[] ReadStatic(uint address, int length)
    {
        if (length > 0 && address < (ulong)CapturePacketAddress + CapturePacketLength && (ulong)address + (uint)length > CapturePacketAddress)
        {
            throw new NotSupportedException("Effect312 capture packet requires its current mutable owner.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect312 static range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != 1 || StaticFronts is null || StaticFronts.Count != StaticRanges.Length + MeshAddresses.Length + SpriteAddresses.Length + 1)
        {
            throw new InvalidDataException("Effect312 requires its complete original resources.");
        }
        ValidateTim();
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            NativeBattleStaticMeshResource mesh = ReadMesh(MeshAddresses[index]);
            if (mesh.Address != MeshAddresses[index] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[index])
            {
                throw new InvalidDataException("Effect312 mesh leaves its original extent.");
            }
            ValidateMesh(mesh.Bytes, index);
            if (!StaticFronts.TryGetValue(mesh.Address, out ImmutableArray<byte> addressed) || !addressed.AsSpan().SequenceEqual(mesh.Bytes.AsSpan()))
            {
                throw new InvalidDataException("Effect312 mesh disagrees with its addressed source.");
            }
        }
        NativeBattleModuleResource sprite = Sprite(SpriteAddress);
        if (sprite.Address != SpriteAddress || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLength || Word(sprite.Bytes, 8) != 10)
        {
            throw new InvalidDataException("Effect312 requires its complete sprite directory.");
        }
        if (!StaticFronts.TryGetValue(SpriteAddress, out ImmutableArray<byte> addressedSprite) || !addressedSprite.AsSpan().SequenceEqual(sprite.Bytes.AsSpan()) || !StaticFronts.TryGetValue(CameraAddress, out ImmutableArray<byte> addressedCamera) || !addressedCamera.AsSpan().SequenceEqual(Camera.Data.AsSpan()))
        {
            throw new InvalidDataException("Effect312 camera or sprite disagrees with its addressed source.");
        }
        int[] offsets = [32, 56, 100, 144, 168, 192, 216, 240, 264, 288];
        int[] counts = [1, 2, 2, 1, 1, 1, 1, 1, 1, 1];
        for (int frame = 0; frame < 10; frame++)
        {
            int offset = Word(sprite.Bytes, 10 + frame * 2);
            if (offset != offsets[frame] || Dword(sprite.Bytes, offset) != counts[frame] || offset + 4 + counts[frame] * 20 > sprite.Bytes.Length)
            {
                throw new InvalidDataException("Effect312 sprite frame leaves its original source.");
            }
            _ = sprite.ProjectedFrame571C80(frame);
        }
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 24 || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect312 camera directory leaves its original extent.");
        }
        NativeBattleCameraBank bank = new(Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
        foreach ((uint address, int length) in StaticRanges)
        {
            if (!StaticFronts.TryGetValue(address, out ImmutableArray<byte> bytes) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect312 requires its exact addressed constant sources.");
            }
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddresses[0], 4)) != 0x64209 || BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddresses[1], 4)) != 0x6420C)
        {
            throw new InvalidDataException("Effect312 sound cells changed.");
        }
        uint[] variants = [0xCDEE24, 0xCDF0DC, 0xCDF394];
        for (int index = 0; index < variants.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(MeshTableAddress + (uint)index * 4, 4)) != variants[index])
            {
                throw new InvalidDataException("Effect312 mesh directory changed.");
            }
        }
        if (Dword(CapturePacket, 0) != 0x05000000 || Dword(CapturePacket, 4) != 0x28000000 || Dword(CapturePacket, 8) != 0 || Dword(CapturePacket, 12) != 0x100 || Dword(CapturePacket, 16) != 0x1000000 || Dword(CapturePacket, 20) != 0x1000100)
        {
            throw new InvalidDataException("Effect312 capture template requires its original flat quad header.");
        }
    }

    private static void ValidateMesh(ImmutableArray<byte> data, int selected)
    {
        int vertices = checked((int)Dword(data, 4));
        int cursor = checked((int)Dword(data, 0));
        if (vertices != MeshVertexCounts[selected] || cursor != 8 + vertices * 8)
        {
            throw new InvalidDataException("Effect312 mesh vertex directory changed.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] commands = [0x22, 0x2A, 0x24, 0x2E, 0x32, 0x3A, 0x34, 0x3E];
        for (int group = 0; group < 8; group++)
        {
            uint count = Dword(data, cursor);
            cursor += 4;
            if (count != MeshGroupCounts[selected][group] || count > (data.Length - cursor) / strides[group])
            {
                throw new InvalidDataException("Effect312 primitive group leaves its original source.");
            }
            for (int face = 0; face < count; face++)
            {
                int at = cursor + face * strides[group];
                if (data[at + 3] != commands[group])
                {
                    throw new InvalidDataException("Effect312 original primitive command changed.");
                }
                for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
                {
                    int physical = Word(data, at + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect312 face index leaves its physical vertex rows.");
                    }
                }
            }
            cursor += checked((int)count * strides[group]);
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect312 mesh has trailing data.");
        }
    }

    private void ValidateTim()
    {
        ImmutableArray<byte> data = MainTim.Data;
        if (Dword(data, 0) != 16 || Dword(data, 4) != 9)
        {
            throw new InvalidDataException("Effect312 requires its original indexed TIM.");
        }
        int cursor = 8;
        int[][] rectangles = [[320, 251, 256, 5], [576, 256, 256, 256]];
        for (int block = 0; block < 2; block++)
        {
            int length = checked((int)Dword(data, cursor));
            if (length < 12 || length > data.Length - cursor || length != 12 + Word(data, cursor + 8) * Word(data, cursor + 10) * 2)
            {
                throw new InvalidDataException("Effect312 TIM block leaves its input.");
            }
            for (int component = 0; component < 4; component++)
            {
                if (Word(data, cursor + 4 + component * 2) != rectangles[block][component])
                {
                    throw new InvalidDataException("Effect312 TIM rectangle changed.");
                }
            }
            cursor += length;
        }
        if (cursor != data.Length)
        {
            throw new InvalidDataException("Effect312 TIM has trailing bytes.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect312 DWORD read leaves its source.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect312 WORD read leaves its source.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
