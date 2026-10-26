using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationExcelCoexistenceTests(ITestOutputHelper output) : IDisposable
{
    private const string Key = "$en_excel_acceptance_0000";
    private const string Initial = "Initial translation";
    private const string Updated = "日本語 — Перевод — Ελληνικά";
    private static readonly string ParentDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Memoria.LocalizationExcel.Tests"));
    private readonly string root = Path.Combine(ParentDirectory, Guid.NewGuid().ToString("N"));

    [Fact]
    public void OptionalInstalledExcelKeepsWorkbookOpenDuringProjectReadsAndUnicodeSave()
    {
        Type excelType = RequireExcel();
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        RunInSta(() =>
        {
            string package = CreatePackage(cancellation);
            using ExcelWorkbook excel = new(excelType, WorkbookPath(package));
            output.WriteLine("Owned hidden Microsoft Excel version: {0}", excel.Version);
            Assert.Equal(Initial, Assert.Single(LocalizationProjectIO.Read(package).Entries).Target);
            excel.SetTarget(Updated);
            Assert.Equal(Initial, Assert.Single(LocalizationProjectIO.Read(package).Entries).Target);
            excel.Save();
            Assert.Equal(Updated, Assert.Single(LocalizationProjectIO.Read(package).Entries).Target);
            Assert.True(excel.IsOpen);
        });
    }

    [Fact]
    public void OptionalInstalledExcelPublishesWatchedHostReloadWhileWorkbookRemainsOpen()
    {
        Type excelType = RequireExcel();
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(game) && Directory.Exists(game), "Configure MEMORIA_FF8_GAME_DIR with installed Classic resources.");
        using (IResourceSource source = GameDataSourceFactory.Open(game!))
        {
            Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "Classic resources are required.");
        }
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        RunInSta(() =>
        {
            string package = CreatePackage(cancellation);
            string mods = Path.Combine(root, "Mods");
            File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nname=Excel coexistence acceptance\nenabled=1\n");
            File.WriteAllText(Path.Combine(mods, "localization-stack.json"), JsonSerializer.Serialize(new[] { "Memoria/ExcelAcceptance" }));
            using ExcelWorkbook excel = new(excelType, WorkbookPath(package));
            output.WriteLine("Owned hidden Microsoft Excel version: {0}", excel.Version);
            using GameResourceHost host = new(game!, Path.Combine(root, "application"), watch: true, modsDirectory: mods);
            PumpUntil(host, () => host.Resources.Localize(Key, "Synthetic source") == Initial, cancellation);
            Assert.Equal(Initial, Assert.Single(LocalizationProjectIO.Read(package).Entries).Target);
            long initialGeneration = host.Resources.Generation;
            excel.SetTarget(Updated);
            excel.Save();
            Assert.Equal(Updated, Assert.Single(LocalizationProjectIO.Read(package).Entries).Target);
            PumpUntil(host, () => host.Resources.Localize(Key, "Synthetic source") == Updated, cancellation);
            Assert.Equal(initialGeneration + 1, host.Resources.Generation);
            Assert.Equal(1, host.LastLocalizationChangedKeys);
            Assert.True(excel.IsOpen);
        });
    }

    private string CreatePackage(CancellationToken cancellation)
    {
        string package = Path.Combine(root, "Mods", "Memoria", "ExcelAcceptance");
        LocalizationProject project = new()
        {
            Manifest = new() { TargetLanguage = "excel-acceptance", MappingVersion = FieldCorrespondenceCatalog.Version },
            Entries = [new() { Category = "System", File = "excel_acceptance", Key = Key, Source = "Synthetic source", Target = Initial }]
        };
        LocalizationProjectIO.WriteNew(project, package, "xlsx", "single", cancellation);
        return package;
    }

    private static string WorkbookPath(string package) => Path.Combine(package, "Localization", "localization.xlsx");

    private static Type RequireExcel()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Desktop Microsoft Excel automation requires Windows.");
        Type? excelType = Type.GetTypeFromProgID("Excel.Application", throwOnError: false);
        Assert.SkipUnless(excelType is not null, "Microsoft Excel.Application is not registered; real Excel coexistence was not exercised.");
        return excelType!;
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void PumpUntil(GameResourceHost host, Func<bool> completed, CancellationToken cancellation)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!completed() && timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            cancellation.ThrowIfCancellationRequested();
            host.Pump();
            if (!completed())
            {
                Thread.Sleep(25);
            }
        }
        Assert.True(completed(), "Watched localization publication did not complete: " + string.Join("; ", host.Mods.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
    }

    private sealed class ExcelWorkbook : IDisposable
    {
        private object? application;
        private object? workbooks;
        private object? workbook;
        private object? worksheets;
        private object? worksheet;
        private object? target;

        public ExcelWorkbook(Type excelType, string path)
        {
            try
            {
                application = Activator.CreateInstance(excelType) ?? throw new InvalidOperationException("Excel automation did not create an owned application.");
                dynamic excel = application;
                excel.Visible = false;
                excel.DisplayAlerts = false;
                excel.AskToUpdateLinks = false;
                excel.AutomationSecurity = 3;
                workbooks = excel.Workbooks;
                workbook = ((dynamic)workbooks).Open(Path.GetFullPath(path), UpdateLinks: 0, ReadOnly: false);
                Assert.False((bool)((dynamic)workbook).ReadOnly);
                worksheets = ((dynamic)workbook).Worksheets;
                worksheet = ((dynamic)worksheets).Item["System"];
                object header = ((dynamic)worksheet).Range["D1"];
                try
                {
                    Assert.Equal("Target", (string)((dynamic)header).Value2);
                }
                finally
                {
                    Release(header);
                }
                target = ((dynamic)worksheet).Range["D2"];
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Version => (string)((dynamic)application!).Version;
        public bool IsOpen => workbook is not null && !string.IsNullOrEmpty((string)((dynamic)workbook).Name);

        public void SetTarget(string value)
        {
            ((dynamic)target!).NumberFormat = "@";
            ((dynamic)target!).Value2 = value;
        }

        public void Save() => ((dynamic)workbook!).Save();

        public void Dispose()
        {
            Release(target);
            target = null;
            Release(worksheet);
            worksheet = null;
            Release(worksheets);
            worksheets = null;
            try
            {
                if (workbook is not null)
                {
                    ((dynamic)workbook).Close(SaveChanges: false);
                }
            }
            finally
            {
                try
                {
                    if (application is not null)
                    {
                        ((dynamic)application).Quit();
                    }
                }
                finally
                {
                    Release(workbook);
                    workbook = null;
                    Release(workbooks);
                    workbooks = null;
                    Release(application);
                    application = null;
                }
            }
        }

        private static void Release(object? value)
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }

    public void Dispose()
    {
        string resolved = Path.GetFullPath(root);
        Assert.StartsWith(ParentDirectory + Path.DirectorySeparatorChar, resolved);
        if (Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }
}
