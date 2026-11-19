using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureExportInstalledTests
{
    [Fact]
    public void ExportInstallationWithExplicitStorageBudget()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_EXPORT_BENCHMARK") == "1" && Directory.Exists(directory), "An explicit installed export benchmark is required.");
        string destination = Path.Combine(Path.GetTempPath(), "memoria-budgeted-texture-export-" + Guid.NewGuid().ToString("N"));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        long allocatedEstimate = 0;
        int written = 0;
        bool completed = false;
        Stopwatch elapsed = Stopwatch.StartNew();
        Stopwatch? cancellationLatency = null;
        Exception? failure = null;
        int catalogCount = 0;
        int resident = -1;
        long peakSourceBytes = 0;
        Dictionary<string, int> outcomes = [];
        try
        {
            using IResourceSource source = GameDataSourceFactory.OpenTextureAuthoring(directory!);
            ResourceService resources = new(source);
            resident = resources.ResidentTextureCount;
            using GameAssets assets = new(directory!, resources: resources);
            TextureCatalogEntry[] catalog = assets.EnumerateTextureCatalog(cancellation.Token, diagnostic => outcomes[diagnostic.Outcome] = outcomes.GetValueOrDefault(diagnostic.Outcome) + 1).ToArray();
            catalogCount = catalog.Length;
            TextureCatalogEntry? current = null;
            void Account(string path)
            {
                // A conservative NTFS allocation estimate includes two minimum clusters and
                // directory/metadata overhead per authored item, in addition to actual file bytes.
                foreach (string file in new[] { path, path + ".texture.json" })
                {
                    long length = new FileInfo(file).Length;
                    allocatedEstimate += (length + 4095) / 4096 * 4096;
                }
                allocatedEstimate += 16 * 1024;
                written++;
                resident = resources.ResidentTextureCount;
                peakSourceBytes = Math.Max(peakSourceBytes, ((ITextureAuthoringResourceSource)source).RetainedTextureAuthoringArchiveBytes);
                if (allocatedEstimate >= 512L * 1024 * 1024)
                {
                    cancellationLatency = Stopwatch.StartNew();
                    cancellation.Cancel();
                }
            }
            IEnumerable<TextureCatalogEntry> Ordinary()
            {
                foreach (TextureCatalogEntry entry in catalog.Where(entry => entry.Layout != "field-workspace"))
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    current = entry;
                    yield return entry;
                }
            }
            TextureExchange.Export(assets, Ordinary(), destination, cancellationToken: cancellation.Token, progress: new ImmediateProgress(_ => Account(Path.Combine(destination, current!.Target.CanonicalPath().Replace('/', Path.DirectorySeparatorChar)))));
            foreach (TextureCatalogEntry field in catalog.Where(entry => entry.Layout == "field-workspace"))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                string path = Path.Combine(destination, "fields", field.Target.Owner.Split('/')[1] + ".ora");
                assets.ExportFieldTextureWorkspace(field.Target.Owner.Split('/')[1], path, cancellation.Token);
                Account(path);
            }
            resident = resources.ResidentTextureCount;
            completed = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested && !TestContext.Current.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            elapsed.Stop();
            cancellationLatency?.Stop();
            using Process process = Process.GetCurrentProcess();
            object report = new { Completed = completed, CatalogCount = catalogCount, Written = written, AllocatedBytesEstimate = allocatedEstimate, LogicalByteBudget = 1024L * 1024 * 1024, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, CancellationLatencyMilliseconds = cancellationLatency?.ElapsedMilliseconds, PeakRetainedSourceBytes = peakSourceBytes, ManagedBytes = GC.GetTotalMemory(false), PeakProcessBytes = process.PeakWorkingSet64, ResidentTextureCount = resident, DiscoveryOutcomes = outcomes, Failure = failure?.ToString() };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_CATALOG_REPORT") is { Length: > 0 } reportDirectory)
            {
                Directory.CreateDirectory(reportDirectory);
                File.WriteAllText(Path.Combine(reportDirectory, "budgeted-installation-export.json"), json);
            }
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }
        }
        Assert.Null(failure);
        Assert.True(written > 0);
        Assert.InRange(allocatedEstimate, 1, 1024L * 1024 * 1024);
        if (completed)
        {
            Assert.Equal(0, resident);
        }
    }

    private sealed class ImmediateProgress(Action<TextureExportProgress> report) : IProgress<TextureExportProgress>
    {
        public void Report(TextureExportProgress value)
        {
            if (value.Stage == "Writing")
            {
                report(value);
            }
        }
    }
}
