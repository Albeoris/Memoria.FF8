namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void BeginNativeDefeat486450()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.NativeOutcome?.ResultByte6E7 is > 0)
        {
            return;
        }
        if (definition.NativeGlobalMessages.Length == 0 || definition.NativeGlobalMessages[0] is not { } message)
        {
            throw new NotSupportedException("Native486450 requires its actual47EC70 miscellaneous text0 binding.");
        }
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native486450 requires its saved message-speed BYTE.")) + 8;
        NativeBattleOutcomeState state = graphics.NativeOutcome ?? new();
        graphics = graphics with { NativeOutcome = state with { ResultByte6E7 = 1, VictoryQueuedDFE = 1, FadeStyleE01 = 3 } };
        graphics = QueueNativeDescriptor(graphics, 112, 0);
        graphics = QueueNativeDescriptor(graphics, 10, 0x47DF60);
        graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", null, null, duration, message) { NativeMessageMode = 3 }, 86);
        NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x47DFC0);
    }
}
