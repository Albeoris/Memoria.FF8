using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Presentation-only state for one immutable battle-owned Slots deal.</summary>
public sealed class SlotsCardController
{
    private static readonly int[] Cells = [4, 3, 5, 1, 7, 0, 8, 2, 6];
    private double elapsed;
    private double? selectedAt;
    private bool initialFlipSound;
    private bool begun;
    private bool confirmArmed;
    private bool pointerArmed;
    private (float X, float Y)? pointer;

    public SlotsCardController(int count, int revealMask, int? selectedIndex = null, bool restored = false)
    {
        if (count is not (3 or 5 or 7 or 9) || (revealMask & ~((1 << count) - 1)) != 0 || selectedIndex is int selected && (selected < 0 || selected >= count))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        Count = count;
        RevealMask = revealMask;
        SelectedIndex = selectedIndex;
        if (restored)
        {
            elapsed = EntranceDuration;
            initialFlipSound = true;
            if (selectedIndex.HasValue)
            {
                selectedAt = elapsed;
            }
        }
    }

    public int Count { get; }
    public int RevealMask { get; }
    public int FocusIndex { get; private set; }
    public int? SelectedIndex { get; private set; }
    public double EntranceDuration => 0.18 + (RevealMask == 0 ? 0 : 0.16);
    public bool Ready => elapsed >= EntranceDuration && !SelectedIndex.HasValue;
    public bool Finished => selectedAt is double start && elapsed - start >= SelectionDuration + 0.06;
    public float Spread => (float)(1 - Math.Pow(1 - Math.Clamp(elapsed / 0.18, 0, 1), 3));
    public float Opacity => selectedAt is double start ? (float)(1 - Math.Clamp((elapsed - start - SelectionDuration) / 0.06, 0, 1)) : 1;
    private double SelectionDuration => SelectedIndex is int selected && (RevealMask & (1 << selected)) == 0 ? 0.16 : 0.06;
    public event Action<TripleTriadSound>? SoundRequested;

    public void Begin()
    {
        if (begun)
        {
            return;
        }
        begun = true;
        SoundRequested?.Invoke(TripleTriadSound.Place);
    }

    public void Advance(TimeSpan duration, bool confirmHeld, bool mouseHeld)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
        bool entrance = !Ready && !SelectedIndex.HasValue;
        elapsed += duration.TotalSeconds;
        if (!initialFlipSound && elapsed >= 0.18 && RevealMask != 0)
        {
            initialFlipSound = true;
            SoundRequested?.Invoke(TripleTriadSound.Flip);
        }
        if (entrance && confirmHeld)
        {
            confirmArmed = false;
        }
        else if (!confirmHeld)
        {
            confirmArmed = true;
        }
        if (entrance && mouseHeld)
        {
            pointerArmed = false;
        }
        else if (!mouseHeld)
        {
            pointerArmed = true;
        }
    }

    public static int Cell(int index) => Cells[index];

    public void Navigate(MenuInput input)
    {
        if (!Ready)
        {
            return;
        }
        int step = input switch { MenuInput.Left => -1, MenuInput.Right => 1, MenuInput.Up => -3, MenuInput.Down => 3, _ => 0 };
        if (step == 0)
        {
            return;
        }
        int cell = Cell(FocusIndex);
        for (int traversal = 0; traversal < 9; traversal++)
        {
            cell = (cell + step + 9) % 9;
            int index = Array.IndexOf(Cells, cell, 0, Count);
            if (index >= 0)
            {
                Focus(index, true);
                return;
            }
        }
    }

    public bool CanConfirm(bool click) => Ready && (click ? pointerArmed : confirmArmed);

    public void Select(int index)
    {
        if (!Ready || index < 0 || index >= Count)
        {
            return;
        }
        FocusIndex = index;
        SelectedIndex = index;
        selectedAt = elapsed;
        SoundRequested?.Invoke(TripleTriadSound.Confirm);
        if ((RevealMask & (1 << index)) == 0)
        {
            SoundRequested?.Invoke(TripleTriadSound.Flip);
        }
    }

    public void Hover(float x, float y, int index)
    {
        bool moved = pointer is { } previous && (previous.X != x || previous.Y != y);
        pointer = (x, y);
        if (moved && Ready && index >= 0 && index < Count)
        {
            Focus(index, true);
        }
    }

    private void Focus(int index, bool sound)
    {
        if (FocusIndex == index)
        {
            return;
        }
        FocusIndex = index;
        if (sound)
        {
            SoundRequested?.Invoke(TripleTriadSound.Move);
        }
    }

    public (bool FaceUp, float Width) PresentedCard(int index)
    {
        if (SelectedIndex == index && selectedAt is double selected && (RevealMask & (1 << index)) == 0)
        {
            return Flip((elapsed - selected) / 0.16);
        }
        if ((RevealMask & (1 << index)) != 0 && elapsed >= 0.18)
        {
            return Flip((elapsed - 0.18) / 0.16);
        }
        return (false, 1);
    }

    private static (bool FaceUp, float Width) Flip(double progress)
    {
        float value = (float)Math.Clamp(progress, 0, 1);
        return (value >= 0.5f, Math.Max(0.015f, Math.Abs(1 - 2 * value)));
    }
}
