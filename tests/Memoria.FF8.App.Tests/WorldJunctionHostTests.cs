using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldJunctionHostTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Prepared_world_entry_binds_auto_junction_before_the_first_menu_action(int priority)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld host = new(session, directory, validateWorldPresentation: false);
        host.EnsureField();
        for (int callback = 0; callback < 8 && host.Events.Scene.WorldMusicSelection is null; callback++)
        {
            Assert.True(host.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.NotNull(host.Events.Scene.WorldMusicSelection);
        // Isolated field allocation for host integration; no gameplay route is replayed.
        session.Restore(session.State with { FieldLoadContext = null, FieldId = 159, FieldName = "bggate_1", FieldArrival = new FieldTransition(159, short.MaxValue, short.MaxValue, short.MaxValue, 0) });
        host.EnsureField();
        // Explicit silent channels for this component's field music initialization.
        JsonNode field = JsonNode.Parse(host.Events.Capture().GetRawText())!;
        field["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(0, 255));
        field["Scene"]!["MusicChannel112"] = JsonSerializer.SerializeToNode(new NativeMusicChannelState(1, 255));
        host.Events.Restore(JsonSerializer.SerializeToElement(field));
        for (int callback = 0; callback < 8 && host.Events.Scene.NativeFieldLoadVersion != 1; callback++)
        {
            Assert.True(host.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeCharacterVitals source = session.State.CharacterVitals!.AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0);
        source = source.ChangeMagicStock(0, 1, 100, MagicOperationReason.ScriptReward, out _);
        source = MagicModeCoordinator.Preview(source, new GameplayPreferences { SharedMagicStock = true }).Vitals;
        session.Restore(session.State with { CharacterVitals = source, GameplayPreferences = new GameplayPreferences { SharedMagicStock = true } });
        NativeWorldControlState controls = session.WorldControls!;
        NativeWorldControlState loader = controls.WithWorldEntryMenuFlag();
        NativeWorldTransitionRequest request = new(1, host.Events.FieldId, host.Events.Frame, NativeWorldTransitionOrigin.Script, new WorldTransition(0, 0, -1), controls, loader, loader);

        Action commit = host.Events.PrepareWorldTransition!(request)!;
        commit();
        Assert.False(host.EnsureField());
        Assert.NotNull(host.WorldScene!.ReadFieldMagicStatRules);
        Assert.NotNull(host.WorldScene.ResolveNativeAutoJunction);
        NativeCharacterVitals expected = catalog.AutoJunction(source, 0, priority);
        Assert.Contains(expected.Records[0].Junction.MagicJunctions, spell => spell != 0);

        PlayerActionResult applied = host.Apply(new PlayerAction(PlayerActionKind.AutoJunction, 0, priority.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.True(applied.Accepted);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.State.CharacterVitals));
        Assert.Equal(0, host.WorldScene.State.Frame);
    }
}
