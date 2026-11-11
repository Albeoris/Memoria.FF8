using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldSaveIsolationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-world-save-isolation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Legacy_FF7_slots_remain_readable_without_mutating_or_exposing_them_in_FF8()
    {
        PlayerSaveStore ff8 = Store(SessionGame.Ff8);
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        ff8.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = 8 }));
        string ff8Quick = ff8.GetPath(0);
        byte[] ff8QuickBytes = File.ReadAllBytes(ff8Quick);
        string legacyManual = Path.Combine(directory, "slot-01.json");
        string legacyQuick = Path.Combine(directory, "quick-0000000008.json");
        CombinedSessionSave legacy = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
        CombinedSessionCodec.Save(legacyManual, legacy);
        CombinedSessionCodec.Save(legacyQuick, legacy);
        byte[] legacyManualBytes = File.ReadAllBytes(legacyManual);
        byte[] legacyQuickBytes = File.ReadAllBytes(legacyQuick);
        Assert.Equal(legacyManual, ff7.GetPath(1));
        Assert.Equal(legacyQuick, ff7.GetPath(-8));
        Assert.Equal(legacyQuick, ff7.GetPath(0));
        Assert.True(ff7.ContainsPath(legacyManual));
        Assert.True(ff7.ContainsPath(legacyQuick));
        Assert.Equal(240u, ff7.LoadFf7(1).Ff7!.Progress.Gil);
        Assert.Equal(240u, ff7.LoadFf7(-8).Ff7!.Progress.Gil);
        Assert.True(Assert.Single(ff7.ReadEntries(false), entry => entry.Slot == 1).CanLoad);
        Assert.False(Assert.Single(ff8.ReadEntries(false), entry => entry.Slot == 1).CanLoad);
        Assert.Equal(0, Assert.Single(ff8.ReadQuickEntries()).Slot);
        Assert.Equal(ff8Quick, ff8.GetPath(0));
        Assert.Equal(legacyManualBytes, File.ReadAllBytes(legacyManual));
        Assert.Equal(legacyQuickBytes, File.ReadAllBytes(legacyQuick));
        Assert.False(Directory.Exists(Path.Combine(directory, "ff7")));
        CombinedSessionSave newer = legacy with { Ff7 = legacy.Ff7! with { Progress = legacy.Ff7!.Progress with { Gil = 999 } } };
        ff7.SaveFf7(1, newer);
        Assert.Equal(Path.Combine(directory, "ff7", "slot-01.json"), ff7.GetPath(1));
        Assert.Equal(999u, ff7.LoadFf7(1).Ff7!.Progress.Gil);
        Assert.Equal(legacyManualBytes, File.ReadAllBytes(legacyManual));
        ff7.SetQuickSaveCount(1);
        ff7.SaveFf7(0, newer);
        Assert.Single(ff7.ReadQuickEntries());
        Assert.Equal(Path.Combine(directory, "ff7", "quick-0000000009.json"), ff7.GetPath(0));
        Assert.Equal(999u, ff7.LoadFf7(0).Ff7!.Progress.Gil);
        Assert.Equal(ff8QuickBytes, File.ReadAllBytes(ff8Quick));
        Assert.Equal(8, ff8.Load(0).X);
        Assert.Single(ff8.ReadQuickEntries());
        Assert.Equal(legacyManualBytes, File.ReadAllBytes(legacyManual));
    }

    [Fact]
    public void Duplicate_legacy_quick_sequence_selects_the_current_FF7_file_once()
    {
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        CombinedSessionSave legacy = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
        CombinedSessionSave current = legacy with { Ff7 = legacy.Ff7! with { Progress = legacy.Ff7!.Progress with { Gil = 999 } } };
        string legacyPath = Path.Combine(directory, "quick-0000000001.json");
        string currentPath = Path.Combine(directory, "ff7", "quick-0000000001.json");
        CombinedSessionCodec.Save(legacyPath, legacy);
        CombinedSessionCodec.Save(currentPath, current);
        File.SetLastWriteTimeUtc(legacyPath, DateTime.UtcNow.AddMinutes(1));
        Assert.Single(ff7.ReadQuickEntries());
        Assert.Equal(currentPath, ff7.GetPath(0));
        Assert.Equal(currentPath, ff7.GetPath(-1));
        Assert.Equal(999u, ff7.LoadFf7(0).Ff7!.Progress.Gil);
        Assert.Equal(999u, ff7.LoadFf7(-1).Ff7!.Progress.Gil);
    }

    [Fact]
    public void Saving_FF8_cannot_overwrite_hidden_legacy_FF7_manual_or_quick_files()
    {
        PlayerSaveStore ff8 = Store(SessionGame.Ff8);
        CombinedSessionSave legacy = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
        string manual = Path.Combine(directory, "slot-01.json");
        string quick = Path.Combine(directory, "quick-0000000001.json");
        CombinedSessionCodec.Save(manual, legacy);
        CombinedSessionCodec.Save(quick, legacy);
        byte[] manualBytes = File.ReadAllBytes(manual);
        byte[] quickBytes = File.ReadAllBytes(quick);
        GameSession session = new(new GameState { Scene = SceneKind.Field, X = 8 });
        Assert.Throws<InvalidOperationException>(() => ff8.Save(1, session));
        ff8.Save(0, session);
        Assert.Equal(Path.Combine(directory, "quick-0000000002.json"), ff8.GetPath(0));
        Assert.Equal(manualBytes, File.ReadAllBytes(manual));
        Assert.Equal(quickBytes, File.ReadAllBytes(quick));
    }

    [Fact]
    public void Equal_manual_and_quick_slot_numbers_preserve_each_world_independently()
    {
        PlayerSaveStore ff8 = Store(SessionGame.Ff8);
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        GameSession native = new(new GameState { Scene = SceneKind.Field, FieldName = "ff8", X = 17 });
        CombinedSessionSave guest = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
        ff8.Save(1, native);
        ff8.Save(0, native);
        byte[] ff8Manual = File.ReadAllBytes(ff8.GetPath(1));
        byte[] ff8Quick = File.ReadAllBytes(ff8.GetPath(0));
        ff7.SaveFf7(1, guest);
        ff7.SaveFf7(0, guest);
        Assert.NotEqual(ff8.GetPath(1), ff7.GetPath(1));
        Assert.NotEqual(ff8.GetPath(0), ff7.GetPath(0));
        Assert.Equal(ff8Manual, File.ReadAllBytes(ff8.GetPath(1)));
        Assert.Equal(ff8Quick, File.ReadAllBytes(ff8.GetPath(0)));
        Assert.Equal(17, ff8.Load(1).X);
        Assert.Equal(17, ff8.Load(0).X);
        Assert.Equal("md1stin", ff7.LoadFf7(1).Ff7!.FieldName);
        Assert.Equal(240u, ff7.LoadFf7(0).Ff7!.Progress.Gil);
        Assert.Single(ff8.ReadQuickEntries());
        Assert.Single(ff7.ReadQuickEntries());
    }

    [Fact]
    public void Quick_save_retention_and_cleanup_never_remove_the_other_world()
    {
        PlayerSaveStore ff8 = Store(SessionGame.Ff8);
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        GameSession native = new(new GameState { Scene = SceneKind.Field, X = 1 });
        CombinedSessionSave guest = new() { ActiveGame = SessionGame.Ff7, Ff7 = Ff7Fixture() };
        ff8.SetQuickSaveCount(3);
        ff7.SetQuickSaveCount(1);
        for (int index = 0; index < 3; index++)
        {
            ff8.Save(0, native);
            ff7.SaveFf7(0, guest);
        }
        Assert.Equal(3, ff8.ReadQuickEntries().Count);
        Assert.Single(ff7.ReadQuickEntries());
        byte[] retained = File.ReadAllBytes(ff7.GetPath(0));
        Assert.Equal(2, ff8.PreviewQuickSaveCount(1));
        ff8.CommitQuickSaveCount(1, 2, () => { });
        Assert.Single(ff8.ReadQuickEntries());
        Assert.Single(ff7.ReadQuickEntries());
        Assert.Equal(retained, File.ReadAllBytes(ff7.GetPath(0)));
    }

    [Fact]
    public void Native_bank_copy_preserves_source_and_every_other_destination_slot()
    {
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        string original = Path.Combine(directory, "native");
        Directory.CreateDirectory(original);
        ff7.SetOriginalDirectory(original);
        Ff7NativeSaveSlot source = NativeSlot(10);
        Ff7NativeSaveSlot neighbor = NativeSlot(20);
        Ff7NativeSaveBank first = Ff7NativeSaveBank.Create().WithSlot(0, source).WithSlot(1, neighbor);
        Ff7NativeSaveBank second = Ff7NativeSaveBank.Create().WithSlot(0, neighbor).WithSlot(14, neighbor);
        File.WriteAllBytes(ff7.GetPath(101), first.Encode());
        File.WriteAllBytes(ff7.GetPath(116), second.Encode());
        ff7.TransferFf7(101, 117);
        Assert.Equal(first.Encode(), File.ReadAllBytes(ff7.GetPath(101)));
        Ff7NativeSaveBank result = Ff7NativeSaveBank.Decode(File.ReadAllBytes(ff7.GetPath(116)));
        for (int slot = 0; slot < Ff7NativeSaveBank.SlotCount; slot++)
        {
            Assert.Equal(slot == 1 ? source.Encode() : second.GetSlot(slot).Encode(), result.GetSlot(slot).Encode());
        }
        ff7.TransferFf7(101, 103);
        Ff7NativeSaveBank sameBank = Ff7NativeSaveBank.Decode(File.ReadAllBytes(ff7.GetPath(101)));
        Assert.Equal(source.Encode(), sameBank.GetSlot(0).Encode());
        Assert.Equal(neighbor.Encode(), sameBank.GetSlot(1).Encode());
        Assert.Equal(source.Encode(), sameBank.GetSlot(2).Encode());
    }

    [Fact]
    public void Switching_menu_store_closes_transient_operations_and_does_not_reuse_another_world_cursor()
    {
        PlayerSaveStore ff8 = Store(SessionGame.Ff8);
        PlayerSaveStore ff7 = Store(SessionGame.Ff7);
        PlayerSaveMenu menu = new(ff8);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.SelectSlot(73);
        menu.SetStore(ff7);
        Assert.False(menu.IsOpen);
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(0, menu.Bank);
        Assert.Equal(1, menu.SelectedSlot);
        menu.SelectSlot(25);
        menu.SetStore(ff8);
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(1, menu.SelectedSlot);
        Assert.False(menu.IsTransferring);
        Assert.False(menu.ConfirmOverwrite);
    }

    private PlayerSaveStore Store(SessionGame game)
    {
        return new(Path.Combine(directory, "checkpoint.ffsave"), directory, discoverOriginalSaves: false, game: game);
    }

    private static Ff7NativeSaveSlot NativeSlot(byte marker)
    {
        byte[] data = new byte[Ff7NativeSaveSlot.Size];
        data[4] = marker;
        BinaryPrimitives.WriteUInt16LittleEndian(data, Ff7NativeSaveSlot.ComputeChecksum(data));
        return Ff7NativeSaveSlot.Decode(data);
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

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
