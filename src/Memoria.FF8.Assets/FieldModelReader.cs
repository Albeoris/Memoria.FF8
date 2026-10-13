using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

/// <summary>Selected field ONE/MCH structures. Archive, image and text formats are handled by Albeoris.</summary>
internal static class FieldModelReader
{
    private sealed record Header(int Offset, int Size, uint Flag, int ModelOffset, int[] Textures, string Name);

    public static IReadOnlyList<FieldModel> Read(byte[] one, Func<string, byte[]> mainCharacter)
    {
        using BinaryReader reader = Reader(one);
        int count = Count(reader.ReadUInt32(), 100, "ONE models");
        List<Header> headers = [];
        for (int i = 0; i < count; i++)
        {
            int offset = checked((int)reader.ReadUInt32() + 4);
            int size = checked((int)reader.ReadUInt32());
            reader.ReadUInt32();
            uint flag = reader.ReadUInt32();
            List<int> textures = [];
            int modelOffset = -1;
            if ((flag >> 24) == 0xD0)
            {
                reader.ReadUInt32();
            }
            else if ((flag >> 24) == 0xA0)
            {
                reader.ReadUInt64();
            }
            else
            {
                textures.Add(checked((int)(flag & 0xFFFFFF)));
                uint value;
                while ((value = reader.ReadUInt32()) != uint.MaxValue)
                {
                    textures.Add(checked((int)(value & 0xFFFFFF)));
                    if (textures.Count > 100)
                    {
                        throw new InvalidDataException("ONE texture pointer table is unbounded.");
                    }
                }
                modelOffset = checked((int)reader.ReadUInt32());
            }
            string name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
            reader.ReadUInt32();
            if (offset < 0 || size > one.Length - offset)
            {
                throw new InvalidDataException($"ONE model {name} lies outside its container.");
            }
            headers.Add(new Header(offset, size, flag, modelOffset, textures.ToArray(), name));
        }
        List<FieldModel> models = [];
        foreach (Header header in headers)
        {
            if ((header.Flag >> 24) == 0xD0)
            {
                string referenceName = header.Name[..4];
                byte[] main = mainCharacter(referenceName);
                using BinaryReader global = Reader(main);
                List<int> textures = [];
                uint value;
                while ((value = global.ReadUInt32()) != uint.MaxValue)
                {
                    textures.Add(checked((int)(value & 0xFFFFFF)));
                    if (textures.Count > 100)
                    {
                        throw new InvalidDataException("MCH texture pointer table is unbounded.");
                    }
                }
                int modelOffset = checked((int)global.ReadUInt32());
                models.Add(ReadModel(header.Name, main, modelOffset, textures, 0, one, header.Offset));
            }
            else if (header.ModelOffset >= 0)
            {
                models.Add(ReadModel(header.Name, one, header.Offset + header.ModelOffset, header.Textures, header.Offset, null, 0));
            }
            else
            {
                models.Add(new FieldModel(header.Name, [], [], [], [], []));
            }
        }
        return models;
    }

