using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleActor32CaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        if (graphics.OriginalEffect is not { } selected || selected.Binding.Effect != effect || graphics.PrimitiveFrame is null)
        {
            return 0;
        }
        HashSet<NativeBattlePrimitiveReference> owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        NativeBattleLogicalCommand[] commands = graphics.PrimitiveFrame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet))).Where(command => command.Opcode is >= 0x20 and < 0x80).ToArray();
        NativeBattleReachedCaptureAssertions.Moving(effect, snapshot, commands);
        foreach (NativeBattleLogicalCommand command in commands)
        {
            NativeBattlePilotVisualGeometry visual = command.PilotGeometry!;
            Assert.True(visual.Identity.Generation > 0);
            Assert.NotEqual(0, visual.Identity.ResourceGeneration);
            Assert.False(visual.ScreenSpace);
            Assert.True(visual.Continuous);
            Assert.True(visual.UseSourceDepths);
            Assert.NotNull(visual.SourceDepths);
        }
        Assert.Equal(commands.Length, commands.Select(command => command.PilotGeometry!.Identity).Distinct().Count());
        return commands.Length;
    }
}
