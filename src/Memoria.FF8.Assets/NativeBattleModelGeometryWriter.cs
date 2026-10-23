using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Encodes rigid model geometry into the native grouped vertex and FT3 representation.</summary>
public static class NativeBattleModelGeometryWriter
{
    public static byte[] Encode(EditableModel edited, EditableModel original)
    {
        float scale = original.NativeMetadata.GetProperty("Scale").GetSingle();
        JsonElement sourceMeshes = original.NativeMetadata.GetProperty("Meshes");
        Dictionary<(int Part, int Texture), JsonElement> materials = [];
        HashSet<(int Part, int Texture)> ambiguous = [];
        int sourcePart = 0;
        foreach (JsonElement sourceMesh in sourceMeshes.EnumerateArray())
        {
            foreach (JsonElement face in sourceMesh.GetProperty("Faces").EnumerateArray())
            {
                var key = (sourcePart, face.GetProperty("Texture").GetInt32());
                if (materials.TryGetValue(key, out JsonElement previous) && MaterialKey(previous) != MaterialKey(face))
                {
                    ambiguous.Add(key);
                }
                materials.TryAdd(key, face);
            }
            sourcePart++;
        }
        if (edited.Meshes.Any(mesh => mesh.Texture < 0 || mesh.Texture >= edited.Textures.Count))
        {
            throw new NotSupportedException("Native battle geometry requires an existing imported base-color image.");
        }
        foreach (var binding in edited.Meshes.GroupBy(mesh => mesh.NativeTexture))
        {
            var images = binding.Select(mesh => edited.Textures[mesh.Texture]).ToArray();
            if (images.Skip(1).Any(image => image.Width != images[0].Width || image.Height != images[0].Height || !image.Pixels.SequenceEqual(images[0].Pixels)))
            {
                throw new NotSupportedException("A native texture slot cannot bind conflicting imported images.");
            }
        }
        Matrix4x4[] globals = new Matrix4x4[edited.Bones.Count];
        for (int bone = 0; bone < globals.Length; bone++)
        {
            EditableBone value = edited.Bones[bone];
            Matrix4x4 local = Matrix4x4.CreateScale(value.Scale) * Matrix4x4.CreateFromQuaternion(value.Rotation) * Matrix4x4.CreateTranslation(value.Translation);
            globals[bone] = value.Parent < 0 ? local : local * globals[value.Parent];
        }
        int count = edited.Meshes.Select(mesh => mesh.NativePart + 1).DefaultIfEmpty(0).Max();
        if (edited.Meshes.Any(mesh => mesh.NativePart < 0) || count > sourceMeshes.GetArrayLength())
        {
            throw new NotSupportedException("Native battle geometry requires retained native part identities.");
        }
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(count);
        for (int part = 0; part < count; part++)
        {
            writer.Write(0);
        }
        for (int part = 0; part < count; part++)
        {
            long start = stream.Position;
            stream.Position = 4 + part * 4;
            writer.Write(checked((int)start));
            stream.Position = start;
            List<(Vector3 Position, int Bone)> vertices = [];
            List<(int[] Indices, Vector2[] Uv, ushort Page, ushort Clut, int Bias)> faces = [];
            foreach (EditableMesh mesh in edited.Meshes.Where(mesh => mesh.NativePart == part))
            {
                int texture = mesh.NativeTexture;
                if (!materials.TryGetValue((part, texture), out JsonElement template) || mesh.Texture < 0 || mesh.Texture >= edited.Textures.Count || texture >= original.Textures.Count)
                {
                    throw new NotSupportedException("Native battle geometry requires an existing native texture binding.");
                }
                EditableMesh? originalMesh = original.Meshes.FirstOrDefault(value => value.NativePart == part && value.NativeTexture == texture);
                if (originalMesh is null || mesh.Transparent != originalMesh.Transparent && !(mesh.Transparent && original.Textures[texture].Pixels.Any(pixel => pixel.A < 255)) || mesh.DoubleSided != originalMesh.DoubleSided)
                {
                    throw new NotSupportedException("Native battle geometry requires retained transparency and culling flags.");
                }
                if (((template.GetProperty("NativeMaterial").GetProperty("TexturePage").GetUInt16() >> 7) & 3) != 1)
                {
                    throw new NotSupportedException("Imported native battle geometry requires existing indexed eight-bit materials.");
                }
                if (mesh.NativeMaterial >= 0)
                {
                    JsonElement sourceFaces = sourceMeshes[part].GetProperty("Faces");
                    if (mesh.NativeMaterial >= sourceFaces.GetArrayLength() || sourceFaces[mesh.NativeMaterial].GetProperty("Texture").GetInt32() != texture)
                    {
                        throw new NotSupportedException("An imported native polygon binding is absent from its target part.");
                    }
                    template = sourceFaces[mesh.NativeMaterial];
                }
                else if (ambiguous.Contains((part, texture)))
                {
                    throw new NotSupportedException("Native material groups with differing polygon fields require retained material identities.");
                }
                Vector2 logical = ResourceTextureList.LogicalSize(original.Textures, texture);
                var image = edited.Textures[mesh.Texture];
                if (image.Width != logical.X || image.Height != logical.Y)
                {
                    throw new NotSupportedException("Imported native battle textures must retain their original dimensions.");
                }
                JsonElement material = template.GetProperty("NativeMaterial");
                ushort page = material.GetProperty("TexturePage").GetUInt16();
                ushort clut = material.GetProperty("Clut").GetUInt16();
                Vector2 origin = ReadUv(template.GetProperty("Uv")[0]) - ReadUv(material.GetProperty("Uv")[0]);
                byte[] polygon = template.GetProperty("NativePolygon").EnumerateArray().Select(value => value.GetByte()).ToArray();
                int bias = BinaryPrimitives.ReadUInt16LittleEndian(polygon) & 0xF000;
                int offset = vertices.Count;
                foreach (EditableVertex vertex in mesh.Vertices)
                {
                    int[] slots = Enumerable.Range(0, 4).Where(slot => Math.Abs(vertex.Weights[slot]) > 0.00001f).ToArray();
                    if (!mesh.Skinned || slots.Length != 1 || Math.Abs(vertex.Weights[slots[0]] - 1) > 0.00001f || Vector4.Distance(vertex.Color, Vector4.One) > 0.0001f)
                    {
                        throw new NotSupportedException("Native battle geometry supports rigid single-joint weights and unchanged vertex colors.");
                    }
                    int joint = checked((int)vertex.Joints[slots[0]]);
                    if ((uint)joint >= globals.Length || !Matrix4x4.Invert(globals[joint], out Matrix4x4 inverse))
                    {
                        throw new NotSupportedException("Native battle geometry requires an invertible retained joint bind.");
                    }
                    inverse = mesh.InverseBindMatrices?.GetValueOrDefault(joint, inverse) ?? inverse;
                    Vector3 local = Vector3.Transform(Vector3.Transform(vertex.Position, inverse), Matrix4x4.CreateRotationX(MathF.PI / 2)) / scale;
                    int nativeBone = edited.Bones[joint].NativeIndex;
                    if (nativeBone < 0)
                    {
                        throw new NotSupportedException("Native battle geometry references an unbound joint.");
                    }
                    _ = Word(-local.X * 2048);
                    _ = Word(-local.Y * 2048);
                    _ = Word(local.Z * 2048);
                    vertices.Add((local, nativeBone));
                }
                if (mesh.Indices.Count % 3 != 0)
                {
                    throw new NotSupportedException("Native battle geometry requires triangles.");
                }
                for (int index = 0; index < mesh.Indices.Count; index += 3)
                {
                    int[] triangle = mesh.Indices.Skip(index).Take(3).ToArray();
                    Vector2[] uv = triangle.Select(vertex => mesh.Vertices[vertex].Uv * logical - origin).ToArray();
                    if (uv.Any(value => !float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < -0.5f || value.Y < -0.5f || value.X > 255.49f || value.Y > 255.49f))
                    {
                        throw new NotSupportedException("Imported UVs leave the original native texture page.");
                    }
                    faces.Add((triangle.Select(vertex => vertex + offset).ToArray(), uv, page, clut, bias));
                }
            }
            if (vertices.Count > 4096 || faces.Count > 16384)
            {
                throw new NotSupportedException("Imported geometry exceeds the native object index limits.");
            }
            var groups = vertices.Select((vertex, index) => (vertex, index)).GroupBy(value => value.vertex.Bone).ToArray();
            writer.Write(checked((ushort)groups.Length));
            int[] remap = new int[vertices.Count];
            int output = 0;
            foreach (var group in groups)
            {
                writer.Write(checked((ushort)group.Key));
                writer.Write(checked((ushort)group.Count()));
                foreach (var value in group)
                {
                    remap[value.index] = output++;
                    writer.Write(Word(-value.vertex.Position.X * 2048));
                    writer.Write(Word(-value.vertex.Position.Y * 2048));
                    writer.Write(Word(value.vertex.Position.Z * 2048));
                }
            }
            while ((stream.Position & 3) != 0)
            {
                writer.Write((byte)0);
            }
            writer.Write(checked((ushort)faces.Count));
            writer.Write((ushort)0);
            writer.Write(0UL);
            foreach (var face in faces)
            {
                // FT3 stores its third corner first in the UV record and its index record is rotated.
                writer.Write(checked((ushort)(remap[face.Indices[1]] | face.Bias)));
                writer.Write(checked((ushort)remap[face.Indices[2]]));
                writer.Write(checked((ushort)remap[face.Indices[0]]));
                Uv(face.Uv[0]);
                Uv(face.Uv[1]);
                writer.Write(face.Clut);
                Uv(face.Uv[2]);
                writer.Write(face.Page);
            }
        }
        byte[] bytes = stream.ToArray();
        _ = NativeBattleRawGeometryReader.Read(bytes, original.Bones.Count(bone => bone.IsJoint));
        return bytes;

        void Uv(Vector2 uv)
        {
            writer.Write(checked((byte)MathF.Round(uv.X)));
            writer.Write(checked((byte)MathF.Round(uv.Y)));
        }
    }

    private static (ushort Page, ushort Clut, int Bias) MaterialKey(JsonElement face)
    {
        JsonElement material = face.GetProperty("NativeMaterial");
        byte[] polygon = face.GetProperty("NativePolygon").EnumerateArray().Select(value => value.GetByte()).ToArray();
        return (material.GetProperty("TexturePage").GetUInt16(), material.GetProperty("Clut").GetUInt16(), BinaryPrimitives.ReadUInt16LittleEndian(polygon) & 0xF000);
    }

    private static short Word(float value)
    {
        if (!float.IsFinite(value) || MathF.Round(value) < short.MinValue || MathF.Round(value) > short.MaxValue)
        {
            throw new NotSupportedException("Imported geometry exceeds the native signed WORD coordinate range.");
        }
        return checked((short)MathF.Round(value));
    }

    private static Vector2 ReadUv(JsonElement value) => new(value.GetProperty("X").GetSingle(), value.GetProperty("Y").GetSingle());
}
