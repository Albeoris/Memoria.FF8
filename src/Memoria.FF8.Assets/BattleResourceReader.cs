using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

/// <summary>Bounded original DAT skeleton, geometry, compressed animation and X static-stage decoder.</summary>
internal static partial class BattleResourceReader
{
    private sealed record ModelTexture(TimImage Tim, PixelImage Image);

    private static BinaryReader Reader(byte[] bytes) => new(new MemoryStream(bytes, false));
    private static int Count(int count, int maximum, string name)
    {
        if (count < 0 || count > maximum)
        {
            throw new InvalidDataException($"Battle {name} count {count} exceeds its supported bounds.");
        }
        return count;
    }

    private static int[] Directory(BinaryReader reader)
    {
        int count = Count(reader.ReadInt32(), 16, "sections");
        int[] offsets = Enumerable.Range(0, count).Select(_ => reader.ReadInt32()).ToArray();
        int length = reader.ReadInt32();
        if (length != reader.BaseStream.Length || offsets.Where((offset, index) => offset < 8 + count * 4 || offset > length || (index > 0 && offset < offsets[index - 1])).Any())
        {
            throw new InvalidDataException("Battle DAT section directory is invalid.");
        }
        return offsets;
    }

    public static BattleModel ReadModel(string name, byte[] bytes, int? textureSectionOverride = null)
    {
        using BinaryReader reader = Reader(bytes);
        int[] offsets = Directory(reader);
        if (offsets.Length is not (7 or 8 or 9 or 10 or 11))
        {
            throw new NotSupportedException($"{name}: {offsets.Length}-section battle model has no supported skeleton contract.");
        }
        reader.BaseStream.Position = offsets[0];
        int boneCount = Count(reader.ReadUInt16(), 128, "bones");
        float scale = reader.ReadUInt16() / 2048f * 12;
        BattleBone[] bones = new BattleBone[boneCount];
        for (int index = 0; index < boneCount; index++)
        {
            reader.BaseStream.Position = offsets[0] + 16 + index * 48;
            ushort parent = reader.ReadUInt16();
            short length = reader.ReadInt16();
            if (parent != ushort.MaxValue && parent >= index)
            {
                throw new InvalidDataException($"{name}: bone {index} references nonancestor {parent}.");
            }
            bones[index] = new BattleBone(parent == ushort.MaxValue ? -1 : parent, length / 2048f);
        }
        int textureSection = textureSectionOverride ?? (offsets.Length == 11 ? 10 : offsets.Length is 9 or 10 ? 8 : 5);
        IReadOnlyList<ModelTexture> textures = Textures(reader, bytes, offsets[textureSection]);
        IReadOnlyList<BattleMesh> meshes = Geometry(reader, offsets[1], bones.Length, textures);
        IReadOnlyList<BattleAnimation> animations = Animations(reader, offsets[2], bones.Length);
        return new BattleModel(name, bones, meshes, textures.Select(texture => texture.Image).ToArray(), animations, scale)
        {
            NativePoseSource = new(bytes.AsSpan(offsets[0], 16 + boneCount * 48).ToArray().ToImmutableArray(), bytes.AsSpan(offsets[2], offsets.Where(offset => offset > offsets[2]).DefaultIfEmpty(bytes.Length).Min() - offsets[2]).ToArray().ToImmutableArray()),
            NativeActionCameraBank = offsets.Length is 9 or 10 && offsets[6] != offsets[7] ? new(bytes.AsSpan(offsets[6], offsets[7] - offsets[6]).ToArray().ToImmutableArray()) : null,
            Sequences = name.StartsWith("c0m", StringComparison.OrdinalIgnoreCase) ? Sequences(bytes, offsets, 3) : offsets.Length is 9 or 10 ? Sequences(bytes, offsets, 5) : null
        };
    }

