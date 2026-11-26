using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>A retained, awaitable in-game review. Cancel is selected whenever a new request opens.</summary>
internal sealed record GameConfirmationRequest(string Message, string Title, string CancelText = "Cancel", string AcceptText = "Continue", string ScrollHint = "Up / Down: scroll review");

internal sealed class GameConfirmationOverlay : IDisposable
{
    private TaskCompletionSource<bool>? completion;
    private CancellationTokenRegistration cancellation;
    private string message = "";
    private string title = "";
    private string cancelText = "Cancel";
    private string acceptText = "Continue";
    private string scrollHint = "Up / Down: scroll review";
    private List<string> bodyLines = [];
    private (float Size, float Width, string Family)? bodyLayout;
    private float bodyLineHeight;
    private float scroll;
    private float maximumScroll;
    private bool acceptSelected;
    private bool mouseArmed;
    private bool disposed;
    private UiRect cancelBounds;
    private UiRect acceptBounds;

    internal bool IsOpen => completion is not null && !completion.Task.IsCompleted;
    internal bool AcceptSelected => acceptSelected;

    internal Task<bool> ShowAsync(string message, string title, CancellationToken cancellationToken = default)
    {
        return ShowAsync(new GameConfirmationRequest(message, title), cancellationToken);
    }

    internal Task<bool> ShowAsync(GameConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsOpen)
        {
            throw new InvalidOperationException("Another confirmation is already open.");
        }
        cancellation.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        message = request.Message;
        title = request.Title;
        cancelText = request.CancelText;
        acceptText = request.AcceptText;
        scrollHint = request.ScrollHint;
        bodyLayout = null;
        bodyLines.Clear();
        scroll = 0;
        maximumScroll = 0;
        acceptSelected = false;
        mouseArmed = false;
        TaskCompletionSource<bool> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        completion = pending;
        Task<bool> task = pending.Task;
        cancellation = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
        return task;
    }

    internal void Handle(UiKey key)
    {
        if (completion is null)
        {
            return;
        }
        switch (key)
        {
            case UiKey.Left:
                acceptSelected = false;
                break;
            case UiKey.Right:
                acceptSelected = true;
                break;
            case UiKey.Up:
                scroll = Math.Max(0, scroll - 40);
                break;
            case UiKey.Down:
                scroll = Math.Min(maximumScroll, scroll + 40);
                break;
            case UiKey.PageUp:
                scroll = Math.Max(0, scroll - 240);
                break;
            case UiKey.PageDown:
                scroll = Math.Min(maximumScroll, scroll + 240);
                break;
            case UiKey.Confirm:
                Complete(acceptSelected);
                break;
            case UiKey.Cancel:
                Complete(false);
                break;
        }
    }

    internal void Pointer(float x, float y, bool pressed)
    {
        if (!pressed)
        {
            mouseArmed = true;
            return;
        }
        if (!mouseArmed || completion is null)
        {
            return;
        }
        mouseArmed = false;
        if (cancelBounds.Contains(x, y))
        {
            Complete(false);
        }
        else if (acceptBounds.Contains(x, y))
        {
            Complete(true);
        }
    }

    private void Complete(bool accepted)
    {
        TaskCompletionSource<bool>? pending = completion;
        completion = null;
        message = "";
        bodyLines.Clear();
        bodyLayout = null;
        cancellation.Dispose();
        pending?.TrySetResult(accepted);
    }

    internal void Draw(IUiCanvas canvas, UiTheme theme, UiRect viewport, string family, bool useNativeFonts = false)
    {
        if (!IsOpen)
        {
            return;
        }
        float scale = Math.Clamp(Math.Min(viewport.Width / 960, viewport.Height / 672), 0.5f, 3);
        float padding = 22 * scale;
        float width = Math.Min(viewport.Width - padding * 2, 860 * scale);
        float height = Math.Min(viewport.Height - padding * 2, 560 * scale);
        UiRect panel = new(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
        canvas.Fill(viewport, new UiColor(0, 0, 0, 210));
        canvas.Fill(panel, new UiColor(16, 24, 36));
        canvas.Line(panel.X, panel.Y, panel.Right, panel.Y, theme.BorderLight, scale);
        canvas.Line(panel.X, panel.Bottom, panel.Right, panel.Bottom, theme.BorderLight, scale);
        canvas.Line(panel.X, panel.Y, panel.X, panel.Bottom, theme.BorderLight, scale);
        canvas.Line(panel.Right, panel.Y, panel.Right, panel.Bottom, theme.BorderLight, scale);
        canvas.Text(title, 25 * scale, new UiRect(panel.X + padding, panel.Y + padding, panel.Width - padding * 2, 40 * scale), theme.Foreground, family);
        UiRect body = new(panel.X + padding, panel.Y + 76 * scale, panel.Width - padding * 2, panel.Height - 166 * scale);
        DrawBody(canvas, theme.Foreground, body, family, scale, useNativeFonts);
        if (maximumScroll > 0)
        {
            canvas.Text(scrollHint, 13 * scale, new UiRect(body.X, body.Bottom + 6 * scale, body.Width, 20 * scale), theme.Muted, family);
        }
        float buttonWidth = Math.Min(200 * scale, (panel.Width - padding * 3) / 2);
        cancelBounds = new UiRect(panel.X + padding, panel.Bottom - 55 * scale, buttonWidth, 36 * scale);
        acceptBounds = new UiRect(panel.Right - padding - buttonWidth, cancelBounds.Y, buttonWidth, cancelBounds.Height);
        theme.Selection(canvas, acceptSelected ? acceptBounds : cancelBounds);
        canvas.Text(cancelText, 21 * scale, cancelBounds.Inset(5 * scale), theme.Foreground, family);
        canvas.Text(acceptText, 21 * scale, acceptBounds.Inset(5 * scale), theme.Foreground, family);
    }

    private void DrawBody(IUiCanvas canvas, UiColor color, UiRect body, string family, float scale, bool useNativeFonts)
    {
        if (body.IsEmpty || message.Length == 0)
        {
            maximumScroll = 0;
            return;
        }

        float size = 20 * scale;
        var layout = (size, body.Width, family);
        if (!useNativeFonts && bodyLayout != layout)
        {
            using PortableTextShaper shaper = PortableTextRasterizer.CreateMeasurer(size, family);
            bodyLines = PortableTextRasterizer.Wrap(message, shaper.Measure, body.Width);
            bodyLineHeight = shaper.LineHeight;
            bodyLayout = layout;
        }

        float textHeight = useNativeFonts ? canvas.MeasureText(message, size, body.Width, family).Height : bodyLines.Count * bodyLineHeight;
        float contentHeight = Math.Max(body.Height, textHeight + 8 * scale);
        maximumScroll = contentHeight - body.Height;
        scroll = Math.Clamp(scroll, 0, maximumScroll);
        canvas.PushClip(body);
        try
        {
            if (useNativeFonts)
            {
                // Native font rendering already clips individual glyphs from an atlas.
                canvas.Text(message, size, new UiRect(body.X, body.Y - scroll, body.Width, contentHeight), color, family, UiAlignment.Start, UiTextFlow.Wrap);
                return;
            }

            // Draw visible rows separately so each raster stays the size of one line.
            const float shadowOffset = 2;
            int first = Math.Max(0, (int)MathF.Floor((scroll - shadowOffset) / bodyLineHeight));
            int end = Math.Min(bodyLines.Count, (int)MathF.Ceiling((scroll + body.Height) / bodyLineHeight));
            for (int index = first; index < end; index++)
            {
                float y = body.Y + index * bodyLineHeight - scroll;
                canvas.Text(bodyLines[index], size, new UiRect(body.X, y, body.Width, bodyLineHeight + shadowOffset), color, family, UiAlignment.Start, UiTextFlow.Wrap);
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    public void Dispose()
    {
        disposed = true;
        TaskCompletionSource<bool>? pending = completion;
        completion = null;
        cancellation.Dispose();
        pending?.TrySetCanceled();
    }
}
