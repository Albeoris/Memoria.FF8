using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSaveAdapterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FF7_debug_state_handoff_uses_its_own_loader_regardless_of_argument_order(bool stateFirst)
    {
        string path = Path.GetFullPath("standalone-ff7.ffsave");
        string[] arguments = stateFirst ? ["--state", path, "--ff7-debug"] : ["--ff7-debug", "--state", path];
        LaunchOptions options = PlayerStateLoader.Parse(arguments);
        Assert.True(options.Ff7Debug);
        Assert.Equal(path, options.CrossoverStatePath);
        Assert.Null(options.StatePath);
        Assert.Equal(SceneKind.Title, options.InitialState.Scene);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FF8_slots_hide_active_FF7_envelopes_and_reject_implicit_world_loading(bool suspendedFf8)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-crossover-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint"), directory, discoverOriginalSaves: false);
            CombinedSessionSave combined = new()
            {
                ActiveGame = SessionGame.Ff7,
                Ff7 = Ff7Fixture(),
                Ff8Payload = suspendedFf8 ? [9, 8, 7] : null,
                Ff8PayloadFormat = suspendedFf8 ? "opaque-test-payload" : null,
                Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.StandardFieldSquallProfileVersion)]
            };
            CombinedSessionCodec.Save(store.GetPath(1), combined);
            byte[] before = File.ReadAllBytes(store.GetPath(1));
            PlayerSaveEntry entry = Assert.Single(store.ReadEntries(false), item => item.Slot == 1);
            Assert.False(entry.CanLoad);
            Assert.False(entry.CanCopy);
            Assert.Equal("save.slot.empty", entry.DetailsSystemId);
            Assert.Throws<InvalidDataException>(() => store.Load(1));
            Assert.Equal(before, File.ReadAllBytes(store.GetPath(1)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FF7_load_menu_uses_its_own_namespace_without_an_active_FF8_continuation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-ff7-own-load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore ff8 = new(Path.Combine(directory, "checkpoint"), directory, discoverOriginalSaves: false);
            PlayerSaveStore ff7 = new(Path.Combine(directory, "checkpoint"), directory, discoverOriginalSaves: false, game: SessionGame.Ff7);
            CombinedSessionSave saved = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
            ff7.SaveFf7(1, saved);
            PlayerSaveEntry own = Assert.Single(ff7.ReadEntries(false), entry => entry.Slot == 1);
            Assert.True(own.CanLoad);
            Assert.True(own.CanCopy);
            Assert.False(Assert.Single(ff8.ReadEntries(false), entry => entry.Slot == 1).CanLoad);
            PlayerSaveMenu menu = new(ff7);
            menu.Open(PlayerSaveMenuMode.Load);
            int loaded = -1;
            menu.Update(new InputFrame(0, 0, true, false, false, false), _ => Assert.Fail("Unexpected save."), slot => loaded = slot);
            Assert.Equal(1, loaded);
            Assert.False(menu.IsOpen);
            Assert.Equal(SessionGame.Ff7, ff7.LoadFf7(1).ActiveGame);
            Assert.Null(ff7.LoadFf7(1).Ff8Payload);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
    private static Ff7SessionSave Ff7Fixture()
    {
        FieldMachine machine = new(new FieldProgram([0, 0x5F, 0x12, 1], [new ushort[32]]));
        machine.Initialize();
        return new()
        {
            ResourceProfile = "fixture",
            Language = "en",
            FieldName = "md1stin",
            Progress = new()
            {
                InitialKernelData = [1],
                Characters = [new(0, "Cloud", 6, 100, 314, 54, new byte[132])],
                Party = [0, 255, 255],
                Inventory = [new(0, 2)],
                Materia = [],
                Gil = 240
            },
            Vm = machine.Capture(),
            Process = new()
        };
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Combined_player_slot_preserves_native_FF8_components_and_uses_the_normal_loader(bool world)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, world);
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        CombinedSessionSave combined = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = payload.ToArray(), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-crossover-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "diagnostic.ff8"), directory, discoverOriginalSaves: false, loadMemoria: path => PlayerStateLoader.Load(path, sharedAssets: assets));
            string path = store.GetPath(1);
            CombinedSessionCodec.Save(path, combined);
            CombinedSessionSave restored = Assert.IsType<CombinedSessionSave>(CrossoverSaveAdapter.ReadEnvelope(path));
            Assert.Equal(combined.Ff8Payload, restored.Ff8Payload);
            Assert.Equal(document.Resume, CrossoverSaveAdapter.ReadFf8(restored).Resume);
            GameState state = store.Load(1);
            Assert.Equal(world ? SceneKind.WorldMap : SceneKind.Field, state.Scene);
            Assert.Equal(document.Economy.Gil, state.Economy!.Gil);
            PlayerSaveEntry entry = Assert.Single(store.ReadEntries(false), item => item.Slot == 1);
            Assert.True(entry.CanLoad);
            Assert.Equal(document.Progression!.Clocks.Playtime!.Seconds, entry.PlayTime!.Value.TotalSeconds);
            CombinedSessionSave ff7Active = combined with { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
            CombinedSessionCodec.Save(path, ff7Active);
            PlayerSaveEntry ff7Entry = Assert.Single(store.ReadEntries(false), item => item.Slot == 1);
            Assert.True(ff7Entry.CanLoad);
            Assert.Contains("FINAL FANTASY VII", ff7Entry.Label);
            Assert.Null(ff7Entry.PlayTime);
            Assert.Empty(ff7Entry.PartyPortraits);
            store.Transfer(1, 2);
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(store.GetPath(2)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Unknown_FF8_execution_contract_is_rejected_before_any_game_is_replaced()
    {
        CombinedSessionSave combined = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = [1, 2, 3], Ff8PayloadFormat = "unsupported" };
        Assert.Throws<NotSupportedException>(() => CrossoverSaveAdapter.ReadFf8(combined));
    }
}
