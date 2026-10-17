using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private sealed record NativeAnimationInputSpan(int Start, byte[] Bytes, int Offset, int Length);
    private sealed record NativeAnimationModelLayout(int ResourceBytes, int InstanceBytes, int GeometryOffset, byte[]? MainCharacter, int OneOffset, int OneSize);

    /// <summary>Resolves resource words for ordinary fresh field allocation; retained physical snapshots keep their own existing words.</summary>
    public IReadOnlyList<ushort?> ReadNativeEmptyAnimationTerminals(int fieldId, IReadOnlyList<int> boundModelIds)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw UnsupportedRuntime("Native animation allocation requires an authenticated runtime profile.");
        }
        ushort?[] result = new ushort?[boundModelIds.Count];
        if (!boundModelIds.Contains(-1))
        {
            return result;
        }
        if ((uint)fieldId >= fieldNames.Length || string.IsNullOrWhiteSpace(fieldNames[fieldId]))
        {
            throw new InvalidDataException("Native animation allocation requires an actual field archive.");
        }
        using IFlArchive field = OpenNested(fieldNames[fieldId]);
        byte[] one = Read(field, "chara.one");
        int[] unique = boundModelIds.Where(model => model >= 0).Distinct().ToArray();
        Dictionary<int, NativeAnimationModelLayout> models = ReadNativeAnimationModelLayouts(one, unique);
        if (boundModelIds.Any(model => model < -1 || model >= 0 && !models.ContainsKey(model)))
        {
            throw new InvalidDataException("Native animation allocation references an absent model.");
        }
        if (unique.Length == 0)
        {
            return result;
        }
        List<NativeAnimationInputSpan> spans = [];
        List<(int Start, int Length)> pointerWrites = [];
        int input = 4;
        foreach (int model in unique)
        {
            NativeAnimationModelLayout layout = models[model];
            spans.Add(new NativeAnimationInputSpan(input, one, layout.OneOffset, layout.OneSize));
            if (layout.MainCharacter is null)
            {
                pointerWrites.Add((checked(input + layout.GeometryOffset + 56), 4));
            }
            input = checked(input + layout.OneSize);
        }
        foreach (int model in unique)
        {
            NativeAnimationModelLayout layout = models[model];
            if (layout.MainCharacter is byte[] main)
            {
                spans.Add(new NativeAnimationInputSpan(input, main, 0, main.Length));
                pointerWrites.Add((checked(input + layout.GeometryOffset + 56), 4));
                input = checked(input + main.Length);
            }
        }
        // The original loader reads into its scratch slab, relocates that slab
        // to the arena top, then constructs resources and renderer instances.
        int preModel = ReadNativePreModelCursor(fieldId);
        int relocatedStart = checked((0x11C800 - preModel - input) & ~3);
        int output = unique.Sum(model => models[model].ResourceBytes);
        for (int index = 0; index < boundModelIds.Count; index++)
        {
            int model = boundModelIds[index];
            if (model == -1)
            {
                int word = checked(output + 12);
                int source = checked(word - 0x20000);
                bool relocated = word < relocatedStart + input && word + 2 > relocatedStart;
                bool patched = pointerWrites.Any(write => source < write.Start + write.Length && source + 2 > write.Start);
                NativeAnimationInputSpan? span = spans.SingleOrDefault(candidate => source >= candidate.Start && source + 2 <= candidate.Start + candidate.Length);
                if (!relocated && !patched && span is not null)
                {
                    result[index] = BinaryPrimitives.ReadUInt16LittleEndian(span.Bytes.AsSpan(checked(span.Offset + source - span.Start), 2));
                }
            }
            output = checked(output + (model == -1 ? 152 : models[model].InstanceBytes));
        }
        return result;
    }

    private Dictionary<int, NativeAnimationModelLayout> ReadNativeAnimationModelLayouts(byte[] one, IReadOnlyList<int> selected)
    {
        using BinaryReader reader = new(new MemoryStream(one, false));
        int count = checked((int)reader.ReadUInt32());
        if (count > 100)
        {
            throw new InvalidDataException("Native ONE model count is outside its pointer table.");
        }
        Dictionary<int, NativeAnimationModelLayout> result = [];
        for (int index = 0; index < count; index++)
        {
            int offset = checked((int)reader.ReadUInt32() + 4);
            int size = checked((int)reader.ReadUInt32());
            reader.ReadUInt32();
            uint flag = reader.ReadUInt32();
            int geometry;
            if ((flag >> 28) == 0xD)
            {
                reader.ReadUInt32();
                geometry = -1;
            }
            else if ((flag >> 28) == 0xA)
            {
                reader.ReadUInt32();
                geometry = checked((int)reader.ReadUInt32());
            }
            else
            {
                int textures = 0;
                while (reader.ReadUInt32() != uint.MaxValue)
                {
                    if (++textures > 100)
                    {
                        throw new InvalidDataException("Native ONE texture table is unbounded.");
                    }
                }
                geometry = checked((int)reader.ReadUInt32());
            }
            string name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
            reader.ReadUInt32();
            if (offset < 0 || size < 0 || offset > one.Length - size)
            {
                throw new InvalidDataException("Native ONE payload lies outside its resource.");
            }
            if (!selected.Contains(index))
            {
                continue;
            }
            byte[]? main = null;
            byte[] data = one;
            int header;
            int animation;
            if ((flag >> 28) == 0xD)
            {
                main = ReadMainCharacter(name[..4]);
                data = main;
                int pointer = 0;
                while (NativeAnimationUInt32(main, pointer) != uint.MaxValue)
                {
                    pointer = checked(pointer + 4);
                }
                geometry = checked((int)NativeAnimationUInt32(main, pointer + 4));
                header = geometry;
                animation = offset;
            }
            else
            {
                header = checked(offset + geometry);
                animation = checked(header + (int)NativeAnimationUInt32(data, header + 56));
            }
            int bones = checked((int)NativeAnimationUInt32(data, header));
            int vertices = checked((int)NativeAnimationUInt32(data, header + 4));
            int eyes = checked((int)NativeAnimationUInt32(data, header + 8));
            int faces = checked((int)NativeAnimationUInt32(data, header + 12));
            int parts = checked((int)NativeAnimationUInt32(data, header + 16));
            int skins = checked((int)NativeAnimationUInt32(data, header + 20));
            uint untextured = NativeAnimationUInt32(data, header + 24);
            uint textured = NativeAnimationUInt32(data, header + 28);
            int triangles = (ushort)untextured;
            int quads = (int)(untextured >> 16);
            int texturedTriangles = (ushort)textured;
            int texturedQuads = (int)(textured >> 16);
            if (faces != triangles + quads + texturedTriangles + texturedQuads)
            {
                throw new InvalidDataException("Native animation geometry has inconsistent primitive counts.");
            }
            int clips = NativeAnimationUInt16(one, animation);
            int cursor = checked(animation + 2);
            int units = 0;
            for (int clip = 0; clip < clips; clip++)
            {
                byte[] animationData = main is null ? data : one;
                int frames = NativeAnimationUInt16(animationData, cursor);
                int animationBones = NativeAnimationUInt16(animationData, cursor + 2);
                if (animationBones != bones)
                {
                    throw new InvalidDataException("Native animation and geometry disagree on the bone count.");
                }
                cursor = checked(cursor + 4 + frames * (6 + 4 * animationBones));
                if (cursor > animationData.Length)
                {
                    throw new InvalidDataException("Native animation frames lie outside their resource.");
                }
                units = checked(units + frames * (2 + animationBones));
            }
            int resource = checked(96 + (bones + 1) * 64 + 24 * triangles + 32 * quads + 24 * texturedTriangles + 28 * texturedQuads + 8 * eyes + 8 * vertices + 32 * parts + 8 * skins + 4 * clips + 4 * units);
            int instance = checked((152 + (bones + 2) * 64 + 2 * faces + 3) & ~3);
            instance = checked(instance + 2 * (28 * triangles + 36 * quads + 32 * texturedTriangles + 40 * texturedQuads));
            result[index] = new NativeAnimationModelLayout(resource, instance, geometry, main, offset, size);
        }
        return result;
    }

    private static uint NativeAnimationUInt32(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Native model DWORD lies outside its resource.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static ushort NativeAnimationUInt16(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 2)
        {
            throw new InvalidDataException("Native model WORD lies outside its resource.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }
}
