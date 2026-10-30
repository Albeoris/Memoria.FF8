using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRecoveryCaptureAssertions
{
    internal static int AssertOwnedGeometry(BattleSnapshot snapshot, ushort effect)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        if (graphics.OriginalEffect is not { } selected || selected.Binding.Effect != effect)
        {
            return 0;
        }
        int count = 0;
        foreach (NativeBattleLogicalResolvedCommand resolved in graphics.PrimitiveFrame!.LastCommands)
        {
            NativeBattleLogicalCommand command = resolved.Command;
            // This family emits into its own physical primary packet arena. E1/E2 state and fixed screen rectangles remain discrete.
            if (command.Packet.Bank != "20DFAB8" || command.Opcode < 0x20 || command.Opcode >= 0x60)
            {
                continue;
            }
            Assert.NotNull(command.PilotGeometry);
            Assert.Equal(effect, command.PilotGeometry!.Identity.Effect);
            Assert.InRange(command.PilotGeometry.CornerCount, 3, 4);
            Assert.True(command.PilotGeometry.Identity.Generation > 0);
            count++;
        }
        return count;
    }
}
