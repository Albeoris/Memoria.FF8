using System.Numerics;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Selected field ONE/MCH structures. Archive, image and text formats are handled by Albeoris.</summary>
internal static class FieldModelReader
{
    internal static IReadOnlyList<NativeFieldOneModelEntry> ReadDirectory(byte[] one, long? containerLength = null)
    {
        using BinaryReader reader = Reader(one);
        int count = Count(reader.ReadUInt32(), 100, "ONE models");
        List<NativeFieldOneModelEntry> headers = [];
        for (int i = 0; i < count; i++)
        {
            int offset = checked((int)reader.ReadUInt32() + 4);
            int size = checked((int)reader.ReadUInt32());
            uint sourceWord8 = reader.ReadUInt32();
            uint flag = reader.ReadUInt32();
            List<int> textures = [];
            List<uint> rawTextureWords = [];
            int modelOffset = -1;
            if ((flag >> 28) == 0xD)
            {
                reader.ReadUInt32();
            }
            else if ((flag >> 28) == 0xA)
            {
                reader.ReadUInt32();
                modelOffset = checked((int)reader.ReadUInt32());
            }
            else
            {
                textures.Add(checked((int)(flag & 0xFFFFFF)));
                rawTextureWords.Add(flag);
                uint value;
                while ((value = reader.ReadUInt32()) != uint.MaxValue)
                {
                    textures.Add(checked((int)(value & 0xFFFFFF)));
                    rawTextureWords.Add(value);
                    if (textures.Count > 100)
                    {
                        throw new InvalidDataException("ONE texture pointer table is unbounded.");
                    }
                }
                modelOffset = checked((int)reader.ReadUInt32());
            }
            byte[] rawName = reader.ReadBytes(8);
            string name = System.Text.Encoding.ASCII.GetString(rawName).TrimEnd('\0');
            reader.ReadUInt32();
            if (offset < 0 || size > (containerLength ?? one.Length) - offset)
            {
                throw new InvalidDataException($"ONE model {name} lies outside its container.");
            }
            NativeFieldOneModelEntry entry = new(offset, size, flag, modelOffset, textures.ToImmutableArray(), name) { RawTextureWords = rawTextureWords.ToImmutableArray(), SourceWord8 = sourceWord8, RawName = rawName.ToImmutableArray() };
            headers.Add(entry);
        }
        return headers;
    }

    public static IReadOnlyList<FieldModel> Read(byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null)
    {
        IReadOnlyList<NativeFieldOneModelEntry> headers = ReadDirectory(one);
        foreach (NativeFieldOneModelEntry header in headers)
        {
            retainEntry?.Invoke(header);
        }
        return ReadSelected(one, mainCharacter, headers, Enumerable.Range(0, headers.Count));
    }

    internal static FieldModel ReadSingle(byte[] one, Func<string, byte[]> mainCharacter, int index)
    {
        IReadOnlyList<NativeFieldOneModelEntry> headers = ReadDirectory(one);
        return ReadSelected(one, mainCharacter, headers, [index])[0];
    }

