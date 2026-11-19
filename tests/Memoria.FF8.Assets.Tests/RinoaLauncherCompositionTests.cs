using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class RinoaLauncherCompositionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(9)]
    public void Transferred_mount_preserves_nonzero_source_world_vectors_in_the_recipients_forearm_frame(int recipient)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleModel source = assets.LoadPlayer(4);
        BattleModel target = assets.LoadPlayer(recipient);
        int launcher = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 4).Id;
        int gloves = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id;
        BattleModel withoutMount = assets.LoadPlayerForWeapon(4, 0, gloves);
        var sourceJoints = BattleHumanoidRigProfiles.SemanticPairs(4, source, 4, source);
        int sourceForearm = sourceJoints.Single(pair => pair.Joint == "forearm-a").Source;
        int[] hands = sourceJoints.Where(pair => pair.Joint is "hand-a" or "hand-b").Select(pair => pair.Source).ToArray();
        BattleMesh original = source.Meshes[0];
        BattleFace[] hardware = original.Faces.Except(withoutMount.Meshes[0].Faces).Where(face => !face.Vertices.All(index => hands.Contains(original.Vertices[index].Bone))).ToArray();
        BattleVertex[] sourceVertices = hardware.SelectMany(face => face.Vertices).Distinct().Order().Select(index => original.Vertices[index]).ToArray();
        Assert.NotEmpty(sourceVertices);
        Assert.All(sourceVertices, vertex => Assert.Equal(sourceForearm, vertex.Bone));
        BattleCombatantDefinition actor = new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = launcher };
        NativeBattleActorSurface surface = Assert.Single(assets.ReadNativeGraphicsResource(actor).ActorSurfaces.Where(surface => surface.Kind == NativeBattleActorSurfaceKind.LauncherMount));
        NativeBattleRawVertex[] targetVertices = surface.Geometry.SelectMany(mesh => mesh.Vertices).ToArray();
        Assert.Equal(sourceVertices.Length, targetVertices.Length);
        Assert.True(targetVertices.Max(vertex => vertex.Position.X) > targetVertices.Min(vertex => vertex.Position.X));
        Assert.True(targetVertices.Max(vertex => vertex.Position.Y) > targetVertices.Min(vertex => vertex.Position.Y));
        Assert.True(targetVertices.Max(vertex => vertex.Position.Z) > targetVertices.Min(vertex => vertex.Position.Z));

        NativeBattleDecodedPose sourcePose = new(default, Enumerable.Repeat(new NativeBattleBonePose(default, new(1024, 1024, 1024)), source.Bones.Count).ToImmutableArray(), false);
        NativeBattleDecodedPose targetPose = BattleHumanoidRigProfiles.CreateRetargeter(4, source, recipient, target).Transfer(sourcePose, false);
        var sourceBones = NativeBattlePoseMatrices.StageBoneMatrices(source.NativePoseSource!, sourcePose);
        var targetBones = NativeBattlePoseMatrices.StageBoneMatrices(target.NativePoseSource!, targetPose);
        NativeBattleSemanticBonePair forearm = BattleHumanoidRigProfiles.SemanticPairs(4, source, recipient, target).Single(pair => pair.Joint == "forearm-a");
        for (int index = 0; index < sourceVertices.Length; index++)
        {
            NativeBattleFullMacVertex expected = NativeBattleGfMatrices.TransformMac460840(sourceBones[sourceForearm], sourceVertices[index].NativePosition!.Value);
            Assert.Equal(forearm.Recipient, targetVertices[index].Bone);
            NativeBattleFullMacVertex actual = NativeBattleGfMatrices.TransformMac460840(targetBones[forearm.Recipient], targetVertices[index].Position);
            Assert.InRange(Math.Abs((expected.X - sourceBones[sourceForearm].X) - (actual.X - targetBones[forearm.Recipient].X)), 0, 2);
            Assert.InRange(Math.Abs((expected.Y - sourceBones[sourceForearm].Y) - (actual.Y - targetBones[forearm.Recipient].Y)), 0, 2);
            Assert.InRange(Math.Abs((expected.Z - sourceBones[sourceForearm].Z) - (actual.Z - targetBones[forearm.Recipient].Z)), 0, 2);
        }
        Assert.Same(source, assets.LoadPlayerForWeapon(4, 0, launcher));
        Assert.DoesNotContain(assets.ReadNativeGraphicsResource(actor with { ModelId = 4 }).ActorSurfaces, item => item.Kind == NativeBattleActorSurfaceKind.LauncherMount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_mount_moves_without_recipient_skin_or_sleeves(bool remastered)
    {
        string game = GameDirectory(remastered);
        using BattleAssets assets = new(game);
        BattleModel original = assets.LoadPlayer(4);
        int launcher = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 4).Id;
        int gloves = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id;
        Assert.Same(original, assets.LoadPlayerForWeapon(4, 0, launcher));
        BattleModel foreign = assets.LoadPlayerForWeapon(4, 0, gloves);
        BattleMesh own = original.Meshes[0];
        BattleMesh replaced = foreign.Meshes[0];
        Assert.Equal(own.Vertices, replaced.Vertices);
        int forearm = BattleHumanoidRigProfiles.SemanticPairs(4, original, 4, original).Single(pair => pair.Joint == "forearm-a").Recipient;
        int[] hands = BattleHumanoidRigProfiles.SemanticPairs(4, original, 4, original).Where(pair => pair.Joint is "hand-a" or "hand-b").Select(pair => pair.Recipient).ToArray();
        BattleFace[] removedMount = own.Faces.Except(replaced.Faces).Where(face => !face.Vertices.All(index => hands.Contains(own.Vertices[index].Bone))).ToArray();
        Assert.Equal(remastered ? 150 : 31, removedMount.Length);
        Assert.All(removedMount, face => Assert.All(face.Vertices, index => Assert.Equal(forearm, own.Vertices[index].Bone)));
        Assert.All(own.Faces.Where(face => face.Vertices.Any(index => own.Vertices[index].Bone != forearm) && !face.Vertices.All(index => hands.Contains(own.Vertices[index].Bone))), face => Assert.Contains(face, replaced.Faces));
        foreach (int recipient in new[] { 0, 1, 6, 9 })
        {
            BattleCombatantDefinition actor = new() { Id = "player", IsPlayer = true, ModelId = recipient, WeaponId = launcher };
            NativeBattleGraphicsResource equipped = assets.ReadNativeGraphicsResource(actor);
            NativeBattleActorSurface mount = Assert.Single(equipped.ActorSurfaces.Where(surface => surface.Kind == NativeBattleActorSurfaceKind.LauncherMount));
            Assert.Equal(removedMount.Length, mount.Geometry.Sum(mesh => mesh.Polygons.Length));
            int recipientForearm = BattleHumanoidRigProfiles.SemanticPairs(4, original, recipient, assets.LoadPlayer(recipient)).Single(pair => pair.Joint == "forearm-a").Recipient;
            Assert.All(mount.Geometry.SelectMany(mesh => mesh.Vertices), vertex => Assert.Equal(recipientForearm, vertex.Bone));
            Assert.Equal(original.NativeBodyTexturePack!.Data, mount.TexturePack.Data);
            NativeBattleGraphicsResource free = assets.ReadNativeLimitPresentationResource(actor, BattleLimitFamily.Duel, equipped);
            Assert.DoesNotContain(free.ActorSurfaces, surface => surface.Kind == NativeBattleActorSurfaceKind.LauncherMount);
            if (recipient is 1 or 9)
            {
                Assert.Contains(equipped.ActorSurfaces, surface => surface.Kind == NativeBattleActorSurfaceKind.IntrinsicHands);
                Assert.Contains(free.ActorSurfaces, surface => surface.Kind == NativeBattleActorSurfaceKind.IntrinsicHands);
            }
        }
        BattleCombatantDefinition rinoa = new() { Id = "player", IsPlayer = true, ModelId = 4, WeaponId = gloves };
        NativeBattleGraphicsResource gloveResource = assets.ReadNativeGraphicsResource(rinoa);
        Assert.Empty(gloveResource.ActorSurfaces);
        NativeBattleGraphicsResource duel = assets.ReadNativeLimitPresentationResource(rinoa, BattleLimitFamily.Duel, gloveResource);
        Assert.Empty(duel.ActorSurfaces);
        Assert.Equal(own.Faces.Count - removedMount.Length, duel.Geometry!.Value[0].Polygons.Length);
        NativeBattleGraphicsResource combine = assets.ReadNativeLimitPresentationResource(rinoa, BattleLimitFamily.Combine, gloveResource);
        Assert.Single(combine.ActorSurfaces, surface => surface.Kind == NativeBattleActorSurfaceKind.LauncherMount);
    }

    private static string GameDirectory(bool remastered)
    {
        if (!remastered)
        {
            return TestResources.GameDirectory;
        }
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(Path.Combine(game!, "main.zzz")), "Select optional Remastered resources with MEMORIA_FF8_REMASTERED_GAME_DIR.");
        return game!;
    }
}
