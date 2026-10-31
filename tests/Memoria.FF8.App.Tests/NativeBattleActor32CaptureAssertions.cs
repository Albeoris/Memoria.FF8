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
        AssertPhysicalIdentities(commands);
        return commands.Length;
    }

    internal static void AssertPhysicalIdentities(IEnumerable<NativeBattleLogicalCommand> commands)
    {
        NativeBattleLogicalCommand[] physical = commands.GroupBy(command => (Packet: NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet), command.WordOffset)).Select(group =>
        {
            NativeBattleLogicalCommand first = group.First();
            foreach (NativeBattleLogicalCommand replay in group)
            {
                Assert.Equal(first.PilotGeometry!.Identity, replay.PilotGeometry!.Identity);
                Assert.Equal(first.PilotGeometry, replay.PilotGeometry);
                Assert.Equal(first.Opcode, replay.Opcode);
                Assert.Equal(first.Words.ToArray(), replay.Words.ToArray());
                Assert.Equal(first.VertexPayload4.ToArray(), replay.VertexPayload4.ToArray());
            }
            return first;
        }).ToArray();
        Assert.Equal(physical.Length, physical.Select(command => command.PilotGeometry!.Identity).Distinct().Count());
    }

}