    private static IReadOnlyList<FieldModel> ReadSelected(byte[] one, Func<string, byte[]> mainCharacter, IReadOnlyList<NativeFieldOneModelEntry> headers, IEnumerable<int> selected)
    {
        FieldModel?[] models = new FieldModel?[headers.Count];
        HashSet<int> resolving = [];
        FieldModel Resolve(int index)
        {
            if ((uint)index >= headers.Count || !resolving.Add(index))
            {
                throw new InvalidDataException("ONE texture alias references an absent model or a cycle.");
            }
            if (models[index] is FieldModel cached)
            {
                resolving.Remove(index);
                return cached;
            }
            NativeFieldOneModelEntry header = headers[index];
            FieldModel result;
            if ((header.Flag >> 28) == 0xD)
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
                result = ReadModel(header.Name, main, modelOffset, textures, 0, one, header.Offset);
            }
            else if ((header.Flag >> 28) == 0xA)
            {
                // Native 0x532A40 keeps this model's geometry/animation and aliases texture slot0.
                FieldModel source = Resolve((byte)(header.Flag >> 20));
                if (source.Textures.Count == 0)
                {
                    throw new InvalidDataException($"ONE model {header.Name} aliases an absent texture slot0.");
                }
                result = ReadModel(header.Name, one, header.Offset + header.ModelOffset, [], header.Offset, null, 0, decodedTextures: [source.Textures[0]], nativeEyeTexture: source.NativeEyes?.Texture) with { TextureRegions = source.TextureRegions.Take(1).ToArray() };
            }
            else if (header.ModelOffset >= 0)
            {
                result = ReadModel(header.Name, one, header.Offset + header.ModelOffset, header.Textures, header.Offset, null, 0);
            }
            else
            {
                throw new InvalidDataException($"ONE model {header.Name} has no supported geometry binding.");
            }
            models[index] = result;
            resolving.Remove(index);
            return result;
        }
        return selected.Select(Resolve).ToArray();
    }

    internal static FieldModel ReadModel(string name, byte[] content, int modelOffset, IEnumerable<int> textureOffsets, int textureBase, byte[]? animationContent, int animationOffset, IReadOnlyList<ModelAnimation>? animations = null, IReadOnlyList<PixelImage>? decodedTextures = null, NativeEyeTexture? nativeEyeTexture = null)
    {
        using BinaryReader reader = Reader(content);
        reader.BaseStream.Position = modelOffset;
        int boneCount = Count(reader.ReadUInt32(), 100, "MCH bones");
        int vertexCount = Count(reader.ReadUInt32(), 10000, "MCH vertices");
        int eyeDataCount = Count(reader.ReadUInt32(), 10000, "MCH native eye data");
        int faceCount = Count(reader.ReadUInt32(), 20000, "MCH faces");
        int partCount = Count(reader.ReadUInt32(), 1000, "MCH parts");
        int skinCount = Count(reader.ReadUInt32(), 1000, "MCH skin groups");
        reader.ReadUInt32();
        reader.ReadUInt32();
        int boneOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int vertexOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int eyeOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int faceOffset = checked(modelOffset + (int)reader.ReadUInt32());
        int partOffset = checked(modelOffset + (int)reader.ReadUInt32());
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
        NativeModelPartDefinition[] parts = new NativeModelPartDefinition[partCount];
        for (int index = 0; index < parts.Length; index++)
        {
            reader.BaseStream.Position = checked(partOffset + index * 32);
            short[] words = Enumerable.Range(0, 16).Select(_ => reader.ReadInt16()).ToArray();
            parts[index] = new NativeModelPartDefinition(words);
            if (words[0] < 0 || words[1] < 0 || words[0] + words[1] > skinCount)
            {
                throw new InvalidDataException($"{name}: native part{index} skin bindings exceed their original table.");
            }
        }
        NativeModelSkinBinding[] skinBindings = new NativeModelSkinBinding[skinCount];
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
            ushort skinFlags = reader.ReadUInt16();
            skinBindings[i] = new NativeModelSkinBinding(checked((short)start), checked((short)length), checked((short)bone), skinFlags);
            if (start < 0 || length < 0 || start + length > vertices.Length || bone < 0 || bone >= bones.Length)
            {
                throw new InvalidDataException($"{name}: invalid skin group {i}.");
            }
            for (int vertex = start; vertex < start + length; vertex++)
            {
                vertices[vertex] = vertices[vertex] with { Bone = bone, SkinBinding = i };
            }
        }
        ModelFace[] faces = new ModelFace[faceCount];
        for (int i = 0; i < faces.Length; i++)
        {
            int position = faceOffset + i * 64;
            reader.BaseStream.Position = position;
            uint type = reader.ReadUInt32();
            reader.BaseStream.Position = position + 8;
            ushort flags = reader.ReadUInt16();
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
            // Native 531310 preserves MCH face+8 bit2; 531980 sets the packet's semitransparency bit from it.
            faces[i] = new ModelFace(indices, uv, texture, quad) { SemiTransparent = (flags & 4) != 0 };
        }
        TimImage[]? nativeTextures = decodedTextures is null ? textureOffsets.Select(offset => ReadTim(content, textureBase + offset)).ToArray() : null;
        IReadOnlyList<PixelImage> textures = decodedTextures ?? nativeTextures!.Select(texture => texture.Decode()).ToArray();
        IReadOnlyList<TextureSourceRegion> textureRegions = nativeTextures?.Select(texture => new TextureSourceRegion(texture.X, texture.Y, texture.PixelMode switch { TimPixelMode.Indexed4 => 4, TimPixelMode.Indexed8 => 8, TimPixelMode.Direct16 => 16, _ => 24 }, texture.PixelWidth, texture.Height)).ToArray() ?? [];
        if (faces.Any(face => face.Texture >= textures.Count))
        {
            throw new InvalidDataException($"{name}: face references an absent texture.");
        }
        animations ??= ReadAnimations(animationContent ?? content, animationContent is null ? embeddedAnimation : animationOffset, boneCount);
        return new FieldModel(name, bones, vertices, faces, textures, animations) { TextureRegions = textureRegions, NativeEyes = new NativeFieldEyeDefinition(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), modelOffset, eyeOffset, content.AsSpan(eyeOffset, eyeDataCount).ToArray(), nativeEyeTexture ?? ReadEyeTexture(content, textureOffsets.Select(offset => textureBase + offset).FirstOrDefault(-1))), ControlBones = controls, NativeParts = new NativeModelPartsDefinition(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), modelOffset, parts, skinBindings) };
    }

    private static NativeEyeTexture? ReadEyeTexture(byte[] content, int offset)
    {
        if (offset < 0)
        {
            return null;
        }
        using BinaryReader reader = Reader(content);
        reader.BaseStream.Position = offset;
        if (reader.ReadUInt32() != 0x10)
        {
            throw new InvalidDataException("Native eye texture is not a TIM resource.");
        }
        uint flags = reader.ReadUInt32();
        if ((flags & 8) != 0)
        {
            uint size = reader.ReadUInt32();
            reader.BaseStream.Position += size - 4;
        }
        uint imageSize = reader.ReadUInt32();
        reader.ReadUInt16();
        reader.ReadUInt16();
        int width = reader.ReadUInt16();
        int height = reader.ReadUInt16();
        byte[] words = reader.ReadBytes(checked(width * height * 2));
        if (imageSize != words.Length + 12 || words.Length != width * height * 2)
        {
            throw new InvalidDataException("Native eye texture image extent is invalid.");
        }
        return new NativeEyeTexture((int)(flags & 3), width, height, words);
    }

    private static TimImage ReadTim(byte[] content, int offset)
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
        return TimFile.Read(content.AsSpan(offset, length)).Images[0];
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
