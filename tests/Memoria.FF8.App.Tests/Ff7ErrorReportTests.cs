using System.IO.Compression;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Platform;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.Tests;

public sealed class Ff7ErrorReportTests
{
    [Fact]
    public void RetainedGameplayComponentsUseTheirOwnCanonicalReportCodec()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Selected FFVIII resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        GameplaySaveComponentDocument native = GameplaySaveSessionLoaderTests.Document(assets, world: true);
        using MemoryStream source = new();
        GameplaySaveCodec.Write(source, native);
        ReportSave normalized = RuntimeErrorReport.NormalizeSave(new(source.ToArray(), "retained world", DateTimeOffset.UnixEpoch, "native component fixture"));
        using MemoryStream portable = new(normalized.CopyBytes(), writable: false);
        GameplaySaveComponentDocument restored = GameplaySaveCodec.Read(portable);
        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombinedReportPreservesNativeFf7StateAndPackagesALoadableCombinedSave(bool includeFf8)
    {
        FieldMachine machine = new(new FieldProgram([0, 0x5F, 0x12, 1], [new ushort[32]]));
        machine.Initialize();
        Ff7SessionSave ff7 = new()
        {
            ResourceProfile = "fixture",
            Language = "en",
            FieldName = "fixture",
            Progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new(0, 2)], Materia = [], Gil = 240 },
            Vm = machine.Capture(),
            Process = new()
        };
        CombinedSessionSave combined = new() { ActiveGame = SessionGame.Ff7, Ff7 = ff7, Ff8Payload = includeFf8 ? JsonSerializer.SerializeToUtf8Bytes(new GameState { Scene = SceneKind.Field, FieldId = 165, X = 25 }) : null, Ff8PayloadFormat = includeFf8 ? "ff8-runtime" : null };
        using MemoryStream original = new();
        CombinedSessionCodec.Write(original, combined);
        byte[] source = original.ToArray();
        ReportSave save = new(source, "pre-fault combined continuation", DateTimeOffset.UnixEpoch, "authored native continuation");
        ErrorIncident incident = ErrorIncident.Capture(new InvalidDataException("KAWAI selector2 fixture"), "FFVII field", save: save);
        string directory = Path.Combine(Path.GetTempPath(), "memoria-combined-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            using PreparedErrorReport report = await GameErrorReport.PrepareAsync(incident, directory);
            ErrorIncident recovered = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(directory));
            Assert.NotNull(recovered.Save);
            using MemoryStream recoveredSource = new(recovered.Save.CopyBytes(), writable: false);
            Assert.True(CombinedSessionCodec.HasSignature(recoveredSource));
            using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
            ZipArchiveEntry entry = Assert.Single(archive.Entries, item => item.FullName.StartsWith("repro/", StringComparison.Ordinal));
            Assert.Equal("repro/state.ffsave", entry.FullName);
            using Stream stream = entry.Open();
            CombinedSessionSave restored = CombinedSessionCodec.Read(stream);
            Assert.Equal(JsonSerializer.Serialize(ff7), JsonSerializer.Serialize(restored.Ff7));
            if (includeFf8)
            {
                string foreign = Path.Combine(directory, "foreign.save");
                File.WriteAllBytes(foreign, restored.Ff8Payload!);
                GameState state = GameSession.Load(foreign);
                Assert.Equal(165, state.FieldId);
                Assert.Equal(25, state.X);
            }
            Assert.Equal(source, save.CopyBytes());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReportButtonInvokesTheSelectedGameAdapter()
    {
        TaskCompletionSource<PreparedErrorReport> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using ErrorReportPresenter presenter = new(_ => { }, incident =>
        {
            calls++;
            return pending.Task;
        });
        presenter.Reset("Native FFVII fault");
        presenter.Incident = ErrorIncident.Capture(new InvalidDataException("native fixture"), "FFVII field");
        UiButton button = Descendants(presenter.View("Stopped").Root).OfType<UiButton>().Single(item => item.Text == "Report error");
        button.Click!();
        button.Click!();
        Assert.Equal(1, calls);
        pending.SetCanceled(TestContext.Current.CancellationToken);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
