using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianModMenuRefreshTests
{
    [Theory]
    [InlineData("gf")]
    [InlineData("gf-learn")]
    [InlineData("gf-abilities")]
    public void PublicationRefreshesOpenGuardianMenuOnlyAfterCommit(string screen)
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-gf-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false, modsDirectory: Path.Combine(root, "Mods"));
            NativeCharacterVitals source = GuardianForceAbilityEditorTests.Source();
            source = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
            GameSession session = new(new GameState { Scene = SceneKind.Field, CharacterVitals = source });
            MenuController menu = new(() => LiveMenuData.Build(session.State));
            // Exercise the publication callback without creating a graphics device or requiring installed resources.
            RecreateGame game = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
            GameResourceHost resources = (GameResourceHost)RuntimeHelpers.GetUninitializedObject(typeof(GameResourceHost));
            Set(resources, "<Mods>k__BackingField", mods);
            Set(resources, "<GuardianForceMods>k__BackingField", GuardianForceAbilityMods.Empty);
            Set(game, "session", session);
            Set(game, "resourceHost", resources);
            Set(game, "gameMenus", menu);
            Set(game, "guardianSourceVitals", source);
            Set(game, "guardianSourceRevision", "source");
            var prepare = typeof(RecreateGame).GetMethod("PrepareGuardianModPublication", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<GuardianForceAbilityMods, ModCatalogSnapshot, ModPreparationResult>>(game);
            GuardianForceAbilityPatchLayer layer = new("Memoria/example", new()
            {
                DocumentId = "example",
                Operations = [new() { Id = "add", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = 4 }]
            });
            menu.Open(screen);
            MenuData before = menu.Data;
            var view = menu.BuildView();
            Assert.DoesNotContain(menu.GuardianForce!.LearningChoices, choice => choice.Id == "4");

            ModPreparationResult prepared = prepare(new([layer]), new(1, [], []));

            Assert.True(prepared.Success, string.Join(Environment.NewLine, prepared.Diagnostics?.Select(diagnostic => diagnostic.Message) ?? []));
            Assert.NotNull(prepared.Commit);
            Assert.Same(before, menu.Data);
            Assert.Same(view, menu.BuildView());
            Assert.DoesNotContain(session.CharacterVitals!.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 4);

            prepared.Commit();

            Assert.True(menu.IsOpen);
            Assert.Equal(screen, menu.ScreenId);
            Assert.NotSame(before, menu.Data);
            Assert.NotSame(view, menu.BuildView());
            Assert.Equal("gf-0", menu.GuardianForce!.Id);
            Assert.Contains(menu.GuardianForce.LearningChoices, choice => choice.Id == "4");
            Assert.Contains(session.CharacterVitals!.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 4);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
