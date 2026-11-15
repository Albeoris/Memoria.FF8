using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSaveMenuControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_save_opens_player_slots_and_closes_once_after_cancel_or_success(bool save)
    {
        using SaveField fixture = new();
        NativeSaveMenuController controller = new();
        int openings = 0;
        void Open()
        {
            openings++;
            fixture.Panel.Open(PlayerSaveMenuMode.Save);
        }
        Assert.True(controller.Synchronize(fixture.World, fixture.Panel, Open));
        Assert.Equal(PlayerSaveMenuMode.Save, fixture.Panel.Mode);
        Assert.False(controller.Synchronize(fixture.World, fixture.Panel, Open));
        Assert.Equal(NativePartyMenuPhase.Active, fixture.World.Events.PartyMenu!.Phase);
        int writes = 0;
        fixture.Panel.Update(new InputFrame(0, 0, save, !save, false, false), _ =>
        {
            fixture.World.WriteGameplaySave(fixture.SavePath);
            writes++;
        }, _ => Assert.Fail("Unexpected load."));
        Assert.False(fixture.Panel.IsOpen);
        Assert.Equal(save ? 1 : 0, writes);
        Assert.Equal(save, File.Exists(fixture.SavePath));
        Assert.True(controller.Synchronize(fixture.World, fixture.Panel, Open));
        Assert.Equal(NativePartyMenuPhase.Closing, fixture.World.Events.PartyMenu!.Phase);
        for (int tick = 0; tick < 30; tick++)
        {
            Assert.False(controller.Synchronize(fixture.World, fixture.Panel, Open));
            fixture.World.Events.Step();
        }
        Assert.Equal(1, openings);
        Assert.Null(fixture.World.Events.PartyMenu);
        Assert.Equal(9, fixture.Session.EventMemory.Read(1100, 4).RequireExact());
    }

    [Fact]
    public void Failed_save_keeps_slots_and_native_owner_active()
    {
        using SaveField fixture = new();
        NativeSaveMenuController controller = new();
        void Open()
        {
            fixture.Panel.Open(PlayerSaveMenuMode.Save);
        }
        Assert.True(controller.Synchronize(fixture.World, fixture.Panel, Open));
        fixture.Panel.Update(new InputFrame(0, 0, true, false, false, false), _ => throw new IOException("Save rejected."), _ => Assert.Fail("Unexpected load."));
        Assert.True(fixture.Panel.IsOpen);
        Assert.Contains("Save rejected.", fixture.Panel.Message);
        Assert.False(controller.Synchronize(fixture.World, fixture.Panel, Open));
        Assert.Equal(NativePartyMenuPhase.Active, fixture.World.Events.PartyMenu!.Phase);
        Assert.Equal(0, fixture.Session.EventMemory.Read(1100, 4).RequireExact());
    }

    [Fact]
    public void Reload_at_native_save_replaces_panel_owner_without_cancelling_the_old_world()
    {
        using SaveField fixture = new();
        NativeSaveMenuController controller = new();
        int openings = 0;
        void Open()
        {
            openings++;
            fixture.Panel.Open(PlayerSaveMenuMode.Save);
        }
        Assert.True(controller.Synchronize(fixture.World, fixture.Panel, Open));
        fixture.World.WriteGameplaySave(fixture.SavePath);
        GameSession restoredSession = new(PlayerStateLoader.Load(fixture.SavePath, fixture.GameDirectory, sharedAssets: fixture.Assets));
        using GameWorld restored = new(restoredSession, fixture.GameDirectory, sharedAssets: fixture.Assets);
        restored.EnsureField();
        Assert.Equal(NativePartyMenuPhase.Active, restored.Events.PartyMenu!.Phase);
        Assert.True(controller.Synchronize(restored, fixture.Panel, Open));
        Assert.Equal(2, openings);
        Assert.True(fixture.Panel.IsOpen);
        fixture.Panel.Close();
        Assert.True(controller.Synchronize(restored, fixture.Panel, Open));
        Assert.Equal(NativePartyMenuPhase.Closing, restored.Events.PartyMenu!.Phase);
        Assert.Equal(NativePartyMenuPhase.Active, fixture.World.Events.PartyMenu!.Phase);
        Assert.Equal(0, fixture.Session.EventMemory.Read(1100, 4).RequireExact());
        for (int tick = 0; tick < 30; tick++)
        {
            restored.Events.Step();
        }
        Assert.Null(restored.Events.PartyMenu);
        Assert.Equal(9, restoredSession.EventMemory.Read(1100, 4).RequireExact());
        Assert.False(controller.Synchronize(null, fixture.Panel, Open));
    }

    private sealed class SaveField : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-native-save-ui-" + Guid.NewGuid().ToString("N"));

        public string GameDirectory { get; } = TestResources.GameDirectory;
        public string SavePath => Path.Combine(directory, "continuation.json");
        public GameAssets Assets { get; }
        public GameSession Session { get; }
        public GameWorld World { get; }
        public PlayerSaveMenu Panel { get; }

        public SaveField()
        {
            string fieldName;
            using (GameAssets original = new(GameDirectory))
            {
                fieldName = original.LoadField(165).Name;
            }
            (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUSAVE, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] script = GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary]);
            Assets = new GameAssets(GameDirectory, overrides: new ScriptOverride(fieldName, script));
            Session = new GameSession(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(Assets, false), GameDirectory, sharedAssets: Assets));
            World = new GameWorld(Session, GameDirectory, sharedAssets: Assets);
            World.EnsureField();
            Session.SetEventMemory(Session.EventMemory.Write(1100, 4, NativeScalarValue.Exact(0)));
            Directory.CreateDirectory(directory);
            Panel = new PlayerSaveMenu(new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "slots")));
            for (int tick = 0; World.Events.PartyMenu?.Phase != NativePartyMenuPhase.Active && tick < 30; tick++)
            {
                World.Events.Step();
            }
            Assert.Equal(NativePartyMenuPhase.Active, World.Events.PartyMenu?.Phase);
        }

        public void Dispose()
        {
            World.Dispose();
            Assets.Dispose();
            Directory.Delete(directory, true);
        }
    }

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
