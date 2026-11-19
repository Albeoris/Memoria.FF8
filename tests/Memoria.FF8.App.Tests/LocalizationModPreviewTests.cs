using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationModPreviewTests
{
    [Fact]
    public void Native_tutorial_uses_the_menu_clock_and_renders_the_selected_translation()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Configure installed game resources for the native tutorial.");
        using GameAssets originalAssets = new(gameDirectory!);
        using GameAssets localizedAssets = new(gameDirectory!);
        using GameWorld originalWorld = FreshWorld(originalAssets);
        using GameWorld localizedWorld = FreshWorld(localizedAssets);
        StartLesson(originalWorld, originalAssets);
        StartLesson(localizedWorld, localizedAssets);
        NativeTutorialPlayer original = Assert.IsType<NativeTutorialPlayer>(originalWorld.Tutorial);
        NativeTutorialPlayer localized = Assert.IsType<NativeTutorialPlayer>(localizedWorld.Tutorial);
        using LocalizationTutorialPreview before = new(originalWorld, original, null);
        using LocalizationTutorialPreview after = new(localizedWorld, localized, "selected");
        long fieldFrame = originalWorld.Events.Frame;
        long tutorialFrame = ReadFrame(original);
        Apply(new(PlayerActionKind.Wait));
        Assert.Equal(tutorialFrame + 2, ReadFrame(original));
        Assert.Equal(fieldFrame, originalWorld.Events.Frame);
        ReachInput();
        Assert.Equal(before.Render().Pixels, after.Render().Pixels);

        int lesson = original.Capture().GetProperty("TutorialId").GetInt32();
        int text = original.Capture().GetProperty("TextId").GetInt32();
        string file = $"mngrp_slot{originalAssets.ReadNativeTutorialCatalog().Get(lesson).Descriptor.TextResource:000}_table";
        string key = LocalizationSourceExporter.IndexedKey(originalAssets.Resources.ResourceLanguage, file, text);
        localizedAssets.Resources.PrepareLocalization(new LocalizationCatalog([new(key, new("Selected tutorial text", "selected", "fixture"))])).Commit();
        Assert.False(before.Render().Pixels.SequenceEqual(after.Render().Pixels));
        Apply(new(PlayerActionKind.Confirm));
        ReachInput();
        Assert.NotEqual(text, original.Capture().GetProperty("TextId").GetInt32());
        Apply(new(PlayerActionKind.Cancel));
        Assert.Null(originalWorld.Tutorial);
        Assert.Null(localizedWorld.Tutorial);
        Assert.Equal(originalWorld.Events.Frame, localizedWorld.Events.Frame);

        static GameWorld FreshWorld(GameAssets assets)
        {
            GameSession session = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
            session.StartNewGame();
            return new(session, assets.GameDirectory, resources: assets.Resources, sharedAssets: assets);
        }

        static void StartLesson(GameWorld world, GameAssets assets)
        {
            const int lessonId = 4;
            world.EnsureField();
            world.Events.Step();
            world.Events.Scene.UnlockedTutorials.Add(checked((int)assets.ReadNativeTutorialCatalog().Get(lessonId).Descriptor.UnlockId));
            world.BeginMenuTutorial(lessonId);
        }

        void Apply(PlayerAction action)
        {
            Assert.True(before.Advance(action));
            Assert.True(after.Advance(action));
            Assert.True(before.IsSynchronizedWith(after));
            Assert.Equal(ReadFrame(original), ReadFrame(localized));
        }

        void ReachInput()
        {
            for (int step = 0; step < 25 && !original.WaitingForInput; step++)
            {
                Apply(new(PlayerActionKind.Wait, Frames: 15));
            }
            Assert.True(original.WaitingForInput);
            Assert.True(localized.WaitingForInput);
        }

        static long ReadFrame(NativeTutorialPlayer player) => player.Capture().GetProperty("Frame").GetInt64();
    }

    [Fact]
    public void Native_battle_entry_renders_both_panes_and_dispatches_the_same_hud_command()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_ENTRY_SNAPSHOT");
        Assert.SkipUnless(Directory.Exists(gameDirectory) && File.Exists(checkpoint), "Configure installed game resources and the standalone before-entry battle snapshot.");
        using GameAssets originalAssets = new(gameDirectory!);
        using GameAssets localizedAssets = new(gameDirectory!);
        GameSession original = new(GameSession.Load(checkpoint!));
        GameSession localized = new(GameSession.Load(checkpoint!));
        using GameWorld originalWorld = new(original, gameDirectory!, resources: originalAssets.Resources, sharedAssets: originalAssets);
        using GameWorld localizedWorld = new(localized, gameDirectory!, resources: localizedAssets.Resources, sharedAssets: localizedAssets);
        originalWorld.EnsureField();
        localizedWorld.EnsureField();
        Assert.True(originalWorld.HasPendingBattle);
        Assert.True(localizedWorld.HasPendingBattle);
        const byte seed = 4;
        using LocalizationBattlePreview before = LocalizationBattlePreview.Start(originalWorld, original.State, seed);
        using LocalizationBattlePreview after = LocalizationBattlePreview.Start(localizedWorld, localized.State, seed);
        Assert.Equal(seed, before.Session.ReadDefinition().NativeSeed);
        Assert.Equal(seed, after.Session.ReadDefinition().NativeSeed);
        for (int tick = 0; tick < 1200 && (!before.Session.NativeEntryReady || before.Hud.SelectedActor is null || before.Hud.CommandMenuHidden); tick += 4)
        {
            Apply(new(PlayerActionKind.Wait, Frames: 4));
        }
        Assert.True(before.Session.NativeEntryReady);
        string actorId = Assert.IsType<BattleCombatant>(before.Hud.SelectedActor).Id;
        Assert.Equal(before.Render().Pixels, after.Render().Pixels);
        Apply(new(PlayerActionKind.Confirm));
        Assert.True(before.Hud.SelectingTarget);
        BattleCombatant originalEnemy = Assert.IsType<BattleCombatant>(before.Hud.SelectedTarget);
        Assert.False(originalEnemy.IsPlayer || originalEnemy.HasNativeDisplayNameOverride);
        BattleCombatant localizedEnemy = after.Session.Combatants.Single(actor => actor.Id == originalEnemy.Id);
        const string translatedName = "Preview opponent";
        string nameKey = LocalizationSourceExporter.IndexedKey("en", $"c0m{localizedEnemy.ModelId:000}_names", 0);
        localizedAssets.Resources.PrepareLocalization(new LocalizationCatalog([new(nameKey, new(translatedName, "selected", "fixture"))])).Commit();
        Assert.Equal(originalEnemy.Name, before.Hud.CombatantDisplayName(originalEnemy));
        Assert.Equal(translatedName, after.Hud.CombatantDisplayName(localizedEnemy));

        Assert.False(before.Render().Pixels.SequenceEqual(after.Render().Pixels));
        Apply(new(PlayerActionKind.Cancel));
        Assert.False(before.Hud.SelectingTarget);
        Apply(new(PlayerActionKind.Confirm));
        Apply(new(PlayerActionKind.Confirm));
        BattleAttackCommand originalCommand = Assert.Single(before.Session.QueuedActions, command => command.ActorId == actorId);
        BattleAttackCommand selectedCommand = Assert.Single(after.Session.QueuedActions, command => command.ActorId == actorId);
        Assert.Equal((originalCommand.ActorId, originalCommand.TargetId, originalCommand.Ability, originalCommand.ActionId), (selectedCommand.ActorId, selectedCommand.TargetId, selectedCommand.Ability, selectedCommand.ActionId));

        void Apply(PlayerAction action)
        {
            before.Advance(action);
            after.Advance(action);
            Assert.True(LocalizationBattlePreview.AreSynchronized(before, after, originalWorld.Battle is not null, localizedWorld.Battle is not null));
        }
    }

    [Fact]
    public void Read_only_preview_resolves_the_selected_contribution_without_recovering_authoring_files()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "Memoria.LocalizationPreview.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            LocalizationSourceEntry[] entries = SystemTextCatalog.SourceEntries().Where(entry => !entry.Text.Contains('{')).Take(2).ToArray();
            LocalizationManifest foundation = new() { TargetLanguage = "foundation", MappingVersion = FieldCorrespondenceCatalog.Version };
            LocalizationManifest selected = foundation with { ProjectId = Guid.NewGuid().ToString("D"), BaseProjectId = foundation.ProjectId, TargetLanguage = "selected" };
            ModPackage foundationPackage = WriteSystemPackage(foundation, entries[0]);
            ModPackage selectedPackage = WriteSystemPackage(selected, entries[1]);
            string journal = Path.Combine(selectedPackage.RootDirectory, ".localization-authoring.transaction.json");
            File.WriteAllText(journal, "[]");

            LocalizationPreparation prepared = LocalizationRuntime.PrepareAuthoringProject(new(1, [foundationPackage, selectedPackage], []), selectedPackage.Id, new("fixture", GameDataEdition.Classic, [], "en"), token, readOnly: true);
            using (prepared.InputRevision)
            {
                Assert.Equal("selected", prepared.Catalog.Values[entries[1].Key].Text);
                Assert.False(prepared.Catalog.Values.ContainsKey(entries[0].Key));
                Assert.Equal("[]", File.ReadAllText(journal));
            }

            ModPackage WriteSystemPackage(LocalizationManifest manifest, LocalizationSourceEntry entry)
            {
                string directory = Path.Combine(root, manifest.TargetLanguage);
                LocalizationRecord row = LocalizationProjectBuilder.CreateVerifiedRecord(entry, manifest) with { Target = manifest.TargetLanguage, TargetPresent = true };
                LocalizationProjectIO.WriteNew(new() { Manifest = manifest, Entries = [row] }, directory, "json", "single", token);
                return new("Memoria/" + manifest.TargetLanguage, manifest.TargetLanguage, ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, directory) { HasLocalization = true, LocalizationOnly = true };
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Opening_movie_and_subsequent_dialogue_share_input_with_only_the_selected_translation()
    {
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string toolsDirectory = MediaToolsInstallation.ResolveDirectory();
        Assert.SkipUnless(Directory.Exists(gameDirectory) && MediaToolsInstallation.ToolNames.All(name => File.Exists(MediaToolsInstallation.ResolveExecutable(toolsDirectory, name))), "Configure MEMORIA_FF8_GAME_DIR and the installed FFmpeg tools to test the New Game comparison.");
        CancellationToken token = TestContext.Current.CancellationToken;
        using GameAssets assets = new(gameDirectory!);
        Assert.SkipUnless(assets.Resources.ResourceLanguage == "en", "The comparison fixture requires the installed English game resources.");
        FieldAssets infirmary = assets.LoadField(180);
        const int messageId = 7;
        const string selectedText = "Selected preview translation";
        const string unrelatedText = "Unrelated preview translation";
        string sourceText = infirmary.Messages[messageId];
        FieldCompatibilityInventory inventory = FieldCompatibilityAudit.Prepare(new(infirmary.Name, "en", infirmary.Name, infirmary.Jsm, infirmary.Messages.Select((message, ordinal) => new FieldCompatibilityText(ordinal, message)).ToArray(), DeclaredTextCount: infirmary.Messages.Count)).Inventory;
        FieldCompatibilityContext[] contexts = inventory.Contexts.Where(context => context.IsMessage && context.MessageId == messageId).ToArray();
        Assert.NotEmpty(contexts);
        string root = Path.Combine(Path.GetTempPath(), "Memoria.LocalizationPreview.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            ModPackage selected = WritePackage(root, "selected", selectedText, infirmary.Name, messageId, sourceText, inventory, contexts, token);
            ModPackage unrelated = WritePackage(root, "unrelated", unrelatedText, infirmary.Name, messageId, sourceText, inventory, contexts, token);
            using LocalizationModPreview preview = new(CatalogPreviewPresentation.CreateProfile(gameDirectory!, null, "en"), new(1, [selected], []), toolsDirectory, [unrelated]);
            ModPreviewComparison frame = await preview.RenderAsync(null, token);
            Assert.NotNull(frame.Before.Image);
            Assert.NotNull(frame.After.Image);
            Assert.Equal(frame.Before.Image.Pixels, frame.After.Image.Pixels);

            await preview.RenderAsync(new(PlayerActionKind.Wait, Frames: 12), token);
            await preview.RenderAsync(new(PlayerActionKind.Confirm), token);
            for (int step = 0; step < 24; step++)
            {
                frame = await preview.RenderAsync(new(PlayerActionKind.Wait, Frames: 30), token);
                if (frame.After.Text.Contains(selectedText, StringComparison.Ordinal))
                {
                    break;
                }
                if (frame.Before.Text.Length != 0)
                {
                    await preview.RenderAsync(new(PlayerActionKind.Confirm), token);
                }
            }
            Assert.Contains(selectedText, frame.After.Text);
            Assert.DoesNotContain(selectedText, frame.Before.Text);
            Assert.DoesNotContain(unrelatedText, frame.After.Text);
            Assert.NotNull(frame.Before.Image);
            Assert.NotNull(frame.After.Image);
            Assert.Equal(frame.Before.Image.Pixels, frame.After.Image.Pixels);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ModPackage WritePackage(string root, string name, string text, string field, int message, string original, FieldCompatibilityInventory inventory, FieldCompatibilityContext[] contexts, CancellationToken token)
    {
        string end = original.Contains("{End}", StringComparison.Ordinal) ? "{End}" : "";
        string translated = end.Length != 0 ? original.Replace(end, " " + text + end, StringComparison.Ordinal) : original + " " + text;
        LocalizationRecord row = new()
        {
            Category = "Fields",
            File = field,
            Key = FieldLocalizationKey.Create("en", field, message),
            Source = original,
            Target = translated,
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string?>
                {
                    ["JSM SHA256"] = inventory.JsmSha256,
                    ["Call Contexts"] = JsonSerializer.Serialize(contexts)
                })
            }
        };
        string directory = Path.Combine(root, name);
        LocalizationProjectIO.WriteNew(new() { Manifest = new() { TargetLanguage = name, MappingVersion = FieldCorrespondenceCatalog.Version }, Entries = [row] }, directory, "json", "single", token);
        return new("Memoria/" + name, name, ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, directory) { HasLocalization = true, LocalizationOnly = true };
    }
}
