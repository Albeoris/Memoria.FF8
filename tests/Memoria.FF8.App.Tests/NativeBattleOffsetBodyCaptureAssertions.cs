using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleOffsetBodyCaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (effect is not (290 or 298 or 308) || snapshot.NativeGraphics is not { OriginalEffect: { } selected, PrimitiveFrame: { } frame } || selected.Binding.Effect != effect)
        {
            return 0;
        }
        HashSet<NativeBattlePrimitiveReference> owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        NativeBattleLogicalCommand[] commands = frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80).ToArray();
        NativeBattleReachedCaptureAssertions.Moving(effect, snapshot, commands);
        int moving = 0;
        foreach (NativeBattleLogicalCommand command in commands)
        {
            NativeBattlePilotVisualGeometry visual = command.PilotGeometry!;
            Assert.True(visual.Identity.Generation > 0);
            Assert.NotEqual(0, visual.Identity.ResourceGeneration);
            Assert.InRange(visual.CornerCount, 3, 4);
            Assert.True(visual.UseSourceDepths);
            System.Numerics.Vector4 depths = visual.SourceDepths ?? throw new InvalidDataException("Owned offset-body capture requires its original accepted depth payload.");
            Assert.Equal(4, command.VertexPayload4.Length);
            Assert.Equal((float)command.VertexPayload4[0], depths.X);
            Assert.Equal((float)command.VertexPayload4[1], depths.Y);
            Assert.Equal((float)command.VertexPayload4[2], depths.Z);
            if (visual.CornerCount == 4)
            {
                Assert.Equal((float)command.VertexPayload4[3], depths.W);
            }
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
            }
            else
            {
                Assert.True(visual.Continuous);
                moving++;
            }
        }
        return moving;
    }
}