    internal static FieldModel ReadModel(string name, byte[] content, int modelOffset, IEnumerable<int> textureOffsets, int textureBase, byte[]? animationContent, int animationOffset, IReadOnlyList<ModelAnimation>? animations = null)
    {
        using BinaryReader reader = Reader(content);
        reader.BaseStream.Position = modelOffset;
        int boneCount = Count(reader.ReadUInt32(), 100, "MCH bones");
        int vertexCount = Count(reader.ReadUInt32(), 10000, "MCH vertices");
        reader.ReadUInt32();
        int faceCount = Count(reader.ReadUInt32(), 20000, "MCH faces");
        reader.ReadUInt32();
        int skinCount = Count(reader.ReadUInt32(), 1000, "MCH skin groups");
        reader.ReadUInt32();
        reader.ReadUInt32();
        int boneOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int vertexOffset = checked(modelOffset + (int)reader.ReadUInt32());
        reader.ReadUInt32();
        int faceOffset = checked(modelOffset + (int)reader.ReadUInt32());
        reader.ReadUInt32();
        int skinOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int embeddedAnimation = checked(modelOffset + (int)reader.ReadUInt32());
        uint controlHeader = reader.ReadUInt32();
        int ControlIndex(int value) => value == 255 ? -1 : value;
        ModelControlBones controls = new(ControlIndex((byte)controlHeader), ControlIndex((byte)(controlHeader >> 8)), ControlIndex((byte)(controlHeader >> 16)), controlHeader);
        ModelBone[] bones = new ModelBone[boneCount];
        for (int i = 0; i < bones.Length; i++)
        {
            reader.BaseStream.Position = boneOffset + i * 64;
            int parent = reader.ReadUInt16() - 1;
            reader.ReadUInt16();
            reader.ReadUInt32();
            float length = reader.ReadInt16();
            if (parent >= i)
            {
                throw new InvalidDataException($"{name}: bone {i} has nonancestor parent {parent}.");
            }
            bones[i] = new ModelBone(parent, length);
        }
        ModelVertex[] vertices = new ModelVertex[vertexCount];
        reader.BaseStream.Position = vertexOffset;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new ModelVertex(new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()), 0);
            reader.ReadInt16();
        }
        reader.BaseStream.Position = skinOffset;
        for (int i = 0; i < skinCount; i++)
        {
            int start = reader.ReadInt16();
            int length = reader.ReadInt16();
            int bone = reader.ReadInt16() - 1;
            reader.ReadInt16();
            if (start < 0 || length < 0 || start + length > vertices.Length || bone < 0 || bone >= bones.Length)
            {
                throw new InvalidDataException($"{name}: invalid skin group {i}.");
            }
            for (int vertex = start; vertex < start + length; vertex++)
            {
                vertices[vertex] = vertices[vertex] with { Bone = bone };
            }
        }
        ModelFace[] faces = new ModelFace[faceCount];
        for (int i = 0; i < faces.Length; i++)
        {
            int position = faceOffset + i * 64;
            reader.BaseStream.Position = position;
            uint type = reader.ReadUInt32();
            reader.BaseStream.Position = position + 12;
            int[] indices = Enumerable.Range(0, 4).Select(_ => (int)reader.ReadUInt16()).ToArray();
            reader.BaseStream.Position = position + 44;
            Vector2[] uv = Enumerable.Range(0, 4).Select(_ => new Vector2(reader.ReadByte(), reader.ReadByte())).ToArray();
            reader.ReadUInt16();
            int texture = reader.ReadUInt16();
            bool quad = type == 0x2D010709;
            if (indices.Take(quad ? 4 : 3).Any(vertex => vertex >= vertices.Length))
            {
                throw new InvalidDataException($"{name}: face {i} has an invalid vertex.");
            }
            faces[i] = new ModelFace(indices, uv, texture, quad);
        }
        PixelImage[] textures = textureOffsets.Select(offset => DecodeTim(content, textureBase + offset)).ToArray();
        if (faces.Any(face => face.Texture >= textures.Length))
        {
            throw new InvalidDataException($"{name}: face references an absent texture.");
        }
        animations ??= ReadAnimations(animationContent ?? content, animationContent is null ? embeddedAnimation : animationOffset, boneCount);
        return new FieldModel(name, bones, vertices, faces, textures, animations) { ControlBones = controls };
    }

    private static PixelImage DecodeTim(byte[] content, int offset)
    {
        using BinaryReader reader = Reader(content);
        reader.BaseStream.Position = offset;
        if (reader.ReadUInt32() != 0x10)
        {
            throw new InvalidDataException($"TIM magic missing at 0x{offset:X}.");
        }
        uint flags = reader.ReadUInt32();
        if ((flags & 8) != 0)
        {
            uint clutBytes = reader.ReadUInt32();
            reader.BaseStream.Position += clutBytes - 4;
        }
        uint imageBytes = reader.ReadUInt32();
        reader.BaseStream.Position += imageBytes - 4;
        int length = checked((int)reader.BaseStream.Position - offset);
        return TimFile.Read(content.AsSpan(offset, length)).Images[0].Decode();
    }

    private static IReadOnlyList<ModelAnimation> ReadAnimations(byte[] content, int offset, int boneCount)
    {
        using BinaryReader reader = Reader(content);
        reader.BaseStream.Position = offset;
        int count = Count(reader.ReadUInt16(), 1000, "field animations");
        List<ModelAnimation> animations = [];
        for (int animation = 0; animation < count; animation++)
        {
            int frameCount = Count(reader.ReadUInt16(), 10000, "animation frames");
            int animationBones = reader.ReadUInt16();
            if (animationBones != boneCount || frameCount == 0)
            {
                throw new InvalidDataException($"Animation {animation} has {animationBones} bones for a {boneCount}-bone model.");
            }
            AnimationFrame[] frames = new AnimationFrame[frameCount];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                Vector3 translation = new(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                Vector3[] rotations = new Vector3[boneCount];
                for (int bone = 0; bone < boneCount; bone++)
                {
                    int x = reader.ReadByte();
                    int y = reader.ReadByte();
                    int z = reader.ReadByte();
                    int high = reader.ReadByte();
                    float scale = MathF.Tau / 4096;
                    rotations[bone] = new Vector3(((x << 2) | ((high & 3) << 10)) * scale, ((y << 2) | (((high >> 2) & 3) << 10)) * scale, ((z << 2) | (((high >> 4) & 3) << 10)) * scale);
                }
                frames[frame] = new AnimationFrame(translation, rotations);
            }
            animations.Add(new ModelAnimation(frames));
        }
        return animations;
    }

    private static BinaryReader Reader(byte[] content) => new(new MemoryStream(content, false));

    private static int Count(uint count, int maximum, string label)
    {
        if (count > maximum)
        {
            throw new InvalidDataException($"Invalid {label} count {count}.");
        }
        return checked((int)count);
    }
}
