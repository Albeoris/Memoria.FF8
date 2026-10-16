using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleAssetsTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(94, 31, 61)]
    [InlineData(105, 23, 71)]
    public void OriginalReachedEncountersRetainTheirFormationAndLoadOriginalGeometry(int encounterId, int stageId, int enemyId)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(encounterId);
        Assert.Equal(stageId, encounter.StageId);
        Assert.Single(encounter.Enemies);
        Assert.Equal(enemyId, encounter.Enemies[0].ModelId);
        Assert.NotEmpty(assets.LoadStage(stageId).Meshes);
        Assert.NotEmpty(assets.LoadEnemy(enemyId).Meshes);
        Assert.NotEmpty(assets.ReadEnemyName(enemyId));
    }

    [Fact]
    public void DefaultArenaAndModelsRenderDeterministicallyAndReactToAttackMotion()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStage stage = assets.LoadStage(0);
        BattleModel player = assets.LoadPlayer(0);
        BattleModel enemy = assets.LoadEnemy(0);
        Assert.Equal("d0c000.dat", player.Name);
        Assert.Equal(42, player.Animations.Count);
        Assert.Same(player, assets.LoadPlayer(0));
        BattleRenderer renderer = new(320, 180);
        BattleActorDraw[] actors = [new(player, new Vector3(-12, 0, 0)), new(enemy, new Vector3(12, 0, 0), MathF.PI)];
        byte[] idle = renderer.Render(stage, actors).ToArray();
        Assert.True(renderer.DrawnTriangles > 100);
        Assert.True(idle.Where((_, index) => index % 4 != 3).Distinct().Count() > 64);
        Assert.Equal(idle, renderer.Render(stage, actors));
        actors[0] = actors[0] with { AttackOffset = new Vector3(8, 0, 0) };
        Assert.NotEqual(idle, renderer.Render(stage, actors));
        Assert.NotEqual(player.Pose(0, 0).SelectMany(mesh => mesh), player.Pose(0, 1).SelectMany(mesh => mesh));
    }

    [Fact]
    public void OriginalKernelWeaponOffsetsKeepCharacterTierPowerAndModelOwnership()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleWeaponDefinition revolver = assets.WeaponDefinitions[0];
        Assert.Equal("Revolver", revolver.Name);
        Assert.Equal(0, revolver.CharacterId);
        Assert.Equal(20, revolver.AttackPower);
        Assert.Equal(11, revolver.StrengthBonus);
        Assert.Equal(0, revolver.Tier);
        Assert.Equal("d0w000.dat", assets.ResolveWeaponModel(0, 0, assets.LoadPlayer(0))!.Name);
        Assert.Equal("d0w006.dat", assets.ResolveWeaponModel(0, 6, assets.LoadPlayer(0))!.Name);
        Assert.Equal("d4w023.dat", assets.ResolveWeaponModel(4, 19, assets.LoadPlayer(4))!.Name);
        Assert.Null(assets.ResolveWeaponModel(1, 7, assets.LoadPlayer(1)));
        Assert.Null(assets.ResolveWeaponModel(7, 29, assets.LoadPlayer(7)));
        Assert.Null(assets.ResolveWeaponModel(9, 31, assets.LoadPlayer(9)));
        Assert.Throws<NotSupportedException>(() => assets.ResolveWeaponModel(0, 19, assets.LoadPlayer(0)));
        Assert.DoesNotContain(7, assets.WeaponModelIds(0));
    }

    [Fact]
    public void CroppedModelTimTexturesKeepTheirPageRelativeUvsAndVisibleBladeHighlights()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleModel squall = assets.LoadPlayer(0);
        BattleModel revolver = assets.LoadWeapon(0, 0, squall);
        foreach (BattleModel model in new[] { squall, revolver })
        {
            foreach (BattleFace face in model.Meshes.SelectMany(mesh => mesh.Faces))
            {
                var texture = model.Textures[face.Texture];
                Assert.All(face.Uv, uv =>
                {
                    Assert.InRange(uv.X, 0, texture.Width - 1);
                    Assert.InRange(uv.Y, 0, texture.Height - 1);
                });
            }
        }
        // Original d0c000 head TIM begins at VRAM Y128; d0w000 begins at Y96.
        // The previous cropped-image lookup clamped every one of these faces to its final row.
        Assert.Contains(squall.Meshes.SelectMany(mesh => mesh.Faces).Where(face => face.Texture == 1).SelectMany(face => face.Uv), uv => uv.Y < 32);
        BattleRenderer renderer = new(256, 256);
        byte[] pixels = renderer.Render(new BattleStage(0, [], []), [new BattleActorDraw(revolver, Vector3.Zero)], new BattleCamera(new Vector3(0, 12, -32), new Vector3(0, 12, 0)));
        int highlights = Enumerable.Range(0, pixels.Length / 4).Count(index => pixels[index * 4] > 160 && pixels[index * 4 + 1] > 160 && pixels[index * 4 + 2] > 160);
        Assert.True(highlights > 30, $"Expected the installed blade's silver highlights, got {highlights} bright pixels.");
    }

    [Fact]
    public void InformationOnlyEnemyAndEmptyWeaponDoNotFabricateGraphics()
    {
        using BattleAssets assets = new(GameDirectory);
        Assert.Throws<NotSupportedException>(() => assets.LoadEnemy(127));
        Assert.Throws<EndOfStreamException>(() => assets.LoadWeapon(0, 7, assets.LoadPlayer(0)));
    }
}

