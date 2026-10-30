using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleDuel28CaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (effect is not (212 or 215 or 216) || snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect)
        {
            return 0;
        }
        var owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        int count = 0;
        foreach (NativeBattleLogicalCommand command in snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            Assert.True(visual.Identity.ResourceGeneration != 0 || visual.ScreenSpace);
            Assert.InRange(visual.CornerCount, (byte)3, (byte)4);
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
            }
            for (int corner = 0; corner < visual.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = visual.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
            count++;
        }
        return count;
    }
}
