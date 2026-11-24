using Memoria.FF8.Assets;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Desktop dialog localization")]
public sealed class AsyncDialogLocalizationTests
{
    [Fact]
    public async Task Portable_file_requests_preserve_translated_titles_and_filters()
    {
        IDesktopDialogs previous = ModelBrowserDialogs.Desktop;
        RecordingDialogs desktop = new();
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-dialog-localization-" + Guid.NewGuid().ToString("N"));
        try
        {
            ModelBrowserDialogs.Desktop = desktop;
            GlobalSettingsPaths settings = new(isolatedDirectory: directory);
            Dictionary<string, string> translations = Translations();
            string Translate(string key, string fallback) => translations.GetValueOrDefault(key, fallback);
            Assert.Null(await ModelBrowserDialogs.ImportAsync(0, settings, Translate));
            Assert.Null(await ModelBrowserDialogs.ExportAsync(0, "model", settings, Translate, TestContext.Current.CancellationToken));
            Assert.Null(await ModelBrowserDialogs.DestinationAsync(0, directory, Translate));
            Assert.Equal("訳 Import from Blender (as mod)", desktop.Requests[0].Title);
            Assert.Equal("訳 Export to Blender", desktop.Requests[1].Title);
            Assert.Equal("訳 Choose a mod package folder (or create one)", desktop.Requests[2].Title);
            Assert.Collection(desktop.Requests[0].Filters!,
                filter => Assert.Equal("訳 glTF binary model (*.glb)", filter.Name),
                filter => Assert.Equal("vrm", Assert.Single(filter.Extensions)));
            Assert.Equal("訳 glTF binary model (*.glb)", Assert.Single(desktop.Requests[1].Filters!).Name);
            Assert.Null(desktop.Requests[0].InitialPath);
            Assert.Equal("model.glb", desktop.Requests[1].InitialPath);
            Assert.Equal(directory, desktop.Requests[2].InitialPath);
        }
        finally
        {
            ModelBrowserDialogs.Desktop = previous;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public async Task Model_file_dialogs_remember_independent_directories_after_selection()
    {
        IDesktopDialogs previous = ModelBrowserDialogs.Desktop;
        string root = Path.Combine(Path.GetTempPath(), "Memoria-model-dialogs-" + Guid.NewGuid().ToString("N"));
        string settingsDirectory = Path.Combine(root, "settings");
        GlobalSettingsPaths settings = new(isolatedDirectory: settingsDirectory);
        RecordingDialogs desktop = new();
        try
        {
            string importDirectory = Directory.CreateDirectory(Path.Combine(root, "import")).FullName;
            string exportDirectory = Directory.CreateDirectory(Path.Combine(root, "export")).FullName;
            string importPath = Path.Combine(importDirectory, "source.glb");
            string exportPath = Path.Combine(exportDirectory, "target");
            ModelBrowserDialogs.Desktop = desktop;
            desktop.Result = new(DialogStatus.Selected, importPath);
            Assert.Equal(importPath, await ModelBrowserDialogs.ImportAsync(0, settings));
            desktop.Result = new(DialogStatus.Selected, exportPath);
            Assert.Equal(exportPath + ".glb", await ModelBrowserDialogs.ExportAsync(0, "first", settings, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("first.glb", desktop.Requests[1].InitialPath);

            settings = new(isolatedDirectory: settingsDirectory);
            desktop.Result = new(DialogStatus.Cancelled);
            Assert.Null(await ModelBrowserDialogs.ImportAsync(0, settings));
            Assert.Equal(importDirectory + Path.DirectorySeparatorChar, desktop.Requests[2].InitialPath);
            Assert.Null(await ModelBrowserDialogs.ExportAsync(0, "second", settings, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(Path.Combine(exportDirectory, "second.glb"), desktop.Requests[3].InitialPath);

            Assert.Null(await ModelBrowserDialogs.ImportAsync(0, settings));
            Assert.Equal(importDirectory + Path.DirectorySeparatorChar, desktop.Requests[4].InitialPath);
            Directory.Delete(exportDirectory);
            Assert.Null(await ModelBrowserDialogs.ExportAsync(0, "third", settings, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("third.glb", desktop.Requests[5].InitialPath);
        }
        finally
        {
            ModelBrowserDialogs.Desktop = previous;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Asynchronous_confirmation_retains_default_question_and_structured_export_loss_translations()
    {
        Func<GameConfirmationRequest, Task<bool>>? previous = ModelBrowserDialogs.Confirmation;
        List<GameConfirmationRequest> requests = [];
        try
        {
            ModelBrowserDialogs.Confirmation = request =>
            {
                requests.Add(request);
                return Task.FromResult(false);
            };
            Dictionary<string, string> translations = Translations();
            translations[SystemTextCatalog.Key("save.gf-export.loss.remap")] = "GF {0}: {1}, {3} ← {2}";
            string Translate(string key, string fallback) => translations.GetValueOrDefault(key, fallback);
            Assert.False(await ModelBrowserDialogs.ConfirmAsync(0, "Supplied warning", localizeText: Translate));
            Assert.Equal("訳 Model import warning", requests[0].Title);
            Assert.Equal("Supplied warning\n\n訳 Import this model anyway?", requests[0].Message);
            Assert.Equal("訳 Cancel", requests[0].CancelText);
            Assert.Equal("訳 Continue", requests[0].AcceptText);
            NativeGuardianForceLegacyLoss loss = new(3, 9, "remap", "Untranslated diagnostic") { DescriptionId = "remap", DescriptionArguments = [3, 9, 2, 5] };
            NativeGuardianForceLegacyExportPlan plan = new(new GameState(), [loss]);
            Assert.False(await GuardianForceLegacyExportReview.ConfirmAsync(0, plan, Translate));
            Assert.Equal("訳 Original FFVIII GF conversion", requests[1].Title);
            Assert.Contains("GF 3: 9, 5 ← 2", requests[1].Message);
            Assert.DoesNotContain("Untranslated diagnostic", requests[1].Message);
            Assert.Equal("訳 Cancel", requests[1].CancelText);
            Assert.Equal("訳 Export this copy", requests[1].AcceptText);
        }
        finally
        {
            ModelBrowserDialogs.Confirmation = previous;
        }
    }

    private static Dictionary<string, string> Translations()
    {
        Dictionary<string, string> result = SystemTextCatalog.Defaults.ToDictionary(pair => SystemTextCatalog.Key(pair.Key), pair => "訳 " + pair.Value);
        result[SystemTextCatalog.Key("model.dialog.continue")] = "訳 Continue";
        result[SystemTextCatalog.Key("model.dialog.scroll-review")] = "訳 Up / Down: scroll review";
        return result;
    }

    private sealed class RecordingDialogs : IDesktopDialogs
    {
        internal List<FileDialogRequest> Requests { get; } = [];
        internal DialogResult Result { get; set; } = new(DialogStatus.Cancelled);

        public Task<DialogResult> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);
        public Task<DialogResult> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);
        public Task<DialogResult> SelectFolderAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);

        private Task<DialogResult> Record(FileDialogRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }
}
