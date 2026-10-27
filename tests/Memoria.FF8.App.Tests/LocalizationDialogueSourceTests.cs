using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationDialogueSourceTests
{
    [Theory]
    [InlineData(SceneKind.Field, false)]
    [InlineData(SceneKind.Field, true)]
    [InlineData(SceneKind.WorldMap, false)]
    [InlineData(SceneKind.WorldMap, true)]
    public void GeneratedMessagesKeepTheirTextChoicesAndWindowAcrossLocalizationReload(SceneKind scene, bool selection)
    {
        var (host, resources) = CreateHost(scene);
        DialogueState native = new(6, -1, selection ? "Choose{Line}Cancel{Line}{Char Squall}" : "Found Fire!", 0, 0, selection ? 1 : null, selection ? 2 : null, selection ? 2 : null, selection ? 1 : null, Operation: 12);
        DialogueWindow window = new(6, 20, 30, 200, 80);
        foreach (bool translated in new[] { false, true, false })
        {
            resources.PrepareLocalization(translated ? Catalog("Translated") : LocalizationCatalog.Empty).Commit();
            Assert.Same(native, Invoke<DialogueState>(host, "LocalizedDialogue", native));
            Assert.Null(Invoke<LocalizationFieldText?>(host, "ResolveLocalizedField", native));
            Assert.Same(window, Invoke<DialogueWindow?>(host, "LocalizedWindow", native, window));
            Assert.Null(Invoke<DialogueWindow?>(host, "LocalizedWindow", native, null));
            Assert.Equal(2, Invoke<int>(host, "LocalizedChoiceLine", native, 2));
        }
    }

    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void TableBackedMessagesStillResolveCurrentTranslationsIncludingOrdinalZero(SceneKind scene)
    {
        var (host, resources) = CreateHost(scene);
        DialogueState native = new(0, 0, "Original", 0, 0, Operation: 12);
        foreach (string text in new[] { "Перевод", "Updated translation" })
        {
            resources.PrepareLocalization(Catalog(text)).Commit();
            Assert.Equal(text, Invoke<DialogueState>(host, "LocalizedDialogue", native).Text);
            Assert.Equal("Original", native.Text);
            if (scene == SceneKind.Field)
            {
                DialogueWindow window = Invoke<DialogueWindow>(host, "LocalizedWindow", native, null);
                Assert.Equal(240, window.Width);
            }
        }
    }

    private static (RecreateGame Host, ResourceService Resources) CreateHost(SceneKind scene)
    {
        // Exercise the host's actual text and window resolvers without a graphics device or installed resources.
        ResourceService resources = new(new InMemoryResourceSource(new("fixture", GameDataEdition.Classic, [], "en")));
        GameResourceHost resourceHost = (GameResourceHost)RuntimeHelpers.GetUninitializedObject(typeof(GameResourceHost));
        Set(resourceHost, "<Resources>k__BackingField", resources);
        GameWorld world = (GameWorld)RuntimeHelpers.GetUninitializedObject(typeof(GameWorld));
        Set(world, "<Field>k__BackingField", new FieldAssets(600, "fixture", [], [], [], null!, null!, []));
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        GameSession cold = GameSession.CreateColdProcess();
        Set(host, "session", new GameSession(cold.State with { Scene = scene, FieldId = 600, WorldArrival = scene == SceneKind.WorldMap ? new WorldTransition(0, 0, 0) : null }));
        Set(host, "resourceHost", resourceHost);
        Set(host, "world", world);
        return (host, resources);
    }

    private static LocalizationCatalog Catalog(string text)
    {
        string hash = Convert.ToHexString(SHA256.HashData([]));
        LocalizationFieldText field = new(text, hash, 20, 30, 240, 80, null, null, null, null, [new(12, 0, false, null, null, null, null)]);
        return new(new Dictionary<string, LocalizationValue>
        {
            ["$en_fixture_0000"] = new(text, "fixture", "fixture") { Field = field },
            ["$en_messages_world_0000"] = new(text, "fixture", "fixture")
        });
    }

    private static T Invoke<T>(RecreateGame host, string method, params object?[] arguments)
    {
        return (T)typeof(RecreateGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, arguments)!;
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
