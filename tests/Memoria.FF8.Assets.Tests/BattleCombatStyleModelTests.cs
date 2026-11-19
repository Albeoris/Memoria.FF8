using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleCombatStyleModelTests
{
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
                Assert.Equal(22, pairs.Count);
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
        }
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = weapon });
        resource.Validate();
        Assert.Equal(source, resource.CombatStyleModelId);
        Assert.All(resource.ModelLoadSources, dat => Assert.Equal(recipient, dat.ModelId));
        Assert.Equal(compiled.NativePoseSource, resource.PoseSource);
        NativeBattlePoseSource pose = resource.PoseSource!;
        for (int animation = 0; animation < resource.AnimationFrames.Length; animation++)
        {
            NativeBattleDecodedPose first = pose.Sample(checked((byte)animation), 1, 0);
            Assert.Equal(compiled.Bones.Count, NativeBattlePoseMatrices.StageBoneMatrices(pose, first).Length);
        }
    }
}
