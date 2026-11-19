using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattleGraphicsResource BindIntrinsicHands(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor, bool freeHands = false)
    {
        if (actor.ModelId is not (1 or 9))
        {
            return resource with { IntrinsicHands = null };
        }
        int style = resource.CombatStyleModelId ?? actor.ModelId;
        if (!freeHands && (style == 1 || actor.ModelId == 9 && style == 9))
        {
            return resource with { IntrinsicHands = null };
        }
        BattleModel body = LoadPlayer(actor.ModelId, actor.Costume);
        int nativeWeapon = WeaponDefinitions.First(weapon => weapon.CharacterId == actor.ModelId).Id;
        BattleModel ownHands = LoadWeapon(ResolvePlayerWeaponName(actor.ModelId, nativeWeapon), body);
        IReadOnlyList<BattleMesh> meshes = actor.ModelId == 9 ? ownHands.Meshes.Select(mesh => SelectKirosHandComponents(mesh, true)).ToArray() : ownHands.Meshes;
        return resource with { IntrinsicHands = new(NativeBattleGeometryMetadata.FromMeshes(meshes), ownHands.NativeWeaponTexturePack ?? throw new InvalidDataException("The recipient's intrinsic hands require their own native texture pack.")) };
    }
    private static BattleMesh SelectKirosHandComponents(BattleMesh mesh, bool hands)
    {
        int[] parents = Enumerable.Range(0, mesh.Vertices.Count).ToArray();
        int Root(int vertex)
        {
            while (parents[vertex] != vertex)
            {
                vertex = parents[vertex];
            }
            return vertex;
        }
        foreach (BattleFace face in mesh.Faces)
        {
            foreach (int vertex in face.Vertices.Skip(1))
            {
                parents[Root(vertex)] = Root(face.Vertices[0]);
            }
        }
        HashSet<int> wristComponents = [];
        foreach (IGrouping<int, int> group in Enumerable.Range(0, mesh.Vertices.Count).GroupBy(index => mesh.Vertices[index].Bone))
        {
            int[] crossing = group.Where(index => (mesh.Vertices[index].NativePosition ?? throw new InvalidDataException("The native hand profile requires signed local vertices.")).Z >= 0).Select(Root).Distinct().ToArray();
            if (crossing.Length != 1)
            {
                throw new NotSupportedException("The Kiros hand profile requires one connected component crossing each native wrist plane.");
            }
            wristComponents.Add(crossing[0]);
        }
        return mesh with { Faces = mesh.Faces.Where(face => wristComponents.Contains(Root(face.Vertices[0])) == hands).ToArray() };
    }

    private static BattleMesh MaskBodyHands(BattleMesh mesh, IReadOnlyCollection<int> hands)
    {
        return mesh with { Faces = mesh.Faces.Where(face => !face.Vertices.All(index => hands.Contains(mesh.Vertices[index].Bone))).ToArray() };
    }

    private static IReadOnlyList<BattleMesh> RefreshNativeVertexGroups(IReadOnlyList<BattleMesh> meshes, ImmutableArray<byte> geometry)
    {
        BattleMesh[] result = meshes.ToArray();
        for (int index = 0; index < result.Length; index++)
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan().Slice(4 + index * 4, 4));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan().Slice(cursor, 2));
            cursor += 2;
            ImmutableArray<NativeBattleRawVertexGroup>.Builder groups = ImmutableArray.CreateBuilder<NativeBattleRawVertexGroup>(count);
            for (int group = 0; group < count; group++)
            {
                ushort bone = BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan().Slice(cursor, 2));
                short vertices = BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan().Slice(cursor + 2, 2));
                groups.Add(new(bone, vertices, geometry.Slice(cursor + 4, checked(vertices * 6 + 6))));
                cursor += 4 + vertices * 6;
            }
            result[index] = result[index] with { NativePackedVertexGroups = groups.MoveToImmutable() };
        }
        return result;
    }

    private static ImmutableArray<byte> EncodeNativeBodyGeometry(IReadOnlyList<BattleMesh> meshes)
    {
        using MemoryStream output = new();
        using BinaryWriter writer = new(output);
        writer.Write(meshes.Count);
        foreach (BattleMesh mesh in meshes)
        {
            writer.Write(0);
        }
        for (int index = 0; index < meshes.Count; index++)
        {
            while ((output.Length & 3) != 0)
            {
                output.Position = output.Length;
                writer.Write((byte)0);
            }
            int pointer = checked((int)output.Length);
            output.Position = 4 + index * 4;
            writer.Write(pointer);
            output.Position = pointer;
            BattleMesh mesh = meshes[index];
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.NativePackedVertexGroups ?? throw new InvalidDataException("Native geometry encoding requires its decoded vertex groups.");
            writer.Write(checked((ushort)groups.Length));
            int vertexIndex = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                writer.Write(group.Bone);
                writer.Write(group.Count);
                for (int vertex = 0; vertex < group.Count; vertex++)
                {
                    BattleVertex item = mesh.Vertices[vertexIndex++];
                    NativeBattleVector position = item.NativePosition ?? throw new InvalidDataException("Native geometry encoding requires signed vertex positions.");
                    if (item.Bone != group.Bone)
                    {
                        throw new InvalidDataException("Native geometry encoding changed its rigid group order.");
                    }
                    writer.Write(checked((short)position.X));
                    writer.Write(checked((short)position.Y));
                    writer.Write(checked((short)position.Z));
                }
            }
            if (vertexIndex != mesh.Vertices.Count)
            {
                throw new InvalidDataException("Native geometry groups do not cover their actual vertices.");
            }
            while ((output.Position & 3) != 0)
            {
                writer.Write((byte)0);
            }
            writer.Write(checked((ushort)mesh.Faces.Count(face => face.Vertices.Length == 3)));
            writer.Write(checked((ushort)mesh.Faces.Count(face => face.Vertices.Length == 4)));
            writer.Write(0L);
            foreach (BattleFace face in mesh.Faces.OrderBy(face => face.Vertices.Length))
            {
                writer.Write(face.NativePolygon.AsSpan());
            }
        }
        writer.Write(meshes.Max(mesh => mesh.Vertices.Count));
        return output.ToArray().ToImmutableArray();
    }
}
