using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Tests;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleSceneLayoutTests
{
    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(6, 8)]
    [InlineData(11, 14)]
    public void RenderInstalledProductionFramingAcrossAuthoredStages(int partyCount, int enemyCount)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_SCENE_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_EXPANDED_SCENE_OUTPUT for the optional scene renders.");
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        foreach (int formation in new[] { 7, 40, 62 })
        {
            BattleEncounter encounter = assets.LoadEncounter(formation);
            BattleDefinition definition = CreateDefinitionForEncounter(assets, partyCount, enemyCount, encounter);
            BattleSession battle = OpenScene(game, assets, definition);
            BattleScenePresentation scene = new(assets, battle.ReadDefinition());
            const int width = 1280;
            const int height = 720;
            UiRect viewport = new(0, 0, width, height);
            BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn());
            hud.AdvancePresentation(TimeSpan.FromSeconds(6.1 / hud.BattleCallbackRate));
            UiView view = hud.BuildView();
            view.Resize(width, height);
            using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            canvas.DrawRgba(scene.Render(battle, width, height), width, height, viewport);
            view.Draw(canvas, new UiTheme());
            Directory.CreateDirectory(output!);
            bitmap.SavePng(Path.Combine(output!, $"battle-{partyCount}x{enemyCount}-stage{encounter.StageId}-{width}x{height}.png"));

            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            Assert.Equal(encounter.StageId, process.Stage.ResourceStageId);
            Assert.Same(process.PreparedPresentation, scene.PresentedNativeFrame);
            Assert.Equal(partyCount, process.Graphics.ActorModels.Roster.Party.Count());
            Assert.Equal(enemyCount, process.Graphics.ActorModels.Roster.Enemies.Count());
            NativeBattleCameraProjection projection = Assert.IsType<NativeBattleCameraProjection>(process.Camera.Projection);
            Assert.Equal(projection.OffsetY!.Value / 240f, scene.Camera.VerticalCenter);
            Assert.Equal(projection.OffsetX!.Value / 320f, scene.Camera.HorizontalCenter);
            Assert.Equal(2 * MathF.Atan(120f / projection.FocalLength!.Value), scene.Camera.FieldOfView);
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 720)]
    public void RenderLastExpandedPartyPageWithLargeText(int width, int height)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_SCENE_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_EXPANDED_SCENE_OUTPUT for the optional HUD renders.");
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = CreateDefinition(assets, 11, 14);
        for (int index = 0; index < 11; index++)
        {
            BattleCombatantDefinition actor = definition.Combatants[index];
            definition.Combatants[index] = actor with { Name = $"{actor.Name} with a deliberately long character name", MaximumHp = 99999, CurrentHp = 99999 };
        }
        BattleSession battle = OpenScene(game, assets, definition);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn()) { TextScale = 1.65f };
        hud.SelectParty(definition.Combatants[10].Id);
        hud.AdvancePresentation(TimeSpan.FromSeconds(6.1 / hud.BattleCallbackRate));
        UiRect viewport = new(0, 0, width, height);
        BattleScenePresentation scene = new(assets, battle.ReadDefinition());
        UiView view = hud.BuildView();
        view.Resize(width, height);
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        canvas.DrawRgba(scene.Render(battle, width, height), width, height, viewport);
        view.Draw(canvas, new UiTheme());
        Directory.CreateDirectory(output!);
        bitmap.SavePng(Path.Combine(output!, $"battle-11x14-large-hud-last-{width}x{height}.png"));
        Assert.Equal(definition.Combatants[10].Id, hud.SelectedActor?.Id);
    }

    [Theory]
    [InlineData(3, 4, 1280, 720)]
    [InlineData(3, 4, 640, 448)]
    [InlineData(4, 5, 1280, 720)]
    [InlineData(4, 5, 640, 448)]
    [InlineData(6, 8, 1280, 720)]
    [InlineData(6, 8, 640, 448)]
    [InlineData(11, 14, 1280, 720)]
    [InlineData(11, 14, 640, 448)]
    public void InstalledArenaAndHudFrameTheActiveRoster(int partyCount, int enemyCount, int width, int height)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleDefinition definition = CreateDefinition(assets, partyCount, enemyCount);
        BattleSession battle = OpenScene(game, assets, definition);
        BattleScenePresentation scene = new(assets, battle.ReadDefinition());
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn());
        hud.AdvancePresentation(TimeSpan.FromSeconds(6.1 / hud.BattleCallbackRate));
        UiRect viewport = new(0, 0, width, height);
        hud.TargetModelBounds = id => scene.GetActorBounds(id, viewport);
        UiView view = hud.BuildView();
        view.Resize(width, height);
        UiTheme theme = new();

        byte[] pixels = scene.Render(battle, width, height);
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        canvas.DrawRgba(pixels, width, height, viewport);
        view.Draw(canvas, theme);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_SCENE_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            bitmap.SavePng(Path.Combine(output, $"battle-{partyCount}x{enemyCount}-{width}x{height}.png"));
        }

        BattleCombatant[] active = battle.Combatants.Where(actor => actor.IsActive).ToArray();
        Assert.Equal(partyCount, active.Count(actor => actor.IsPlayer));
        Assert.Equal(enemyCount, active.Count(actor => !actor.IsPlayer));
        Assert.Equal(active.Length, active.Select(actor => actor.Id).Distinct().Count());
        Assert.Equal(definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id), hud.Party.Select(actor => actor.Id));
        foreach (BattleCombatant actor in active)
        {
            UiRect? bounds = scene.GetActorBounds(actor.Id, viewport);
            Assert.True(bounds is { IsEmpty: false } body && body.Right > 0 && body.Bottom > 0 && body.X < width && body.Y < height,
                $"{actor.Id} ({actor.Name}) lacks visible projected bounds at {width}x{height}: {bounds}.");
        }

        BattleUiTests.TestCanvas recording = new();
        view.Draw(recording, theme);
        BattleHudLayout layout = new(viewport);
        for (int index = 0; index < partyCount; index++)
        {
            string label = definition.Combatants[index].Name;
            var text = Assert.Single(recording.TextDraws, draw => draw.Text == label);
            Assert.True(text.Bounds.X >= 0 && text.Bounds.Y >= 0 && text.Bounds.Right <= width && text.Bounds.Bottom <= height, $"{label} is outside the HUD viewport: {text.Bounds}.");
            float fontSize = (partyCount == 3 ? 22 : 18) * layout.Scale;
            fontSize = UiTextOverflow.FitFontSize(fontSize, text.Bounds.Height, SystemTextLayout.LineHeight(fontSize, null));
            Assert.True(canvas.MeasureText(label, fontSize, float.PositiveInfinity).Width <= text.Bounds.Width, $"{label} exceeds its HUD text width at {width}x{height}.");
        }
    }

    private static BattleSession OpenScene(GameAssets game, BattleAssets assets, BattleDefinition definition)
    {
        BattleSession battle = InstalledBattlePractice.Create(definition, BattleAtbMode.TurnBased, game, assets);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return battle;
    }

    private static BattleDefinition CreateDefinition(BattleAssets assets, int partyCount, int enemyCount)
    {
        BattleEnemyPlacement donor = Assert.Single(assets.LoadEncounter(136).Enemies);
        HashSet<int> installedModels = assets.EnemyModelIds.ToHashSet();
        HashSet<int> installedStages = assets.StageIds.ToHashSet();
        BattleEncounter encounter = Enumerable.Range(0, assets.FormationCount).Select(assets.LoadEncounter)
            .Where(candidate => candidate.Enemies.Count == 4 && installedStages.Contains(candidate.StageId) &&
                candidate.Enemies.All(placement => installedModels.Contains(placement.ModelId) && !placement.Hidden && !placement.Untargetable && !placement.InitiallySuspended))
            .OrderByDescending(candidate => candidate.Enemies.Count(placement => placement.ModelId == donor.ModelId))
            .First(candidate => assets.LoadStage(candidate.StageId).IntroductionCameras.ContainsKey(candidate.PrimaryCamera & 0xF7));
        return CreateDefinitionForEncounter(assets, partyCount, enemyCount, encounter);
    }

    internal static BattleDefinition CreateDefinitionForEncounter(BattleAssets assets, int partyCount, int enemyCount, BattleEncounter encounter)
    {
        const int donorFormation = 136;
        BattleEnemyPlacement donor = Assert.Single(assets.LoadEncounter(donorFormation).Enemies);
        BattleCombatantDefinition[] party = Enumerable.Range(0, partyCount).Select(character => new BattleCombatantDefinition
        {
            Id = $"party-{character}",
            Name = GameNames.DefaultName(GameNames.CharacterSubject(character)),
            IsPlayer = true,
            ModelId = character,
            WeaponId = assets.WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == character)?.Id,
            MaximumHp = 500,
            CurrentHp = 500,
            InitialAtb = BattleSession.AtbMaximum
        }).ToArray();
        BattleCombatantDefinition[] authored = encounter.Enemies.Select(placement =>
        {
            byte level = checked((byte)Math.Clamp(placement.Level, 1, 100));
            // This static fixture uses a settled scene with no AI callbacks, as does diagnostic battle preparation.
            NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(placement.ModelId, level) with { NativeDrawTables = null };
            return CreateEnemy(placement, placement.Slot, level) with { NativeTemplate = template };
        }).ToArray();
        BattleEnemyRosterSource source = new(donorFormation, donor.Slot, assets.FormationSourceHash(donorFormation));
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources = Enumerable.Range(NativeBattleSlots.FormationSlotCount, enemyCount - authored.Length)
            .ToImmutableSortedDictionary(slot => slot, _ => source);
        return new()
        {
            StageId = encounter.StageId,
            EncounterId = encounter.Id,
            UsesOriginalEnemySlots = sources.Count > 0,
            AddedEnemySources = sources.Count > 0 ? sources : null,
            Combatants = [.. party, .. authored, .. sources.Keys.Select(slot => CreateEnemy(donor, slot, 15))]
        };

        BattleCombatantDefinition CreateEnemy(BattleEnemyPlacement placement, int slot, byte level)
        {
            return new()
            {
                Id = $"enemy-{slot}",
                Name = assets.ReadEnemyName(placement.ModelId),
                ModelId = placement.ModelId,
                SceneSlot = slot,
                Level = level,
                MaximumHp = 500,
                CurrentHp = 500
            };
        }
    }
}
