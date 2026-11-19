using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleCombatStyleModelTests
{
    public static IEnumerable<object[]> TwoHandCases => Enumerable.Range(0, 11).SelectMany(recipient => new[] { 2, 8 }.Select(source => new object[] { recipient, source }));

    [Theory]
    [MemberData(nameof(TwoHandCases))]
    public void Installed_firearm_contact_preserves_admitted_grip_and_free_source_gestures(int recipient, int source)
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
        int nativeWeapon = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == source).Id;
        BattleModel sourceWeapon = assets.ResolvePlayerRenderWeapon(source, nativeWeapon, donor)!;
        var contacts = BattleWeaponContactFrames.Read(source, donor, sourceWeapon, donorPose);
        Assert.Contains(contacts.SelectMany(frames => frames), value => value);
        Assert.Contains(contacts.SelectMany(frames => frames), value => !value);
        NativeBattlePoseRetargeter compiler = BattleHumanoidRigProfiles.CreateRetargeter(source, donor, recipient, original);
        var sourceFrames = NativeBattlePoseDecoder.DecodeFrames(donorPose.Animations.AsSpan(), donor.Bones.Count);
        var targetFrames = NativeBattlePoseDecoder.DecodeFrames(targetPose.Animations.AsSpan(), compiled.Bones.Count);
        Assert.Equal(original.Bones, compiled.Bones);
        for (int animation = 0; animation < sourceFrames.Length; animation++)
        {
            Assert.Equal(sourceFrames[animation].Length, targetFrames[animation].Length);
            for (int frame = 0; frame < sourceFrames[animation].Length; frame++)
            {
                if (!contacts[animation][frame])
                {
                    NativeBattleDecodedPose free = source == recipient ? sourceFrames[animation][frame] : compiler.Transfer(sourceFrames[animation][frame], false);
                    NativeBattleVector Canonical(NativeBattleVector angles) => new((short)(angles.X & 4095), (short)(angles.Y & 4095), (short)(angles.Z & 4095));
                    Assert.Equal(free.Translation, targetFrames[animation][frame].Translation);
                    Assert.Equal(free.Bones.Select(bone => Canonical(bone.Rotation)), targetFrames[animation][frame].Bones.Select(bone => Canonical(bone.Rotation)));
                    continue;
                }
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
        Assert.Equal((byte?)source, resource.CombatStyleModelId);
        Assert.All(resource.ModelLoadSources!.Value, dat => Assert.Equal((byte)recipient, dat.ModelId));
        Assert.Equal(compiled.NativePoseSource, resource.PoseSource);
        NativeBattleModelDatSource bodyDat = resource.ModelLoadSources!.Value.Single(dat => !dat.Linked);
        Assert.Equal((resource.Geometry ?? []).Sum(mesh => mesh.Vertices.Length), NativeVertexCount(bodyDat));
        NativeBattleModelDatSource weaponDat = resource.ModelLoadSources!.Value.Single(dat => dat.Linked);
        Assert.Equal((resource.LinkedGeometry ?? []).Sum(mesh => mesh.Vertices.Length), NativeVertexCount(weaponDat));
        NativeBattlePoseSource pose = resource.PoseSource!;
        for (int animation = 0; animation < resource.AnimationFrames.Length; animation++)
        {
            NativeBattleDecodedPose first = pose.Sample(checked((byte)animation), 1, 0);
            Assert.Equal(compiled.Bones.Count, NativeBattlePoseMatrices.StageBoneMatrices(pose, first).Length);
        }
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(9, 8)]
    [InlineData(1, 1)]
    [InlineData(9, 9)]
    [InlineData(4, 1)]
    [InlineData(10, 9)]
    public void Installed_freehands_retains_recipient_surfaces_and_separate_intrinsic_hand_material(int recipient, int source)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int weapon = assets.WeaponDefinitions.First(definition => definition.CharacterId == source).Id;
        BattleCombatantDefinition actor = new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = weapon };
        NativeBattleGraphicsResource equipped = assets.ReadNativeGraphicsResource(actor);
        NativeBattleGraphicsResource free = assets.ReadNativeLimitPresentationResource(actor, BattleLimitFamily.Duel, equipped);
        BattleModel original = assets.LoadPlayer(recipient);
        var originalGeometry = original.NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(original.Meshes);
        Assert.Equal(NativeBattleEquipmentPresentationRole.FreeHands, free.EquipmentPresentationRole);
        Assert.Empty(free.LinkedGeometry!.Value);
        Assert.Equal(originalGeometry[0].Vertices.ToArray(), free.Geometry!.Value[0].Vertices.ToArray());
        Assert.Equal(originalGeometry[0].Polygons.ToArray(), free.Geometry.Value[0].Polygons.ToArray());
        if (recipient is 1 or 9)
        {
            Assert.NotNull(free.IntrinsicHands);
            Assert.Contains(free.IntrinsicHands.Geometry, mesh => !mesh.Polygons.IsEmpty);
            Assert.NotEqual(free.BodyTexturePack!.Data, free.IntrinsicHands.TexturePack.Data);
            Assert.All(free.IntrinsicHands.Geometry.SelectMany(mesh => mesh.Vertices), vertex => Assert.InRange(vertex.Bone, 0, original.Bones.Count - 1));
        }
        else
        {
            Assert.Null(free.IntrinsicHands);
        }
        if (source == 1 && recipient != 1)
        {
            Assert.True(equipped.Geometry!.Value[0].Polygons.Length < originalGeometry[0].Polygons.Length);
        }
        if (source == 9 && recipient != 9)
        {
            BattleModel donor = assets.LoadPlayer(9);
            BattleModel donorWeapon = assets.ResolvePlayerRenderWeapon(9, weapon, donor)!;
            Assert.True(equipped.LinkedGeometry!.Value.Sum(mesh => mesh.Polygons.Length) < donorWeapon.Meshes.Sum(mesh => mesh.Faces.Count));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Installed_gloves_preserve_recipient_wrist_bridge_polygons(int recipient)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel original = assets.LoadPlayer(recipient);
        BattleModel equipped = assets.LoadPlayerForStyle(recipient, 0, 1);
        HashSet<int> hands = BattleHumanoidRigProfiles.SemanticPairs(recipient, original, recipient, original).Where(pair => pair.Joint is "hand-a" or "hand-b").Select(pair => pair.Recipient).ToHashSet();
        BattleMesh body = original.Meshes[0];
        BattleFace[] bridges = body.Faces.Where(face => face.Vertices.Any(index => hands.Contains(body.Vertices[index].Bone)) && face.Vertices.Any(index => !hands.Contains(body.Vertices[index].Bone))).ToArray();
        Assert.NotEmpty(bridges);
        foreach (BattleFace face in bridges)
        {
            Assert.Contains(face, equipped.Meshes[0].Faces);
        }
        Assert.DoesNotContain(equipped.Meshes[0].Faces, face => face.Vertices.All(index => hands.Contains(body.Vertices[index].Bone)));
        Assert.Equal(body.Vertices, equipped.Meshes[0].Vertices);
        Assert.Equal(equipped.Meshes[0].Faces.Count, equipped.NativeRenderGeometry!.Value[0].Polygons.Length);
        BattleCombatantDefinition actor = new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id };
        NativeBattleGraphicsResource free = assets.ReadNativeLimitPresentationResource(actor, BattleLimitFamily.Duel, assets.ReadNativeGraphicsResource(actor));
        Assert.Equal(body.Faces.Count, free.Geometry!.Value[0].Polygons.Length);
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
