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
        cancellation.Dispose();
        pending?.TrySetResult(accepted);
    }

    internal void Draw(IUiCanvas canvas, UiTheme theme, UiRect viewport, string family)
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
        UiSize measured = canvas.MeasureText(message, 20 * scale, body.Width, family);
        float contentHeight = Math.Max(body.Height, measured.Height + 8 * scale);
        maximumScroll = Math.Max(0, contentHeight - body.Height);
        scroll = Math.Clamp(scroll, 0, maximumScroll);
        canvas.PushClip(body);
        try
        {
            canvas.Text(message, 20 * scale, new UiRect(body.X, body.Y - scroll, body.Width, contentHeight), theme.Foreground, family, UiAlignment.Start, UiTextFlow.Wrap);
        }
        finally
        {
            canvas.PopClip();
        }
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

    public void Dispose()
    {
        disposed = true;
        TaskCompletionSource<bool>? pending = completion;
        completion = null;
        cancellation.Dispose();
        pending?.TrySetCanceled();
    }
}
