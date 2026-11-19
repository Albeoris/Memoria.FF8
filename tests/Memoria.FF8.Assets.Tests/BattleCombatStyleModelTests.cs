using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleCombatStyleModelTests
{
    public static IEnumerable<object[]> TwoHandCases => Enumerable.Range(0, 11).SelectMany(recipient => new[] { 2, 8 }.Select(source => new object[] { recipient, source }));

    [Theory]
    [MemberData(nameof(TwoHandCases))]
    public void Installed_firearm_contact_preserves_source_grip_gap_for_every_frame(int recipient, int source)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel donor = assets.LoadPlayer(source);
        BattleModel original = assets.LoadPlayer(recipient);
        BattleModel compiled = assets.LoadPlayerForStyle(recipient, 0, source);
        IReadOnlyList<NativeBattleSemanticBonePair> pairs = BattleHumanoidRigProfiles.SemanticPairs(source, donor, recipient, original);
        NativeBattleSemanticBonePair first = pairs.Single(pair => pair.Joint == "hand-a");
        NativeBattleSemanticBonePair second = pairs.Single(pair => pair.Joint == "hand-b");
        NativeBattlePoseSource donorPose = donor.NativePoseSource!;
        NativeBattlePoseSource targetPose = compiled.NativePoseSource!;
        var sourceFrames = NativeBattlePoseDecoder.DecodeFrames(donorPose.Animations.AsSpan(), donor.Bones.Count);
        var targetFrames = NativeBattlePoseDecoder.DecodeFrames(targetPose.Animations.AsSpan(), compiled.Bones.Count);
        Assert.Equal(original.Bones, compiled.Bones);
        for (int animation = 0; animation < sourceFrames.Length; animation++)
        {
            Assert.Equal(sourceFrames[animation].Length, targetFrames[animation].Length);
            for (int frame = 0; frame < sourceFrames[animation].Length; frame++)
            {
                var donorMatrices = NativeBattlePoseMatrices.StageBoneMatrices(donorPose, sourceFrames[animation][frame]);
                var targetMatrices = NativeBattlePoseMatrices.StageBoneMatrices(targetPose, targetFrames[animation][frame]);
                System.Numerics.Vector3 Point(NativeBattleActionMatrix matrix) => new(matrix.X, matrix.Y, matrix.Z);
                System.Numerics.Vector3 sourceGap = Point(donorMatrices[second.Source]) - Point(donorMatrices[first.Source]);
                System.Numerics.Vector3 targetGap = Point(targetMatrices[second.Recipient]) - Point(targetMatrices[first.Recipient]);
                float error = System.Numerics.Vector3.Distance(sourceGap, targetGap);
                Assert.True(error <= 4, $"recipient {recipient}, source {source}, animation {animation}, frame {frame + 1}: native hand gap error {error:R}");
            }
        }
    }

    [Fact]
    public void Installed_standard_semantic_chains_validate_for_every_source_and_recipient()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel[] models = Enumerable.Range(0, 11).Select(character => assets.LoadPlayer(character)).ToArray();
        for (int source = 0; source < models.Length; source++)
        {
            for (int recipient = 0; recipient < models.Length; recipient++)
            {
                IReadOnlyList<NativeBattleSemanticBonePair> pairs = BattleHumanoidRigProfiles.SemanticPairs(source, models[source], recipient, models[recipient]);
                Assert.Equal(23, pairs.Count);
                Assert.Equal(pairs.Count, pairs.Select(pair => pair.Source).Distinct().Count());
                Assert.Equal(pairs.Count, pairs.Select(pair => pair.Recipient).Distinct().Count());
            }
        }
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 1)]
    [InlineData(10, 9)]
    [InlineData(1, 8)]
    [InlineData(7, 8)]
    public void Installed_composition_preserves_recipient_rig_and_native_resource_layout(int recipient, int source)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel original = assets.LoadPlayer(recipient);
        BattleModel donor = assets.LoadPlayer(source);
        int weapon = assets.WeaponDefinitions.First(definition => definition.CharacterId == source).Id;
        BattleModel compiled = assets.LoadPlayerForWeapon(recipient, 0, weapon);
        Assert.Equal(original.Name, compiled.Name);
        Assert.Equal(original.Bones, compiled.Bones);
        Assert.Equal(original.Meshes[0].Vertices.Count, compiled.Meshes[0].Vertices.Count);
        Assert.Equal(original.NativePoseSource!.Skeleton, compiled.NativePoseSource!.Skeleton);
        Assert.Equal(donor.Animations.Select(animation => animation.Frames.Count), compiled.Animations.Select(animation => animation.Frames.Count));
        Assert.Same(compiled, assets.LoadPlayerForWeapon(recipient, 0, weapon));
        BattleModel equipment = assets.ResolvePlayerRenderWeapon(recipient, weapon, compiled)!;
        if (source is 1 or 9)
        {
            Assert.True(equipment.NativeWeaponUsesBodySkeleton);
            Assert.All(equipment.Meshes.SelectMany(mesh => mesh.Vertices), vertex => Assert.InRange(vertex.Bone, 0, compiled.Bones.Count - 1));
            Assert.Equal(compiled.Bones, equipment.Bones);
            Assert.Empty(compiled.Meshes[1].Vertices);
            BattleModel originalEquipment = assets.ResolvePlayerRenderWeapon(source, weapon, donor)!;
            NativeBattleVector[] sourceVertices = originalEquipment.Meshes.SelectMany(mesh => mesh.Vertices).Select(vertex => vertex.NativePosition!.Value).ToArray();
            NativeBattleVector[] recipientVertices = equipment.Meshes.SelectMany(mesh => mesh.Vertices).Select(vertex => vertex.NativePosition!.Value).ToArray();
            Assert.Equal(sourceVertices.Length, recipientVertices.Length);
            int sourceScale = donor.NativePoseSource!.ReadModelScale5027D0();
            int targetScale = compiled.NativePoseSource.ReadModelScale5027D0();
            for (int vertex = 0; vertex < sourceVertices.Length; vertex++)
            {
                Assert.InRange(Math.Abs(sourceVertices[vertex].X * sourceScale - recipientVertices[vertex].X * targetScale), 0, targetScale / 2 + 1);
                Assert.InRange(Math.Abs(sourceVertices[vertex].Y * sourceScale - recipientVertices[vertex].Y * targetScale), 0, targetScale / 2 + 1);
                Assert.InRange(Math.Abs(sourceVertices[vertex].Z * sourceScale - recipientVertices[vertex].Z * targetScale), 0, targetScale / 2 + 1);
            }
        }
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = weapon });
        resource.Validate();
        Assert.Equal(source, resource.CombatStyleModelId);
        Assert.All(resource.ModelLoadSources, dat => Assert.Equal(recipient, dat.ModelId));
        Assert.Equal(compiled.NativePoseSource, resource.PoseSource);
        NativeBattleModelDatSource bodyDat = resource.ModelLoadSources.Single(dat => !dat.Linked);
        Assert.Equal((resource.Geometry ?? []).Sum(mesh => mesh.Vertices.Length), NativeVertexCount(bodyDat));
        NativeBattleModelDatSource weaponDat = resource.ModelLoadSources.Single(dat => dat.Linked);
        Assert.Equal((resource.LinkedGeometry ?? []).Sum(mesh => mesh.Vertices.Length), NativeVertexCount(weaponDat));
        NativeBattlePoseSource pose = resource.PoseSource!;
        for (int animation = 0; animation < resource.AnimationFrames.Length; animation++)
        {
            NativeBattleDecodedPose first = pose.Sample(checked((byte)animation), 1, 0);
            Assert.Equal(compiled.Bones.Count, NativeBattlePoseMatrices.StageBoneMatrices(pose, first).Length);
        }
    }

    private static int NativeVertexCount(NativeBattleModelDatSource source)
    {
        ReadOnlySpan<byte> bytes = source.Bytes.AsSpan();
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int geometry = source.Section(source.Linked && sections == 5 ? 0 : 1);
        int objects = BinaryPrimitives.ReadInt32LittleEndian(bytes[geometry..]);
        int vertices = 0;
        for (int index = 0; index < objects; index++)
        {
            int cursor = geometry + BinaryPrimitives.ReadInt32LittleEndian(bytes[(geometry + 4 + index * 4)..]);
            int groups = BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]);
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(cursor + 2)..]);
                vertices += count;
                cursor = checked(cursor + 4 + count * 6);
            }
        }
        return vertices;
    }
}