    public static BattleModel ReadWeapon(string name, byte[] bytes, BattleModel character)
    {
        using BinaryReader reader = Reader(bytes);
        int[] offsets = Directory(reader);
        if (offsets.Length is 7 or 8)
        {
            return ReadModel(name, bytes, 6) with { Sequences = Sequences(bytes, offsets, 3) };
        }
        if (offsets.Length != 5)
        {
            throw new NotSupportedException($"{name}: independent animated weapons require their own skeleton binding.");
        }
        IReadOnlyList<ModelTexture> textures = Textures(reader, bytes, offsets[4]);
        return new BattleModel(name, character.Bones, Geometry(reader, offsets[0], character.Bones.Count, textures), textures.Select(texture => texture.Image).ToArray(), character.Animations, character.Scale)
        {
            NativePoseSource = character.NativePoseSource,
            Sequences = Sequences(bytes, offsets, 1)
        };
    }

    private static BattleSequenceSet Sequences(byte[] bytes, int[] offsets, int section)
    {
        int start = offsets[section];
        int end = offsets.Where(offset => offset > start).DefaultIfEmpty(bytes.Length).Min();
        return new BattleSequenceSet(bytes[start..end]);
    }

    private static IReadOnlyList<BattleMesh> Geometry(BinaryReader reader, int offset, int boneCount, IReadOnlyList<ModelTexture> textures)
    {
        reader.BaseStream.Position = offset;
        int count = Count(reader.ReadInt32(), 256, "objects");
        int[] objects = Enumerable.Range(0, count).Select(_ => checked(offset + reader.ReadInt32())).ToArray();
        List<BattleMesh> meshes = [];
        foreach (int position in objects)
        {
            reader.BaseStream.Position = position;
            int groups = Count(reader.ReadUInt16(), 1024, "vertex groups");
            List<BattleVertex> vertices = [];
            for (int group = 0; group < groups; group++)
            {
                int bone = reader.ReadUInt16();
                int vertexCount = Count(reader.ReadUInt16(), 8192, "vertices");
                if (bone >= boneCount)
                {
                    throw new InvalidDataException($"Battle geometry references bone {bone} outside {boneCount}.");
                }
                for (int index = 0; index < vertexCount; index++)
                {
                    short x = reader.ReadInt16();
                    short y = reader.ReadInt16();
                    short z = reader.ReadInt16();
                    vertices.Add(new BattleVertex(new Vector3(-x, -y, z) / 2048f, bone));
                }
            }
            reader.BaseStream.Position = (reader.BaseStream.Position + 3) & ~3L;
            int triangles = Count(reader.ReadUInt16(), 16384, "triangles");
            int quads = Count(reader.ReadUInt16(), 16384, "quads");
            if (reader.ReadUInt64() != 0)
            {
                throw new NotSupportedException("Battle geometry polygon-header flags are outside the supported contract.");
            }
            List<BattleFace> faces = [];
            for (int index = 0; index < triangles + quads; index++)
            {
                bool quad = index >= triangles;
                int a = reader.ReadUInt16() & 4095;
                int b = reader.ReadUInt16() & 4095;
                int c = reader.ReadUInt16() & 4095;
                int d = quad ? reader.ReadUInt16() & 4095 : 0;
                Vector2 uv0 = Uv(reader);
                Vector2 uv1 = quad ? default : Uv(reader);
                int texture = (reader.ReadUInt16() >> 6) & 7;
                if (quad)
                {
                    uv1 = Uv(reader);
                }
                Vector2 uv2 = default;
                Vector2 uv3 = default;
                if (!quad)
                {
                    uv2 = Uv(reader);
                }
                ushort page = reader.ReadUInt16();
                if (quad)
                {
                    uv2 = Uv(reader);
                    uv3 = Uv(reader);
                }
                int[] indices = quad ? [a, b, c, d] : [c, a, b];
                if (texture >= textures.Count || indices.Any(vertex => vertex >= vertices.Count))
                {
                    throw new InvalidDataException("Battle polygon references an absent texture or vertex.");
                }
                ModelTexture material = textures[texture];
                if (((page >> 7) & 3) != (int)material.Tim.PixelMode || material.Tim.PixelMode == TimPixelMode.Direct24)
                {
                    throw new NotSupportedException("Battle polygon texture page and TIM pixel modes disagree or are unsupported.");
                }
                // DAT UVs address the PSX texture page, whereas Decode returns only the TIM rectangle.
                // Page X is in 64-word units; each word contains 4, 2 or 1 indexed/direct texels.
                int pixelsPerWord = 4 >> (int)material.Tim.PixelMode;
                Vector2 origin = new(((page & 15) * 64 - material.Tim.X) * pixelsPerWord, ((page & 16) * 16) - material.Tim.Y);
                Vector2[] uv = quad ? [uv0, uv1, uv2, uv3] : [uv0, uv1, uv2];
                for (int corner = 0; corner < uv.Length; corner++)
                {
                    uv[corner] += origin;
                }
                faces.Add(new BattleFace(indices, uv, texture, new Rgba32(128, 128, 128, 255)));
            }
            meshes.Add(new BattleMesh(vertices, faces));
        }
        return meshes;
    }

