namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicModel
{
    /// <summary>Decodes an owned animation pose without callback clocks, drawing, sound or random sampling.</summary>
    public static NativeBattleWaterExecutionFrame DecodePose(NativeBattleWaterExecutionFrame frame, uint model, byte animation, int animationFrame, NativeBattleCinematicModelServices services)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(animationFrame);
        frame = Select509440(frame, model, animation, services);
        for (int index = 1; index < animationFrame; index++)
        {
            bool complete;
            (frame, complete) = AdvanceRegular(frame, model, services);
            if (complete)
            {
                throw new InvalidDataException("Cinematic continuation pose exceeds its selected original animation.");
            }
        }
        return frame;
    }
}
