using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Tracks a timed system notice and renders messages in the shared notice window.</summary>
internal sealed class NoticeWindow
{
    public const double DefaultDurationSeconds = 5;
    public const double QuickSaveDurationSeconds = 1;

    private Func<string>? message;
    private UiView? view;
    private string? viewText;

    public double RemainingSeconds { get; private set; }
    public string? Text => RemainingSeconds > 0 ? message?.Invoke() : null;

    public void Show(Func<string> text, double durationSeconds = DefaultDurationSeconds)
    {
        message = text;
        RemainingSeconds = durationSeconds;
    }

    public void Advance(TimeSpan elapsed)
    {
        RemainingSeconds = Math.Max(0, RemainingSeconds - elapsed.TotalSeconds);
    }

    public void Clear()
    {
        message = null;
        RemainingSeconds = 0;
    }

    public void Draw(IUiCanvas canvas, UiTheme theme, float width, float height, string text)
    {
        if (view is null || viewText != text)
        {
            view = new UiView(new NoticeElement(text));
            viewText = text;
        }
        view.Resize(width, height);
        view.Draw(canvas, theme);
    }
}
