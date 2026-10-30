using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleCloudBombPresentationOwnership
{
    internal static void AssertFrame(BattleSnapshot snapshot, ushort effect)
    {
        if (effect is not (168 or 179 or 180) || snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect)
        {
            return;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> references = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
        foreach (NativeBattleLogicalCommand command in snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => references.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x40))
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            Assert.True(visual.Identity.ResourceGeneration != 0);
            Assert.True(visual.CornerCount is 3 or 4);
            for (int corner = 0; corner < visual.CornerCount; corner++)
            {
                System.Numerics.Vector3 point = visual.Corner(corner);
                Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z));
            }
        }
    }
}
