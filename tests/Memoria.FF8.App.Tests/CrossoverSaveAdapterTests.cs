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
    public void Active_FF7_envelopes_copy_without_rewriting_either_world_or_mapping(bool suspendedFf8)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-crossover-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint"), directory, originalDirectory: directory, discoverOriginalSaves: false, loadMemoria: _ => throw new InvalidOperationException("Opaque copies must not project the inactive FFVIII world."));
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
            Assert.True(entry.CanCopy);
            PlayerSaveMenu menu = new(store);
            menu.Open(PlayerSaveMenuMode.Load);
            menu.SelectSlot(1);
            Assert.True(menu.CanTransfer);
            menu.RequestTransfer();
            Assert.True(menu.IsTransferring);
            Assert.Equal(1, menu.TransferSource);
            Assert.Equal(SessionGame.Ff7, entry.ActiveGame);
            Assert.Contains("FINAL FANTASY VII", entry.Label);
            Assert.Contains("md1stin", entry.Details);
            Assert.Null(entry.PlayTime);
            Assert.Empty(entry.PartyPortraits);
            Assert.Equal(entry.Label, Assert.Single(store.ReadEntries(false), item => item.Slot == 1).Label);
            store.Transfer(1, 2, _ => throw new InvalidOperationException("Inactive FFVIII validation must not run for an opaque FFVII copy."));
            Assert.Equal(before, File.ReadAllBytes(store.GetPath(2)));
            CombinedSessionSave copied = Assert.IsType<CombinedSessionSave>(CrossoverSaveAdapter.ReadEnvelope(store.GetPath(2)));
            Assert.Equal(SessionGame.Ff7, copied.ActiveGame);
            Assert.Equal(combined.Ff8Payload, copied.Ff8Payload);
            Assert.Equal(combined.Mappings, copied.Mappings);
            Assert.Equal(combined.Ff7.Progress.Gil, copied.Ff7!.Progress.Gil);
            Assert.Throws<NotSupportedException>(() => CrossoverSaveAdapter.ReadSummary(store.GetPath(1)));
            string originalTarget = store.GetPath(PlayerSaveStore.MemoriaSlotCount + 1);
            File.WriteAllBytes(originalTarget, [4, 5, 6]);
            Assert.Throws<NotSupportedException>(() => store.Transfer(1, PlayerSaveStore.MemoriaSlotCount + 1));
            Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(originalTarget));
            Assert.Throws<NotSupportedException>(() => store.Save(PlayerSaveStore.MemoriaSlotCount + 1, new GameSession(new() { Scene = SceneKind.Field }), combinedSession: true));
            Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(originalTarget));
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