    private static Vector2 Uv(BinaryReader reader) => new(reader.ReadByte(), reader.ReadByte());

    private static IReadOnlyList<ModelTexture> Textures(BinaryReader reader, byte[] bytes, int offset)
    {
        reader.BaseStream.Position = offset;
        int count = Count(reader.ReadInt32(), 16, "textures");
        int[] pointers = Enumerable.Range(0, count).Select(_ => checked(offset + reader.ReadInt32())).ToArray();
        return pointers.Select(pointer => ReadTim(bytes, pointer).Images[0]).Select(tim => new ModelTexture(tim, tim.Decode())).ToArray();
    }

    private static TimFile ReadTim(byte[] bytes, int offset)
    {
        using BinaryReader reader = Reader(bytes);
        reader.BaseStream.Position = offset;
        if (reader.ReadUInt32() != 16)
        {
            throw new InvalidDataException($"Original TIM magic is absent at 0x{offset:X}.");
        }
        uint flags = reader.ReadUInt32();
        if ((flags & 8) != 0)
        {
            int paletteSize = reader.ReadInt32();
            reader.BaseStream.Position += paletteSize - 4;
        }
        int imageSize = reader.ReadInt32();
        reader.BaseStream.Position += imageSize - 4;
        return TimFile.Read(bytes.AsSpan(offset, checked((int)reader.BaseStream.Position - offset)));
    }

