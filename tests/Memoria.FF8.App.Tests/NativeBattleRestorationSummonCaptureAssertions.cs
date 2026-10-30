using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRestorationSummonCaptureAssertions
{
    internal static int AssertOwned(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } frame)
        {
            return 0;
        }
        // References come from actual accepted native appends, independently of whether the producer captured geometry.
        var owned = selected.PresentationPackets.ToHashSet();
        int captured = 0;
        foreach (NativeBattleLogicalCommand command in frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(command.Packet) && command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.InRange(geometry.CornerCount, 3, 4);
            captured++;
        }
        return captured;
    }
}
