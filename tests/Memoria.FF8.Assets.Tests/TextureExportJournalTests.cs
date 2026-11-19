using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureExportJournalTests
{
    [Fact]
    public void CancellationKeepsCompletedImagesAndMarksIndexIncomplete()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-export-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            using CancellationTokenSource cancellation = new();
            using (TextureExportJournal journal = new(root, "selected", "synthetic", "en", false))
            {
                var progress = new ImmediateProgress(item => cancellation.Cancel());
                Assert.Throws<OperationCanceledException>(() => TextureExchange.Export([Snapshot("first"), Snapshot("second")], root, cancellationToken: cancellation.Token, progress: progress, journal: journal));
            }
            using JsonDocument index = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "texture-export-index.json")));
            Assert.False(index.RootElement.GetProperty("complete").GetBoolean());
            JsonElement entry = Assert.Single(index.RootElement.GetProperty("entries").EnumerateArray());
            string path = Path.Combine(root, entry.GetProperty("path").GetString()!);
            Assert.NotNull(TextureExchange.ReadMetadata(path));
            Assert.Contains("INCOMPLETE", File.ReadAllText(Path.Combine(root, "texture-export-report.txt")));
            Assert.True(File.Exists(Path.Combine(root, "EDITING.txt")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReexportCannotOverwriteAnEditedImageOrItsBaseline()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-export-preserve-" + Guid.NewGuid().ToString("N"));
        try
        {
            TextureSourceSnapshot snapshot = Snapshot("first");
            string path = Assert.Single(TextureExchange.Export([snapshot], root, cancellationToken: TestContext.Current.CancellationToken).Files);
            byte[] edited = [1, 2, 3];
            File.WriteAllBytes(path, edited);
            byte[] baseline = File.ReadAllBytes(path + ".texture.json");
            Assert.Throws<IOException>(() => TextureExchange.Export([snapshot], root, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(edited, File.ReadAllBytes(path));
            Assert.Equal(baseline, File.ReadAllBytes(path + ".texture.json"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static TextureSourceSnapshot Snapshot(string member)
    {
        PixelImage image = new(1, 1, [new(1, 2, 3)]);
        TextureBuildContext context = new(new(new("battle/synthetic.dat"), member), new("synthetic", GameDataEdition.Classic, []), 1, "image", 1, 1, () => image);
        return new(context, new(image, 1, 1, "synthetic"), 0);
    }

    private sealed class ImmediateProgress(Action<TextureExportProgress> report) : IProgress<TextureExportProgress>
    {
        public void Report(TextureExportProgress value) => report(value);
    }
}
