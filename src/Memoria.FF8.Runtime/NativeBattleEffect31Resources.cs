using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect31Resources(NativeBattleEffectHeapAllocation MainTim, NativeBattleEffectHeapAllocation Camera, ImmutableDictionary<uint, NativeBattleGfAnimationSource> Programmes, ImmutableDictionary<uint, NativeBattleStaticMeshResource> Meshes, ImmutableDictionary<uint, NativeBattleModuleResource> Sprites, ImmutableDictionary<uint, ImmutableArray<byte>> StaticFronts)
{
    public NativeBattleGfAnimationSource Programme(uint address) => Programmes.TryGetValue(address, out NativeBattleGfAnimationSource? value) ? value : throw new NotSupportedException($"Effect31 programme 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource Mesh(uint address) => Meshes.TryGetValue(address, out NativeBattleStaticMeshResource? value) ? value : throw new NotSupportedException($"Effect31 mesh 0x{address:X8} is unsupported.");
    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource Sprite(uint address) => Sprites.TryGetValue(address, out NativeBattleModuleResource? value) ? value : throw new NotSupportedException($"Effect31 sprite 0x{address:X8} is unsupported.");
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);
    public uint Sound => BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(SoundAddress, 4));

    public byte[] ReadStatic(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        foreach ((uint start, int count) in MutableStreamRanges)
        {
            if (length > 0 && address < (ulong)start + (uint)count && (ulong)address + (uint)length > start)
            {
                throw new NotSupportedException("Effect31 stream requires its current mutable owner.");
            }
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in StaticFronts)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.AsSpan(checked((int)(address - start)), length).ToArray();
            }
        }
        throw new NotSupportedException($"Effect31 immutable range 0x{address:X8}/{length} is unsupported.");
    }

    public void Validate()
    {
        if (MainTim is null || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || Camera is null || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Programmes is null || Programmes.Count != ProgrammeAddresses.Length || Meshes is null || Meshes.Count != MeshAddresses.Length || Sprites is null || Sprites.Count != SpriteAddresses.Length || StaticFronts is null || StaticFronts.Count != StaticRanges.Length + ProgrammeAddresses.Length + SpriteAddresses.Length + 1)
        {
            throw new InvalidDataException("Effect31 requires its complete typed resources.");
        }
        ValidateTim();
        if (Word(Camera.Data, 0) != 2 || Word(Camera.Data, 2) != 8 || Word(Camera.Data, 4) != 40 || Word(Camera.Data, 6) != CameraLength)
        {
            throw new InvalidDataException("Effect31 camera directory changed.");
        }
        RequireAddressed(CameraAddress, Camera.Data);
        foreach ((uint address, int length) in StaticRanges)
        {
            if (!StaticFronts.TryGetValue(address, out ImmutableArray<byte> bytes) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect31 immutable fragment is incomplete.");
            }
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(CustomDirectoryAddress, 4)) != 1 || Sound != 0x61B68)
        {
            throw new InvalidDataException("Effect31 initialized source or sound changed.");
        }
        uint[] controls = [0x160C978, 0x160CAD0, 0x160CC28, 0x160CD80, 0, 0x160CED8, 0, 0x160D030, 0, 0, 0, 0, 0, 0, 0, 0];
        uint[] sprites = [0x15F8DF4, 0x15F8EEC, 0x15F97B4, 0x15F9174, 0, 0x15F944C, 0, 0x15F944C, 0, 0, 0, 0, 0, 0, 0, 0];
        for (int table = 0; table < 4; table++)
        {
            for (int row = 0; row < 16; row++)
            {
                uint expected = table == 0 ? controls[row] : table == 1 ? sprites[row] : 0;
                if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(CustomDirectoryAddress + 4 + (uint)(table * 64 + row * 4), 4)) != expected)
                {
                    throw new InvalidDataException("Effect31 custom source directory changed.");
                }
            }
        }
        for (int selected = 0; selected < ControlAddresses.Length; selected++)
        {
            uint address = ControlAddresses[selected];
            if (BinaryPrimitives.ReadInt16LittleEndian(ReadStatic(address + 0x38, 2)) != -256 || BinaryPrimitives.ReadUInt16LittleEndian(ReadStatic(address + 0x64, 2)) != (selected < 3 ? 0 : 6553) || ReadStatic(address + 0x148, 6).Any(value => value != 0) || ReadStatic(address + 0x150, 6).Any(value => value != 0))
            {
                throw new InvalidDataException("Effect31 control friction or Euler operands changed.");
            }
            foreach (int offset in ControlHeaderOffsets)
            {
                int index = ControlHeaderOffsets.IndexOf(offset);
                if (ReadStatic(address + (uint)offset, 1)[0] != ControlHeaderValues[selected][index])
                {
                    throw new InvalidDataException("Effect31 selected control mode or lifetime changed.");
                }
            }
            for (int cell = 0; cell < 32; cell++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(ReadStatic(address + 0xC8 + (uint)cell * 4, 4)) != ControlPointerCells[selected][cell])
                {
                    throw new InvalidDataException("Effect31 control key pointer changed.");
                }
            }
        }
        short[][] actorTables = [[656, 1232, 1792, 2304, 2768, 3184, 3552, 3872, 4096], [256, 448, 576, 640, 640, 576, 448, 256, 0]];
        for (int table = 0; table < actorTables.Length; table++)
        {
            byte[] values = ReadStatic(table == 0 ? FactorAddress : YOffsetAddress, 18);
            for (int row = 0; row < actorTables[table].Length; row++)
            {
                if (BinaryPrimitives.ReadInt16LittleEndian(values.AsSpan(row * 2, 2)) != actorTables[table][row])
                {
                    throw new InvalidDataException("Effect31 actor progression table changed.");
                }
            }
        }
        for (int selected = 0; selected < ColorProgrammeAddresses.Length; selected++)
        {
            byte[] values = ReadStatic(ColorProgrammeAddresses[selected], ColorProgrammeLengths[selected]);
            for (int at = 0; at < values.Length; at += 4)
            {
                if ((values[at + 3] == 0xFF) != (at + 4 == values.Length))
                {
                    throw new InvalidDataException("Effect31 color programme terminal changed.");
                }
            }
        }
        for (int selected = 0; selected < ProgrammeAddresses.Length; selected++)
        {
            NativeBattleGfAnimationSource source = Programme(ProgrammeAddresses[selected]);
            if (source.Address != ProgrammeAddresses[selected] || source.Data.IsDefault || source.Data.Length != ProgrammeLengths[selected])
            {
                throw new InvalidDataException("Effect31 programme extent changed.");
            }
            RequireAddressed(source.Address, source.Data);
            int table = checked((int)Dword(source.Data, 4));
            int[] geometryCounts = [3, 1, 2, 2];
            int[] nodeCounts = [4, 1, 2, 2];
            if (Dword(source.Data, 0) != geometryCounts[selected] || Dword(source.Data, table) != nodeCounts[selected] || Dword(source.Data, table + 4) != ProgrammeDurations[selected])
            {
                throw new InvalidDataException("Effect31 programme directory or duration changed.");
            }
            int directoryIndex = 0;
            foreach (uint address in MeshAddresses)
            {
                if (address >= source.Address && address < (ulong)source.Address + (uint)source.Data.Length)
                {
                    if (Dword(source.Data, 8 + directoryIndex * 4) != address - source.Address)
                    {
                        throw new InvalidDataException("Effect31 programme geometry address changed.");
                    }
                    directoryIndex++;
                }
            }
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[selected]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[selected]);
            for (int frame = 0; frame < ProgrammeDurations[selected]; frame++)
            {
                workspace = workspace.Advance701970(source, 0, (current, node) =>
                {
                    int geometry = node.GeometryIndex;
                    if (geometry >= Dword(source.Data, 0))
                    {
                        throw new InvalidDataException("Effect31 node geometry leaves its programme.");
                    }
                    int mesh = checked((int)Dword(source.Data, 8 + geometry * 4));
                    if ((node.AnimationFlags & 0x2000) != 0 && (Word(node.Data, 40) >= Dword(source.Data, mesh + 8) || Word(node.Data, 42) >= Dword(source.Data, mesh + 8)))
                    {
                        throw new InvalidDataException("Effect31 morph frame leaves its shape banks.");
                    }
                    return current;
                }).Workspace;
            }
        }
        for (int selected = 0; selected < MeshAddresses.Length; selected++)
        {
            NativeBattleStaticMeshResource mesh = Mesh(MeshAddresses[selected]);
            if (mesh.Address != MeshAddresses[selected] || mesh.Bytes.IsDefault || mesh.Bytes.Length != MeshLengths[selected] || !ReadStatic(mesh.Address, mesh.Bytes.Length).AsSpan().SequenceEqual(mesh.Bytes.AsSpan()))
            {
                throw new InvalidDataException("Effect31 geometry differs from its programme owner.");
            }
            ValidateMesh(mesh.Bytes, selected);
        }
        for (int selected = 0; selected < SpriteAddresses.Length; selected++)
        {
            NativeBattleModuleResource sprite = Sprite(SpriteAddresses[selected]);
            int count = SpriteFrameCounts[selected];
            if (sprite.Address != SpriteAddresses[selected] || sprite.Bytes.IsDefault || sprite.Bytes.Length != SpriteLengths[selected] || Word(sprite.Bytes, 8) != count)
            {
                throw new InvalidDataException("Effect31 sprite directory changed.");
            }
            RequireAddressed(sprite.Address, sprite.Bytes);
            for (int frame = 0; frame < count; frame++)
            {
                int offset = Word(sprite.Bytes, 10 + frame * 2);
                if (Dword(sprite.Bytes, offset) != 1 || offset + 24 > sprite.Bytes.Length || Word(sprite.Bytes, 12 + frame * 2) != (frame + 1 == count ? ushort.MaxValue : offset + 24))
                {
                    throw new InvalidDataException("Effect31 sprite frame or successor directory changed.");
                }
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
    }

    private void RequireAddressed(uint address, ImmutableArray<byte> data)
    {
        if (!StaticFronts.TryGetValue(address, out ImmutableArray<byte> bytes) || !bytes.AsSpan().SequenceEqual(data.AsSpan()))
        {
            throw new InvalidDataException("Effect31 typed source differs from its addressed immutable owner.");
        }
    }

    private static void ValidateMesh(ImmutableArray<byte> bytes, int selected)
    {
        int vertices = MeshVertexCounts[selected];
        int cursor = checked(12 + vertices * MeshShapeCounts[selected] * 8);
        if (Dword(bytes, 0) != cursor || Dword(bytes, 4) != vertices || Dword(bytes, 8) != MeshShapeCounts[selected])
        {
            throw new InvalidDataException("Effect31 geometry vertex or shape directory changed.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] commands = [0x22, 0x2A, 0x24, 0x2E, 0x30, 0x3A, 0x34, 0x3E];
        if (selected == 4)
        {
            commands[4] = 0x32;
        }
        if (selected == 5)
        {
            commands[6] = 0x36;
        }
        for (int group = 0; group < 8; group++)
        {
            int count = MeshGroupCounts[selected][group];
            if (Dword(bytes, cursor) != count || count > (bytes.Length - cursor - 4) / strides[group])
            {
                throw new InvalidDataException("Effect31 primitive group changed.");
            }
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                int at = cursor + face * strides[group];
                if (bytes[at + 3] != commands[group])
                {
                    throw new InvalidDataException("Effect31 primitive command changed.");
                }
                if ((group is 2 or 3 or 6 or 7) && (Word(bytes, at + 18) != (selected == 6 ? 182 : 186) || Word(bytes, at + 14) != (selected == 6 ? 15380 : 15444)))
                {
                    throw new InvalidDataException("Effect31 primitive material changed.");
                }
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int physical = Word(bytes, at + 4 + corner * 2) * 4;
                    if ((physical & 7) != 0 || physical >= vertices * 8)
                    {
                        throw new InvalidDataException("Effect31 primitive index leaves its vertex bank.");
                    }
                }
            }
            cursor += count * strides[group];
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("Effect31 geometry has trailing data.");
        }
    }

    private void ValidateTim()
    {
        ImmutableArray<byte> data = MainTim.Data;
        if (Dword(data, 0) != 16 || Dword(data, 4) != 9)
        {
            throw new InvalidDataException("Effect31 requires its indexed TIM.");
        }
        int cursor = 8;
        int[][] rectangles = [[320, 240, 256, 5], [384, 256, 320, 256]];
        foreach (int[] rectangle in rectangles)
        {
            int length = checked((int)Dword(data, cursor));
            if (length != 12 + rectangle[2] * rectangle[3] * 2 || length > data.Length - cursor)
            {
                throw new InvalidDataException("Effect31 TIM block extent changed.");
            }
            for (int component = 0; component < 4; component++)
            {
                if (Word(data, cursor + 4 + component * 2) != rectangle[component])
                {
                    throw new InvalidDataException("Effect31 TIM rectangle changed.");
                }
            }
            cursor += length;
        }
        if (cursor != data.Length || 3 * 0xD000 > data.Length)
        {
            throw new InvalidDataException("Effect31 TIM allocation cannot retain its three arena seeds.");
        }
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 4 ? throw new InvalidDataException("Effect31 DWORD leaves its resource.") : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Word(ImmutableArray<byte> bytes, int offset) => offset < 0 || offset > bytes.Length - 2 ? throw new InvalidDataException("Effect31 WORD leaves its resource.") : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
}
