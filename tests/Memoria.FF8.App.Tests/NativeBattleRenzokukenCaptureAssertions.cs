using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRenzokukenCaptureAssertions
{
    internal static int AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } frame)
        {
            return 0;
        }
        var owned = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToHashSet();
        int captured = 0;
        foreach (NativeBattleLogicalCommand command in frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.True(geometry.Identity.ResourceGeneration > 0);
            Assert.InRange(geometry.CornerCount, (byte)3, (byte)4);
            for (int corner = 0; corner < geometry.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = geometry.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
            captured++;
        }
        return captured;
    }
}
