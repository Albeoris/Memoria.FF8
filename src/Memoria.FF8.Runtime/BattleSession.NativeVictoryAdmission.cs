namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasPendingNativeVictory => DebugOutcome is null && NativeGraphics is { NativeOutcome.ResultByte6E7: 4 };

    private void BeginNativeVictory486500()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.NativeOutcome?.ResultByte6E7 is > 0)
        {
            return;
        }
        bool skipAnimation = (ResolvedNativeFlags & 2) != 0;
        NativeBattleOutcomeState state = graphics.NativeOutcome ?? new();
        graphics = graphics with { NativeOutcome = state with { ResultByte6E7 = 4, VictoryQueuedDFE = 1, FadeStyleE01 = skipAnimation ? (byte)1 : (byte)0 } };
        graphics = QueueNativeDescriptor(graphics, 112, 0);
        graphics = QueueNativeDescriptor(graphics, 10, 0x47DF60);
        //TODO(TD-009): Physical494D40 result banks remain unowned. The proved reward projection consumes these frozen combat facts once at host return.
        graphics = QueueNativeDescriptor(graphics, skipAnimation ? (ushort)109 : (ushort)115, 0);
        NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x47DFC0);
    }
}
