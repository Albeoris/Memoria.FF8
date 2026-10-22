using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleEffectFileAsset> Files, NativeBattleEffectHeapAllocation InitialIoScript, uint Sound, uint ColdSharedPointer, NativeBattleCuragaExecutableSource NodeSource, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleGfAnimationSource> Acts, NativeBattleFullscreenOverlayProgram Overlay)
{
    public NativeBattleGfAnimationSource Act(uint address) => Acts.Single(source => source.Address == address);
    public NativeBattleEffectFileAsset File(short id) => Files.Single(file => file.FileId == id);

    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag046.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != 168480 || Animation is null || Animation.Identity != "FF8_EN:15B8030" || Animation.Data.IsDefault || Animation.Data.Length != 512 || Word(Animation.Data, 0) != 2 || Word(Animation.Data, 6) != 512 || Sound != 0x64221 || ColdSharedPointer != NativeBattleCurse47State.SharedAddress)
        {
            throw new InvalidDataException("Curse requires its original initializer, camera, sound and cold shared pointer.");
        }
        if (InitialIoScript is null || InitialIoScript.Identity != "FF8_EN:15B8230" || InitialIoScript.Data.IsDefault || InitialIoScript.Data.Length != 80 || Files.IsDefault || Files.Length != 3 || Overlay is null || Overlay.Address != 0x15B8280 || Overlay.Commands.IsDefault || Overlay.Commands.Length != 4)
        {
            throw new InvalidDataException("Curse requires its complete initial script, three selected files and overlay programme.");
        }
        int[] lengths = [33312, 33312, 32832];
        for (int index = 0; index < 3; index++)
        {
            NativeBattleEffectFileAsset file = Files[index];
            if (file is null || file.FileId != 388 + index || !string.Equals(file.Name, $"MAG046_B.1T{index}", StringComparison.OrdinalIgnoreCase) || file.Data.IsDefault || file.Data.Length != lengths[index])
            {
                throw new InvalidDataException("Curse files disagree with their original ordered script rows.");
            }
        }
        for (int index = 0; index < 5; index++)
        {
            int offset = index * 16;
            uint opcode = index == 0 ? 0u : index == 4 ? 0xFFu : 1u;
            uint file = index is > 0 and < 4 ? (uint)(387 + index) : 0;
            uint length = index is > 0 and < 4 ? (uint)lengths[index - 1] : 0;
            if (Dword(InitialIoScript.Data, offset) != (opcode | file << 16) || Dword(InitialIoScript.Data, offset + 4) != 0 || Dword(InitialIoScript.Data, offset + 8) != length || Dword(InitialIoScript.Data, offset + 12) != 0)
            {
                throw new InvalidDataException("Curse ingress requires the original cold script; live destinations belong to retained runtime state.");
            }
        }
        ValidateNodes();
        if (Acts.IsDefault || Acts.Length != 3)
        {
            throw new InvalidDataException("Curse requires all three original embedded ACT sources.");
        }
        uint[] addresses = [0x17BB134, 0x17BDF6C, 0x17C0DA4];
        int[] sizes = [11832, 11832, 16380];
        for (int index = 0; index < Acts.Length; index++)
        {
            NativeBattleGfAnimationSource source = Acts[index];
            if (source is null || source.Address != addresses[index] || source.Data.IsDefault || source.Data.Length != sizes[index])
            {
                throw new InvalidDataException("Curse ACT source does not retain its original identity and complete extent.");
            }
            ValidateAct(source, index == 2 ? 10 : 7, index == 2 ? 500 : 340);
        }
    }

    private void ValidateNodes()
    {
        if (NodeSource is null || NodeSource.ResourceAddress != 0x17B97A4 || NodeSource.Data.IsDefault || NodeSource.Data.Length != 6544 || Dword(NodeSource.Data, 0) != 0 || Sprites.IsDefault || Sprites.Length != 3)
        {
            throw new InvalidDataException("Curse requires its original relative node bank and three typed sprite resources.");
        }
        for (int index = 0; index < 16; index++)
        {
            uint source = Dword(NodeSource.Data, 4 + index * 4);
            uint sprite = Dword(NodeSource.Data, 0x44 + index * 4);
            if (Dword(NodeSource.Data, 0x84 + index * 4) != 0 || Dword(NodeSource.Data, 0xC4 + index * 4) != 0 || (index < 3 ? source == 0 || sprite == 0 : source != 0 || sprite != 0))
            {
                throw new InvalidDataException("Curse node directories must select their three original sprite sources and no mesh or morph resource.");
            }
            if (index >= 3)
            {
                continue;
            }
            if (source < 0x104 || source > NodeSource.Data.Length - 344 || NodeSource.Data[checked((int)source + 0x16)] != 0)
            {
                throw new InvalidDataException("Curse node header leaves its source bank or changes its original sprite kind.");
            }
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint channel = Dword(NodeSource.Data, checked((int)source + field));
                if (channel < 0x104 || channel > NodeSource.Data.Length - 2)
                {
                    throw new InvalidDataException("Curse source channel leaves its immutable bank.");
                }
            }
            NativeBattleModuleResource resource = Sprites[index];
            if (resource is null || resource.Address != NodeSource.ResourceAddress + sprite || resource.Bytes.IsDefault || resource.Bytes.Length != (index == 2 ? 428 : 404) || sprite > NodeSource.Data.Length - resource.Bytes.Length || !NodeSource.Data.AsSpan().Slice(checked((int)sprite), resource.Bytes.Length).SequenceEqual(resource.Bytes.AsSpan()) || Word(resource.Bytes, 8) != (index == 2 ? 16 : 15))
            {
                throw new InvalidDataException("Curse typed sprite resource disagrees with its original node-bank owner.");
            }
        }
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int geometries, int workspaceBytes)
    {
        if (Dword(source.Data, 0) != geometries)
        {
            throw new InvalidDataException("Curse ACT geometry directory changes its original count.");
        }
        int animation = checked((int)Dword(source.Data, 4));
        if (animation < 8 + geometries * 4 || animation > source.Data.Length - 8 || Dword(source.Data, animation) != geometries || Dword(source.Data, animation + 4) != 40)
        {
            throw new InvalidDataException("Curse ACT animation directory leaves its original source or changes its programme.");
        }
        int next = 8 + geometries * 4;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int shape = 0; shape < geometries; shape++)
        {
            int start = checked((int)Dword(source.Data, 8 + shape * 4));
            int end = shape + 1 == geometries ? animation : checked((int)Dword(source.Data, 12 + shape * 4));
            int vertices = checked((int)Dword(source.Data, start + 4));
            int variants = checked((int)Dword(source.Data, start + 8));
            int faces = checked((int)Dword(source.Data, start));
            if (start != next || end <= start || end > animation || vertices is < 1 or > 105 || variants is < 1 or > 2 || faces != checked(12 + vertices * variants * 8))
            {
                throw new InvalidDataException("Curse ACT geometry does not retain its original bounded twelve-byte header.");
            }
            int cursor = checked(start + faces);
            for (int group = 0; group < 8; group++)
            {
                int count = checked((int)Dword(source.Data, cursor));
                cursor = checked(cursor + 4);
                if (count > 4096 || cursor > end - checked(count * strides[group]))
                {
                    throw new InvalidDataException("Curse ACT primitive directory leaves its shape.");
                }
                for (int face = 0; face < count; face++)
                {
                    for (int corner = 0; corner < (group % 2 == 0 ? 3 : 4); corner++)
                    {
                        int vertex = Word(source.Data, cursor + face * strides[group] + 4 + corner * 2) * 4;
                        if ((vertex & 7) != 0 || vertex > vertices * 8 - 8)
                        {
                            throw new InvalidDataException("Curse ACT face selects a vertex outside its original shape.");
                        }
                    }
                }
                cursor = checked(cursor + count * strides[group]);
            }
            if (cursor != end)
            {
                throw new InvalidDataException("Curse ACT primitive groups do not close their original shape.");
            }
            next = end;
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaceBytes].ToImmutableArray()).Initialize7016B0(source, workspaceBytes);
        for (int frame = 0; frame < 40; frame++)
        {
            NativeBattleGfAnimationAdvance result = workspace.Advance701970(source, 0, (retained, node) =>
            {
                if (node.GeometryIndex >= geometries)
                {
                    throw new InvalidDataException("Curse ACT node selects an absent original shape.");
                }
                return retained;
            });
            if (result.Remaining != 39 - frame)
            {
                throw new InvalidDataException("Curse ACT source does not retain its forty actual frames.");
            }
            workspace = result.Workspace;
        }
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(data, offset, 4));
    private static ushort Word(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Slice(data, offset, 2));
    private static ReadOnlySpan<byte> Slice(ImmutableArray<byte> data, int offset, int length)
    {
        if (data.IsDefault || offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw new InvalidDataException("Curse resource read leaves its authenticated source extent.");
        }
        return data.AsSpan().Slice(offset, length);
    }
}
