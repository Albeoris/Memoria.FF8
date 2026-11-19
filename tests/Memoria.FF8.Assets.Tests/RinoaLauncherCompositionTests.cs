using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class RinoaLauncherCompositionTests
{
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
