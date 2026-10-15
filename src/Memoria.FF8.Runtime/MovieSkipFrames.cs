namespace Memoria.FF8.Runtime;

/// <summary>The consecutive actual decoded samples admitted by an accelerated movie action.</summary>
public sealed record MovieSkipFrames(int FirstFrame, int Count, int TerminalFrame, double FramesPerSecond)
{
    public void Validate()
    {
        if (FirstFrame < 2 || Count < 0 || Count > 30000 || TerminalFrame < 0 || TerminalFrame > ushort.MaxValue || (long)FirstFrame + Count - 1 > TerminalFrame || !double.IsFinite(FramesPerSecond) || FramesPerSecond <= 0)
        {
            throw new InvalidDataException("Invalid accelerated movie sample range.");
        }
    }
}
