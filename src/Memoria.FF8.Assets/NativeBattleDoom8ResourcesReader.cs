using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDoom8ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(5);
        for (short id = 383; id <= 387; id++)
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + (uint)id * 4, 4));
            byte[] bytes = memory.Read(address, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end <= 0)
            {
                throw new InvalidDataException("Doom archive directory requires its original terminated file name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleDoom8Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x163ACC4, 11)) != "mag007.tim\0")
        {
            throw new InvalidDataException("Doom requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file is null || file.FileId != names[index].FileId || !string.Equals(file.Name, names[index].Name, StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new InvalidDataException("Doom archive resources disagree with the executable file directory.");
        }
        ImmutableArray<byte> modelBytes = memory.Read(0x1636CE8, 16332).ToImmutableArray();
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(modelBytes.AsSpan(), index)).ToArray();
        if (!sections.SequenceEqual(new[] { 0x18, 0x568, 0x2E28, 0x3FCC, 0x3FCC }))
        {
            throw new InvalidDataException("Doom requires its complete four-section embedded model.");
        }
        NativeBattlePoseSource pose = new(modelBytes.Slice(sections[0], sections[1] - sections[0]), modelBytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> animations = NativeBattlePoseDecoder.DecodeFrames(pose.Animations.AsSpan(), pose.BoneCount);
        if (!animations.Select(animation => animation.Length).SequenceEqual(new[] { 1, 16, 6, 14, 1, 24, 18 }))
        {
            throw new InvalidDataException("Doom requires all seven original skeletal animation programmes.");
        }
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(modelBytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        NativeBattleEffectOwnedModelResource model = new(0x1636CE8, modelBytes, pose, geometry);
        NativeBattleDoom8Resources result = new(mainTim, new("FF8_EN:16358D8", memory.Read(0x16358D8, 4908).ToImmutableArray()), files,
            [new("FF8_EN:181CFAC", memory.Read(0x181CFAC, 33824).ToImmutableArray()), new("FF8_EN:18253CC", memory.Read(0x18253CC, 16928).ToImmutableArray())],
            new("FF8_EN:1636C78", memory.Read(0x1636C78, 112).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1634F24, 4)), new(memory.Read(0x1818B78, 17460).ToImmutableArray()) { ResourceAddress = 0x1818B78 }, model)
        {
            StripTables = ReadStripTables(memory),
            PlacementSelectors = ReadWords(memory, 0x1636C34, 5),
            OverlayColors = Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x163ACB4u + (uint)index * 4, 4))).ToImmutableArray(),
            ColdTargetPointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1636C04, 4)),
            DustSprite = new(0, 0x1634F28, 0x8CD770, 0x8CD890, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1634F28)),
            SmokeMesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x16350F0)
        };
        result.Validate();
        _ = result.RequireDustSprite();
        _ = result.RequireSmokeMesh();
        _ = result.RequireStripTables();
        _ = result.RequirePlacementSelectors();
        _ = result.RequireOverlayColors();
        _ = result.RequireColdTargetPointer();
        ValidateNodeResources(memory, result.NodeSource);
        ValidateTim(result.MainTim.Data, 320, 240, 256, 8, 384, 256, 320, 256);
        ValidateTim(result.DirectorTims[0].Data, 320, 224, 256, 2, 832, 256, 64, 256);
        ValidateTim(result.DirectorTims[1].Data, 320, 226, 256, 1, 768, 256, 64, 128);
        ValidateTim(files[3].Data, 320, 244, 256, 1, 704, 256, 64, 256);
        ValidateTim(files[4].Data, 320, 245, 256, 1, 768, 384, 56, 128);
        ValidateAct(files[0], 2, 6192, 3, 36, 192);
        ValidateAct(files[1], 2, 2760, 3, 10, 140);
        ValidateAct(files[2], 5, 7632, 14, 30, 944);
        return result;
    }

    private static NativeBattleDoom8StripTables ReadStripTables(NativeExecutableMemory memory)
    {
        ImmutableArray<short> ranges = ReadWords(memory, 0x1636C40, 28);
        return new(ReadWords(memory, 0x1636C08, 4), ReadWords(memory, 0x1636C10, 18), Enumerable.Range(0, 14).Select(index => new NativeBattleDoom8StripRange(ranges[index * 2], ranges[index * 2 + 1])).ToImmutableArray());
    }

    private static ImmutableArray<short> ReadWords(NativeExecutableMemory memory, uint address, int count)
    {
        byte[] bytes = memory.Read(address, checked(count * 2));
        return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
    }

    private static void ValidateNodeResources(NativeExecutableMemory memory, NativeBattleCuragaExecutableSource source)
    {
        for (int index = 0; index < 16; index++)
        {
            uint sprite = Pointer(0x44, index);
            if (sprite != 0)
            {
                ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, checked(source.ResourceAddress + sprite));
                RequireBounded(sprite, bytes.Length);
            }
            uint mesh = Pointer(0x84, index);
            if (mesh != 0)
            {
                NativeBattleStaticMeshResource resource = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, checked(source.ResourceAddress + mesh));
                RequireBounded(mesh, resource.Bytes.Length);
            }
            uint morph = Pointer(0xC4, index);
            if (morph != 0)
            {
                ReadOnlySpan<byte> header = source.Data.AsSpan().Slice(checked((int)morph), 8);
                int count = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
                int length = checked(8 + count * 8);
                if (count != 84 || BinaryPrimitives.ReadInt32LittleEndian(header) != length)
                {
                    throw new InvalidDataException("Doom requires both original 84-vertex morph sources.");
                }
                RequireBounded(morph, length);
            }
        }

        uint Pointer(int directory, int index) => BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(directory + index * 4));

        void RequireBounded(uint offset, int length)
        {
            if (length < 0 || offset > source.Data.Length || length > source.Data.Length - offset)
            {
                throw new InvalidDataException("Doom embedded node payload crosses into another resource.");
            }
        }
    }

    private static void ValidateTim(ImmutableArray<byte> data, ushort cx, ushort cy, ushort cw, ushort ch, ushort ix, ushort iy, ushort iw, ushort ih)
    {
        ReadOnlySpan<byte> bytes = data.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != 9)
        {
            throw new InvalidDataException("Doom requires its original indexed TIM format.");
        }
        int cursor = 8;
        Block(cx, cy, cw, ch);
        Block(ix, iy, iw, ih);
        if (cursor != data.Length || WorldAssetReader.ReadTim(data.ToArray(), 0).End != data.Length)
        {
            throw new InvalidDataException("Doom texture blocks must close their complete original resource.");
        }

        void Block(ushort x, ushort y, ushort width, ushort height)
        {
            ReadOnlySpan<byte> block = data.AsSpan().Slice(cursor);
            int length = checked(12 + width * height * 2);
            if (BinaryPrimitives.ReadInt32LittleEndian(block) != length || BinaryPrimitives.ReadUInt16LittleEndian(block[4..]) != x || BinaryPrimitives.ReadUInt16LittleEndian(block[6..]) != y || BinaryPrimitives.ReadUInt16LittleEndian(block[8..]) != width || BinaryPrimitives.ReadUInt16LittleEndian(block[10..]) != height || cursor > data.Length - length)
            {
                throw new InvalidDataException("Doom texture block dimensions disagree with their original upload resource.");
            }
            cursor = checked(cursor + length);
        }
    }

    private static void ValidateAct(NativeBattleEffectFileAsset file, int geometryCount, int animationOffset, int nodeCount, int duration, int workspaceBytes)
    {
        if (I32(0) != geometryCount || I32(4) != animationOffset || I32(animationOffset) != nodeCount || I32(animationOffset + 4) != duration)
        {
            throw new InvalidDataException("Doom ACT requires its actual geometry directory and animation tail.");
        }
        int previous = 8 + geometryCount * 4;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int geometry = 0; geometry < geometryCount; geometry++)
        {
            int start = I32(8 + geometry * 4);
            int end = geometry + 1 == geometryCount ? animationOffset : I32(12 + geometry * 4);
            if (start != previous || end <= start || end > animationOffset)
            {
                throw new InvalidDataException("Doom ACT geometry sections must form their complete bounded directory.");
            }
            int count = I32(start + 4);
            int variants = I32(start + 8);
            int faceOffset = I32(start);
            if (count <= 0 || variants is < 1 or > 2 || faceOffset != checked(12 + count * variants * 8))
            {
                throw new InvalidDataException("Doom ACT requires its original packed vertex variants.");
            }
            int cursor = checked(start + faceOffset);
            for (int group = 0; group < 8; group++)
            {
                int faces = I32(cursor);
                cursor = checked(cursor + 4);
                if (faces < 0 || faces > (end - cursor) / strides[group] || faces > 0 && group is not (3 or 4 or 5))
                {
                    throw new InvalidDataException("Doom ACT primitive groups leave their original geometry section.");
                }
                for (int face = 0; face < faces; face++)
                {
                    for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                    {
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(file.Data.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2)) * 4;
                        if ((vertex & 7) != 0 || vertex >= count * 8)
                        {
                            throw new InvalidDataException("Doom ACT face references an absent packed vertex.");
                        }
                    }
                }
                cursor = checked(cursor + faces * strides[group]);
            }
            if (cursor != end)
            {
                throw new InvalidDataException("Doom ACT face records do not close their geometry section.");
            }
            previous = end;
        }
        NativeBattleGfAnimationSource source = new(0, file.Data);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaceBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.GeometryIndex >= geometryCount)
                {
                    throw new InvalidDataException("Doom ACT animation selects an absent geometry source.");
                }
                return current;
            });
            if (step.Remaining != duration - frame - 1)
            {
                throw new InvalidDataException("Doom ACT animation does not preserve its original duration.");
            }
            workspace = step.Workspace;
        }

        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(file.Data.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDoom8Resources? nativeDoom8Resources;

    public NativeBattleDoom8Resources ReadNativeDoom8Resources()
    {
        if (nativeDoom8Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleDoom8ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset mainTim = new("mag007.tim", Read(magic, "mag007.tim").ToImmutableArray());
            nativeDoom8Resources = NativeBattleDoom8ResourcesReader.Read(executable, mainTim, files);
        }
        return nativeDoom8Resources;
    }
}