    private static IReadOnlyList<BattleAnimation> Animations(BinaryReader reader, int offset, int bones)
    {
        reader.BaseStream.Position = offset;
        byte[] section = reader.ReadBytes(checked((int)(reader.BaseStream.Length - offset)));
        return Memoria.FF8.Runtime.NativeBattlePoseDecoder.DecodeFrames(section, bones).Select(frames => new BattleAnimation(frames.Select(pose => new BattlePose(new Vector3(pose.Translation.X, pose.Translation.Y, pose.Translation.Z) / 100f, pose.Bones.Select(bone => new Vector3(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z) * (MathF.Tau / 4096)).ToArray(), pose.Bones.Select(bone => new Vector3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z) / 1024f).ToArray())).ToArray())).ToArray();
    }

    public static BattleStage ReadStage(int stageId, byte[] bytes)
    {
        using BinaryReader reader = Reader(bytes);
        int header = -1;
        for (int offset = 0; offset <= bytes.Length - 32; offset += 4)
        {
            reader.BaseStream.Position = offset;
            if (reader.ReadInt32() != 6)
            {
                continue;
            }
            int[] pointers = Enumerable.Range(0, 7).Select(_ => reader.ReadInt32()).ToArray();
            if (pointers[6] != bytes.Length - offset || pointers.Any(pointer => pointer < 32 || pointer > pointers[6]) || pointers[5] > bytes.Length - offset - 4)
            {
                continue;
            }
            reader.BaseStream.Position = offset + pointers[5];
            if (reader.ReadInt32() != 16)
            {
                continue;
            }
            if (header != -1)
            {
                throw new InvalidDataException("Battle stage has multiple valid geometry directories.");
            }
            header = offset;
        }
        if (header == -1)
        {
            throw new NotSupportedException($"Stage {stageId} has no bounded original geometry directory.");
        }
        reader.BaseStream.Position = header + 4;
        int[] sections = Enumerable.Range(0, 6).Select(_ => header + reader.ReadInt32()).ToArray();
        TimImage tim = ReadTim(bytes, sections[5]).Images[0];
        List<PixelImage> textures = [];
        Dictionary<int, int> palettes = [];
        List<BattleMesh> meshes = [];
        for (int group = 0; group < 4; group++)
        {
            reader.BaseStream.Position = sections[group];
            if (reader.ReadInt32() != 3)
            {
                throw new NotSupportedException("Stage group requires an unsupported section contract.");
            }
            reader.ReadInt32();
            int list = sections[group] + reader.ReadInt32();
            reader.BaseStream.Position = list;
            int count = Count(reader.ReadInt32(), 256, "stage objects");
            int[] objects = Enumerable.Range(0, count).Select(_ => list + reader.ReadInt32()).ToArray();
            foreach (int position in objects)
            {
                reader.BaseStream.Position = position;
                uint objectHeader = reader.ReadUInt32();
                if (objectHeader != 0x00010001)
                {
                    throw new NotSupportedException($"Stage {stageId} object header 0x{objectHeader:X8} is unsupported.");
                }
                int verticesCount = Count(reader.ReadUInt16(), 8192, "stage vertices");
                BattleVertex[] vertices = new BattleVertex[verticesCount];
                for (int vertex = 0; vertex < verticesCount; vertex++)
                {
                    vertices[vertex] = new BattleVertex(new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) / 100f, 0);
                }
                reader.BaseStream.Position += reader.BaseStream.Position % 4 + 4;
                int triangles = reader.ReadUInt16();
                int quads = reader.ReadUInt16();
                reader.ReadUInt32();
                List<BattleFace> faces = [];
                for (int face = 0; face < triangles + quads; face++)
                {
                    bool quad = face >= triangles;
                    int[] indices = new int[quad ? 4 : 3];
                    for (int index = 0; index < indices.Length; index++)
                    {
                        indices[index] = reader.ReadUInt16();
                    }
                    Vector2 uv0 = Uv(reader);
                    Vector2 uv1 = quad ? default : Uv(reader);
                    ushort clut = reader.ReadUInt16();
                    if (quad)
                    {
                        uv1 = Uv(reader);
                    }
                    Vector2 uv2 = quad ? default : Uv(reader);
                    int page = reader.ReadByte() & 15;
                    reader.ReadByte();
                    Vector2 uv3 = default;
                    if (quad)
                    {
                        uv2 = Uv(reader);
                        uv3 = Uv(reader);
                    }
                    Rgba32 color = new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 255);
                    reader.ReadByte();
                    int palette = (clut >> 6) & 15;
                    if (!palettes.TryGetValue(palette, out int texture))
                    {
                        texture = textures.Count;
                        textures.Add(tim.Decode(palette));
                        palettes.Add(palette, texture);
                    }
                    Vector2[] uv = quad ? [uv0, uv1, uv2, uv3] : [uv1, uv2, uv0];
                    for (int index = 0; index < uv.Length; index++)
                    {
                        uv[index].X += page * 128;
                    }
                    if (indices.Any(index => index >= vertices.Length))
                    {
                        throw new InvalidDataException("Battle stage polygon references an absent vertex.");
                    }
                    faces.Add(new BattleFace(indices, uv, texture, color));
                }
                meshes.Add(new BattleMesh(vertices, faces));
            }
        }
        return new BattleStage(stageId, meshes, textures)
        {
            IntroductionCameras = BattleStageCameraReader.Read(stageId, bytes, header),
            NativeCameraBank = BattleStageCameraReader.ReadNativeBank(stageId, bytes, header),
            NativeCameraControl = BattleStageCameraReader.ReadNativeControl(stageId, bytes, header)
        };
    }
}
