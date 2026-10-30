using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleCinematicCaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics is not { OriginalEffect: { } selected, PrimitiveFrame: { } frame } || selected.Binding.Effect != effect)
        {
            return 0;
        }
        HashSet<NativeBattlePrimitiveReference> owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        NativeBattleLogicalCommand[] commands = frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80).ToArray();
        AssertCommands(snapshot, effect, commands);
        return commands.Length;
    }

    internal static void AssertCommands(BattleSnapshot snapshot, ushort effect, IEnumerable<NativeBattleLogicalCommand> commands)
    {
        foreach (NativeBattleLogicalCommand command in commands.Where(command => command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.True(visual.Identity.Effect == effect, $"Effect{effect} callback{snapshot.NativeGraphics!.Callback}, packet{command.Packet}, opcode{command.Opcode:X2}, identity{visual.Identity}.");
            Assert.True(visual.Identity.Generation > 0);
            Assert.InRange(visual.CornerCount, 3, 4);
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
            }
        }
    }
}
