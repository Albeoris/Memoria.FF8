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
            Dictionary<string, string> translations = Translations();
            string Translate(string key, string fallback) => translations.GetValueOrDefault(key, fallback);
            Assert.Null(await ModelBrowserDialogs.ImportAsync(0, Translate));
            Assert.Null(await ModelBrowserDialogs.ExportAsync(0, "model", Translate));
            Assert.Null(await ModelBrowserDialogs.DestinationAsync(0, directory, Translate));
            Assert.Equal("訳 Import from Blender (as mod)", desktop.Requests[0].Title);
            Assert.Equal("訳 Export to Blender", desktop.Requests[1].Title);
            Assert.Equal("訳 Choose a mod package folder (or create one)", desktop.Requests[2].Title);
            Assert.All(desktop.Requests.Take(2), request => Assert.Equal("訳 glTF binary model (*.glb)", Assert.Single(request.Filters!).Name));
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

        public Task<DialogResult> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);
        public Task<DialogResult> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);
        public Task<DialogResult> SelectFolderAsync(FileDialogRequest request, CancellationToken cancellationToken = default) => Record(request);

        private Task<DialogResult> Record(FileDialogRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(new DialogResult(DialogStatus.Cancelled));
        }
    }
}
