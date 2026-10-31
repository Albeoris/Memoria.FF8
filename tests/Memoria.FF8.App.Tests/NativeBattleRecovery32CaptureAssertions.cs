using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRecovery32CaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (effect is not (283 or 301 or 303 or 314 or 315) || snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect)
        {
            return 0;
        }
        var owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        int count = 0;
        foreach (NativeBattleLogicalCommand command in snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.True(geometry.Identity.ResourceGeneration != 0 || geometry.ScreenSpace);
            Assert.InRange(geometry.CornerCount, (byte)3, (byte)4);
            Assert.True(geometry.UseSourceDepths);
            Assert.NotNull(geometry.SourceDepths);
            if (geometry.ScreenSpace)
            {
                Assert.False(geometry.Continuous);
            }
            else
            {
                Assert.True(geometry.Continuous);
            }
            for (int corner = 0; corner < geometry.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = geometry.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
            count++;
        }
        return count;
    }
}
