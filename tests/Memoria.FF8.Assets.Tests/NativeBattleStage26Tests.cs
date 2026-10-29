using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStage26Tests
{
    [Fact]
    public void Original_single_group_keeps_bone2_and_current_pose_after_JSON_next_stage_step()
    {
        using BattleAssets assets = new(GameDirectory());
        BattleStage stage = assets.LoadStage(26);
        NativeBattleStageSource source = Assert.IsType<NativeBattleStageSource>(stage.NativeSource);
        NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(source.Groups[2]);
        Assert.Equal(3, pose.BoneCount);
        BattleMesh mesh = stage.Meshes.Where(item => item.NativeStageGroup == 2).ElementAt(1);
        Assert.Equal(48, mesh.Vertices.Count);
        Assert.All(mesh.Vertices, vertex => Assert.Equal(2, vertex.Bone));
        Assert.Equal(new short[] { 2 }, mesh.NativeBoneGroups!.Value.ToArray());
        NativeBattleRawVertexGroup packed = Assert.Single(mesh.NativePackedVertexGroups!.Value);
        Assert.Equal((ushort)2, packed.Bone);
        Assert.Equal((short)48, packed.Count);
        NativeBattleStageState state = NativeBattleStageState.FromColdProcess().Initialize500EA0(source);
        Assert.Equal(pose.BoneCount, state.Groups[2].Matrices.Length);
        Assert.All(stage.Meshes, item => Assert.All(item.Vertices, vertex => Assert.InRange(vertex.Bone, 0, source.Groups[item.NativeStageGroup!.Value]!.BoneCount - 1)));
        string json = NativeBattleSnapshotJson.Serialize(state);
        NativeBattleStageState restored = NativeBattleSnapshotJson.Deserialize<NativeBattleStageState>(json)!;
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        state = state.Step500FD0(camera);
        restored = restored.Step500FD0(camera);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(state), NativeBattleSnapshotJson.Serialize(restored));
        Assert.Equal(pose.BoneCount, state.Groups[2].Matrices.Length);
        state.Validate();
    }

    [Theory]
    [InlineData(0x00010001u, null)]
    [InlineData(0x00030001u, typeof(InvalidDataException))]
    public void Single_group_bone1_remains_valid_but_absent_bone_is_rejected(uint header, Type? error)
    {
        using GameAssets assets = new(GameDirectory());
        byte[] bytes = assets.ReadResource(new ResourceKey("battle/a0stg026.x"));
        int root = FindRoot(bytes);
        int group = root + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(root + 12));
        int geometry = group + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(group + 8));
        int secondObject = geometry + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(geometry + 8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(secondObject), header);
        if (error is not null)
        {
            Exception exception = Assert.ThrowsAny<Exception>(() => ReadStage(bytes));
            Assert.Equal(error, exception.GetType());
            return;
        }
        BattleStage stage = ReadStage(bytes);
        BattleMesh mesh = stage.Meshes.Where(item => item.NativeStageGroup == 2).ElementAt(1);
        Assert.All(mesh.Vertices, vertex => Assert.Equal(1, vertex.Bone));
        Assert.Equal(new short[] { 1 }, mesh.NativeBoneGroups!.Value.ToArray());
    }

    [Fact]
    public void Projection_selects_bone2_matrix_instead_of_bone1()
    {
        NativeBattleActionMatrix bone1 = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix bone2 = bone1 with { X = 100 };
        NativeBattleRawMesh mesh = new([new(new(0, 0, 1000), 2)], []);
        IReadOnlyList<NativeBattleProjectedVertex>? scratch = null;
        NativeBattleCameraProjection projection = new(null, 512, 160, 108);
        NativeBattleRawGeometryPackets.Emit5099D0([mesh], new[] { bone1, bone1, bone2 }, projection, 0, uint.MaxValue, 0, 17, 4, null, publishProjectedScratch: projected => scratch = projected);
        NativeBattleProjectedVertex vertex = Assert.Single(scratch!);
        Assert.Equal(1690u | (864u << 16), vertex.Screen);
        Assert.Equal((ushort)1000, vertex.Depth);
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryPackets.Emit5099D0([mesh], new[] { bone1, bone1 }, projection, 0, uint.MaxValue, 0, 17, 4, null));
    }

    private static int FindRoot(byte[] bytes)
    {
        for (int offset = 0; offset <= bytes.Length - 32; offset += 4)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)) != 6)
            {
                continue;
            }
            int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 28));
            int texture = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 24));
            if (end == bytes.Length - offset && texture >= 32 && texture <= end - 4 && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + texture)) == 16)
            {
                return offset;
            }
        }
        throw new InvalidDataException("Selected stage lacks its six-section directory.");
    }

    private static BattleStage ReadStage(byte[] bytes)
    {
        Type reader = typeof(BattleAssets).Assembly.GetType("Memoria.FF8.Assets.BattleResourceReader", throwOnError: true)!;
        MethodInfo method = reader.GetMethod("ReadStage", BindingFlags.Public | BindingFlags.Static)!;
        try
        {
            return (BattleStage)method.Invoke(null, [26, bytes])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static string GameDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for the stage geometry contract.");
        return directory!;
    }
}
