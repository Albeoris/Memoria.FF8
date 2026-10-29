using System.Globalization;
using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ITextTermSource? authoringTermSnapshot;
    private string? authoringTermProblem;
    private Task<ITextTermSource>? authoringTermsLoading;
    private long authoringTermsGeneration = -1;
    private string? detachedAuthoringKey;
    private IReadOnlyList<DialogueVisualPage>? detachedAuthoringPages;
    private string? detachedAuthoringProblem;

    private void PollAuthoringTerms()
    {
        if (resourceHost is null)
        {
            return;
        }
        if (authoringTermsLoading is { IsCompleted: true } completed)
        {
            authoringTermsLoading = null;
            try
            {
                ITextTermSource captured = completed.GetAwaiter().GetResult();
                if (authoringTermsGeneration == resourceHost.Resources.CatalogGeneration)
                {
                    authoringTermSnapshot = captured;
                    if (authoringNotice == authoringTermProblem)
                    {
                        authoringNotice = "";
                    }
                    authoringTermProblem = null;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
            {
                authoringTermProblem = "Preview dictionary context unavailable: " + exception.Message;
                authoringNotice = authoringTermProblem;
            }
        }
        if (authoringTermsLoading is null && authoringTermsGeneration != resourceHost.Resources.CatalogGeneration)
        {
            authoringTermsGeneration = resourceHost.Resources.CatalogGeneration;
            ResourceService source = resourceHost.Resources;
            authoringTermsLoading = Task.Run<ITextTermSource>(() => NativeTextTerms.Capture(source));
        }
    }

    private NativeTextContext AuthoringPreviewContext => editingAuthoringHit?.NativeContext ?? NativeDialogueContext;

    private NativeMessageVariables CapturedAuthoringVariables()
    {
        var values = editingAuthoringBinding?.MessageVariables;
        return new(values?.GetValueOrDefault(0), values?.GetValueOrDefault(1), values?.GetValueOrDefault(2), values?.GetValueOrDefault(3), values?.GetValueOrDefault(4), values?.GetValueOrDefault(5), values?.GetValueOrDefault(6), values?.GetValueOrDefault(7));
    }

    private DialogueDocument ParseAuthoringPreview(string target, bool originalCompatible)
    {
        string body = authoringSyntax.Parse(target).Body;
        if (localizationWorkbench.Session?.Base.Identity.Category == "System")
        {
            object?[] arguments = editingAuthoringBinding?.Arguments.ToArray() ?? [];
            CompositeFormat format = CompositeFormat.Parse(body);
            if (format.MinimumArgumentCount > arguments.Length)
            {
                throw new InvalidDataException("This template's captured formatting arguments are unavailable. The template remains editable; open a live occurrence to preview its values.");
            }
            body = string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
        if (editingAuthoringHit?.FormatTemplate is { } formatTemplate)
        {
            body = formatTemplate(body);
        }
        IReadOnlyDictionary<int, string>? names = editingAuthoringBinding?.Names;
        if (names is null || names.Count == 0)
        {
            names = editingAuthoringBinding?.Arguments.OfType<IReadOnlyDictionary<int, string>>().LastOrDefault();
        }
        return DialogueTextFormatter.Parse(body, names, CapturedAuthoringVariables(), editingAuthoringHit?.ButtonBindings ?? (_ => throw new NotSupportedException("Captured button-binding context is unavailable for this historical occurrence.")), editingAuthoringHit?.Terms, originalCompatible: originalCompatible);
    }

    private void DrawDetachedAuthoringPreview(Rectangle bounds)
    {
        if (authoringDraftPreview is null || !localizationWorkbench.IsEditing || editingAuthoringHit is null || editingAuthoringHit.Dialogue is not null && !editingAuthoringHit.Detached || uiCanvas is null)
        {
            return;
        }
        string key = authoringDraftPreview + "\0" + uiPreferences.TextFontMode + ":" + uiPreferences.TextLayoutMode + ":" + nativeTextFont?.Revision;
        if (key != detachedAuthoringKey)
        {
            detachedAuthoringKey = key;
            detachedAuthoringProblem = null;
            detachedAuthoringPages = null;
            try
            {
                DialogueDocument document = ParseAuthoringPreview(authoringDraftPreview, OriginalTextLayout);
                DialogueState source = editingAuthoringHit.Dialogue ?? new(0, 0, authoringDraftPreview, 8, 8);
                if (NativeGameFonts)
                {
                    float lineHeight = nativeTextFont?.LineHeightFor(AuthoringPreviewContext) ?? 16;
                    float Measure(string value) => nativeTextFont?.Measure(value, AuthoringPreviewContext) ?? 0;
                    detachedAuthoringPages = OriginalTextLayout ? DialogueLayout.CreateNative(document, source, editingAuthoringHit.Window, lineHeight, Measure) : DialogueLayout.Create(document, source, editingAuthoringHit.Window, lineHeight, Measure);
                }
                else
                {
                    using SystemTextMeasurer measure = new(uiPreferences.DialogueFontSize, uiPreferences.DialogueFontFamily);
                    detachedAuthoringPages = DialogueLayout.Create(document, source, editingAuthoringHit.Window, measure.LineHeight, measure.Measure);
                }
                localizationWorkbench.SetPreviewPages(detachedAuthoringPages.Count);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or FormatException or ArgumentException)
            {
                detachedAuthoringProblem = exception.Message;
            }
        }
        if (localizationWorkbench.Session?.Base.Identity.Category == "System" && editingAuthoringHit.FontSize is > 0)
        {
            DrawDetachedSystemTemplate(bounds);
            return;
        }
        float width = Math.Min(650, Math.Max(0, bounds.Width * 0.48f));
        UiRect previewBounds = new(bounds.Right - width - 12, bounds.Top + 80, width, Math.Min(300, bounds.Height - 100));
        uiCanvas.Fill(previewBounds, new UiColor(18, 26, 42, 240));
        uiCanvas.Text(detachedAuthoringProblem ?? "Captured context preview — gameplay is not replayed", 15, new(previewBounds.X + 8, previewBounds.Y + 4, previewBounds.Width - 16, 34), new UiColor(255, 210, 230));
        if (detachedAuthoringPages is not { Count: > 0 } pages)
        {
            return;
        }
        DialogueVisualPage page = pages[Math.Clamp(localizationWorkbench.PreviewPage, 0, pages.Count - 1)];
        float scale = Math.Min((previewBounds.Width - 20) / 320f, (previewBounds.Height - 48) / 240f);
        DialogueState native = editingAuthoringHit.Dialogue ?? new(0, 0, authoringDraftPreview, 8, 8);
        DialogueWindow? nativeWindow = editingAuthoringHit.Window;
        PixelBounds logicalPanel = DialoguePresentation.Bounds(native, nativeWindow, new(0, 0, 320, 240), page.Width, page.Height);
        UiRect panel = new(previewBounds.X + 10 + logicalPanel.X * scale, previewBounds.Y + 38 + logicalPanel.Y * scale, logicalPanel.Width * scale, logicalPanel.Height * scale);
        uiTheme.Panel(uiCanvas, panel);
        float line = NativeGameFonts ? nativeTextFont?.LineHeightFor(AuthoringPreviewContext) ?? 16 : SystemTextLayout.LineHeight(uiPreferences.DialogueFontSize, uiPreferences.DialogueFontFamily);
        float y = panel.Y + DialoguePresentation.Padding * scale;
        Rectangle clip = new((int)previewBounds.X, (int)previewBounds.Y + 38, (int)previewBounds.Width, (int)Math.Max(0, previewBounds.Height - 38));
        foreach (DialogueVisualLine row in page.Lines)
        {
            foreach (DialoguePlacedRun run in row.Runs)
            {
                Vector2 position = new(panel.X + (DialoguePresentation.Padding + run.X) * scale, y);
                if (run.Run.IsButton)
                {
                    ButtonPrompt.Draw(batch, pixel, text, run.Run.Text, uiPreferences.DialogueFontSize * scale, position, line * scale, 1, uiPreferences.DialogueFontFamily, clip, uiCanvas);
                }
                else if (!DrawNativeDialogueText(run.Run.Text, position, scale, run.Run.Color, clip, AuthoringPreviewContext))
                {
                    text.DrawLineClipped(batch, run.Run.Text, uiPreferences.DialogueFontSize * scale, position, run.Run.Color, clip, uiPreferences.DialogueFontFamily);
                }
            }
            y += line * scale;
        }
    }
    private void DrawDetachedSystemTemplate(Rectangle viewport)
    {
        if (editingAuthoringHit is null || uiCanvas is null || detachedAuthoringPages is not { Count: > 0 } pages)
        {
            return;
        }
        float width = Math.Min(editingAuthoringHit.MaximumWidth ?? editingAuthoringHit.Bounds.Width, viewport.Width * 0.48f - 24);
        float size = editingAuthoringHit.FontSize ?? uiPreferences.UiFontSize;
        string family = editingAuthoringHit.FontFamily ?? uiPreferences.UiFontFamily;
        string sample = string.Join('\n', pages.SelectMany(page => page.Lines).Select(line => string.Concat(line.Runs.Select(run => run.Run.Text))));
        UiRect bounds = new(viewport.Right - width - 24, viewport.Y + 80, width, Math.Min(260, viewport.Height - 100));
        uiCanvas.Fill(bounds, new UiColor(18, 26, 42, 245));
        Rectangle clip = new((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        if (NativeGameFonts && nativeTextFont is not null)
        {
            NativeTextContext context = AuthoringPreviewContext with { ResourceFamily = "System", Field = null };
            nativeTextFont!.Draw(batch, sample, new(bounds.X + 6, bounds.Y + 6), size / (nativeTextFont.LineHeightFor(context)), Color.White, context, clip, Math.Max(0, bounds.Width - 12) / (size / nativeTextFont.LineHeightFor(context)), maximumHeight: Math.Max(0, bounds.Height - 12) / (size / nativeTextFont.LineHeightFor(context)));
        }
        else
        {
            text.DrawWrappedClipped(batch, sample, size, new(bounds.X + 6, bounds.Y + 6), Math.Max(0, bounds.Width - 12), Color.White, clip, family);
        }
    }

}
