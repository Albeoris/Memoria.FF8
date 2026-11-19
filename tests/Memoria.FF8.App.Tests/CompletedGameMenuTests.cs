using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CompletedGameMenuTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-completed-picker-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SourcePickerFiltersOrdinarySavesAndOwnsCardAndQuickSaveInput()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "diagnostic.json"), directory, discoverOriginalSaves: false);
        store.SaveCompleted(2, CompletedSaveFixture.Document());
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 }));
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.CompletedSource);
        Assert.Equal(2, Assert.Single(menu.Entries).Slot);
        Assert.False(menu.CanTransfer);
        Assert.False(menu.SelectBank(1));
        menu.RequestTransfer();
        Assert.False(menu.IsTransferring);
        int writes = 0;
        int selected = 0;
        await menu.UpdateAsync(new InputFrame(0, 0, false, false, false, true, CardChallenge: true), _ =>
        {
            writes++;
            return Task.CompletedTask;
        }, slot => selected = slot);
        Assert.Equal(0, writes);
        Assert.Equal(0, selected);
        await menu.UpdateAsync(new InputFrame(0, 0, true, false, false, false), _ => Task.CompletedTask, slot => selected = slot);
        Assert.Equal(2, selected);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void CompletedWriterOffersOnlyManualMemoriaDestinations()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "diagnostic.json"), directory, discoverOriginalSaves: false);
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.SaveCompleted);
        Assert.True(menu.IsSaving);
        Assert.All(menu.Entries, entry => Assert.InRange(entry.Slot, 1, PlayerSaveStore.MemoriaSlotCount));
        Assert.False(menu.CanTransfer);
        Assert.False(menu.SelectBank(2));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
