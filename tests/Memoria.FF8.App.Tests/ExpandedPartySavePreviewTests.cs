using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedPartySavePreviewTests
{
    private static readonly int[] Identities = [3, 0, 2, 7, 1, 9, 4, 8, 5, 10, 6];

    [Theory]
    [InlineData(3, 3, 62)]
    [InlineData(4, 4, 46)]
    [InlineData(5, 3, 62)]
    [InlineData(11, 6, 29)]
    public void PortraitsKeepSavedOrderAndFitInsideTheirOwnSaveSlot(int count, int columns, int thumbnailWidth)
    {
        PlayerSaveEntry entry = Entry(1, count);
        UiGrid blocks = PlayerSaveView.BuildBlocks([entry], loading: true, pageCapacity: 1);
        UiView view = new(blocks);
        view.Resize(960, 480);
        UiButton slot = Assert.IsType<UiButton>(Assert.Single(blocks.Children));
        UiGrid metadata = Assert.IsType<UiGrid>(Assert.Single(slot.Children));
        UiGrid portraits = Assert.IsType<UiGrid>(Assert.Single(metadata.Children, item => item.GridColumn == 2));
        UiRect location = Assert.Single(metadata.Children.OfType<UiLabel>(), item => item.Text == entry.Details).Bounds;
        UiRect timestamp = Assert.Single(metadata.Children.OfType<UiLabel>(), item => item.GridColumn == 1 && item.GridRow == 1).Bounds;
        UiImage[] images = portraits.Children.Cast<UiImage>().ToArray();

        Assert.Equal(204, metadata.ColumnDefinitions[2].Value);
        Assert.Equal(columns, portraits.ColumnDefinitions.Count);
        Assert.Equal(count <= 4 ? 1 : 2, portraits.RowDefinitions.Count);
        if (count > 4)
        {
            Assert.All(portraits.RowDefinitions, row => Assert.Equal(UiGridLength.Pixel(40), row));
        }
        Assert.Equal(Identities[..count], images.Select(image => image.Source!.Value.Id));
        for (int index = 0; index < images.Length; index++)
        {
            UiImage image = images[index];
            Assert.Equal(UiImageKind.CharacterPortrait, image.Source!.Value.Kind);
            Assert.Equal(thumbnailWidth, image.Bounds.Width, 2);
            Assert.Equal(index / columns, image.GridRow);
            Assert.Equal(index % columns, image.GridColumn);
            Assert.True(count <= 4 ? image.Bounds.Height > 0 : image.Bounds.Height >= 39.99f);
            Assert.True(image.Bounds.X >= location.Right && image.Bounds.X >= timestamp.Right);
            Assert.True(image.Bounds.Right <= slot.Bounds.Right);
            Assert.True(image.Bounds.Y >= slot.Bounds.Y && image.Bounds.Bottom <= slot.Bounds.Bottom);
            Assert.All(images.Take(index), previous =>
            {
                UiRect overlap = previous.Bounds.Intersect(image.Bounds);
                Assert.True(overlap.Width < 0.01f || overlap.Height < 0.01f, $"{previous.Bounds} overlaps {image.Bounds}");
            });
        }
        Assert.True(view.Focus(slot));
        Assert.Same(slot, view.Root.HitTest(slot.Bounds.X + 1, slot.Bounds.Y + 1));
    }

    [Fact]
    public void SaveBrowserPaginatesMixedPortraitRowsAndKeepsTheSelectedSlotInView()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"memoria-save-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveMenu menu = OpenBrowser(directory);
            const int expandedSlot = 6;
            menu.SelectSlot(expandedSlot);
            Assert.Equal(11, menu.Entries.Single(entry => entry.Slot == expandedSlot).PartyPortraits.Count);
            bool checkedExpandedRow = false;
            UiView view = PlayerSaveView.Build(menu, "Transfer", "Bank / Page");
            view.Resize(1280, 720);
            view.Draw(new BattleUiTests.TestCanvas(), new UiTheme());
            Assert.InRange(menu.PageCapacity, 1, PlayerSaveMenu.EntriesPerPage - 1);
            Assert.Equal($"save-slot-{expandedSlot}", view.FocusedElement?.Id);
            int expandedCapacity = menu.PageCapacity;
            CheckVisibleSlots();
            Assert.True(checkedExpandedRow);
            menu.RequestPage(1);
            int nextSlot = menu.SelectedSlot;
            Assert.Equal(expandedSlot + expandedCapacity, nextSlot);
            view = PlayerSaveView.Build(menu, "Transfer", "Bank / Page");
            view.Resize(1280, 720);
            view.Draw(new BattleUiTests.TestCanvas(), new UiTheme());
            CheckVisibleSlots();
            Assert.Equal(expandedCapacity, menu.PageCapacity);
            Assert.Equal($"save-slot-{nextSlot}", view.FocusedElement?.Id);

            Assert.True(menu.SelectBank(1));
            int originalSlot = menu.SelectedSlot;
            PlayerSaveView.ConfigureTextScale(menu, 1);
            Assert.True(menu.PageCapacity > expandedCapacity);
            Assert.Equal(originalSlot, menu.SelectedSlot);
            Assert.True(menu.SelectBank(0));
            PlayerSaveView.ConfigureTextScale(menu, 1);
            Assert.Equal(expandedCapacity, menu.PageCapacity);
            Assert.Equal(nextSlot, menu.SelectedSlot);

            void CheckVisibleSlots()
            {
                UiButton[] slots = PlayerSaveView.FindButtons(view.Root).Where(button => button.Id?.StartsWith("save-slot-", StringComparison.Ordinal) == true).ToArray();
                Assert.Equal(menu.VisibleEntries.Select(entry => $"save-slot-{entry.Slot}"), slots.Select(slot => slot.Id));
                for (int index = 0; index < slots.Length; index++)
                {
                    Assert.True(slots[index].Bounds.Bottom <= view.Root.Bounds.Bottom);
                    if (index > 0)
                    {
                        Assert.True(slots[index].Bounds.Y >= slots[index - 1].Bounds.Bottom - 0.01f);
                    }
                    UiGrid metadata = Assert.IsType<UiGrid>(Assert.Single(slots[index].Children));
                    UiGrid portraits = Assert.IsType<UiGrid>(Assert.Single(metadata.Children, child => child.GridColumn == 2));
                    if (portraits.Children.Count > 4)
                    {
                        checkedExpandedRow = true;
                        Assert.All(portraits.Children, portrait => Assert.True(portrait.Bounds.Height >= 39.99f && portrait.Bounds.Bottom <= slots[index].Bounds.Bottom));
                    }
                }
            }
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void RenderExpandedSavePortraitsWhenOutputIsRequested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_SAVE_PREVIEW_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_EXPANDED_SAVE_PREVIEW_OUTPUT to a scratch directory for a save preview.");
        using SkiaSharp.SKBitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
        UiGrid blocks = PlayerSaveView.BuildBlocks([Entry(1, 3), Entry(2, 4), Entry(3, 5), Entry(4, 11)], loading: true, pageCapacity: 4);
        UiAnchorPanel page = new();
        blocks.Anchors = new UiAnchors(0.02f, 0.10f, 0.98f, 0.92f);
        page.Children.Add(blocks);
        UiView view = new(page) { DesignWidth = 960, DesignHeight = 540 };
        view.Resize(bitmap.Width, bitmap.Height);
        view.Draw(canvas, new UiTheme());
        Directory.CreateDirectory(output!);
        bitmap.SavePng(Path.Combine(output!, "expanded-save-portraits.png"));
    }

    private static PlayerSaveMenu OpenBrowser(string directory)
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory, discoverOriginalSaves: false);
        int[] sizes = [3, 4, 3, 4, 5, 11];
        for (int slot = 1; slot <= 12; slot++)
        {
            string path = store.GetPath(slot);
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(new SaveSummary(SceneKind.Field, 106, "Balamb Garden", null, Identities[..sizes[(slot - 1) % sizes.Length]], null, null, null));
            byte[] payload = [0];
            using (BinaryWriter writer = new(File.Create(path), Encoding.UTF8))
            {
                writer.Write("MFF8SAVE"u8);
                writer.Write(1);
                writer.Write(metadata.Length);
                writer.Write((long)payload.Length);
                writer.Write(SHA256.HashData(metadata));
                writer.Write(SHA256.HashData(payload));
                writer.Write(metadata);
                writer.Write(payload);
            }
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 11, 14, 15, 0, 0, DateTimeKind.Utc).AddMinutes(-slot));
        }
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.Load);
        return menu;
    }

    private static PlayerSaveEntry Entry(int slot, int count)
    {
        return new(slot, PlayerSaveStore.GetLabel(slot), new DateTime(2026, 11, 14), "Balamb Garden", true)
        {
            PartyPortraits = Identities[..count],
            PlayTime = TimeSpan.FromMinutes(143),
            SavedLocalAt = new DateTimeOffset(2026, 11, 14, 15, 30, 0, TimeSpan.FromHours(1))
        };
    }
}
