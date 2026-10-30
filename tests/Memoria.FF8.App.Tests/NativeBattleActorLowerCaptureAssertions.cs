using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleActorLowerCaptureAssertions
{
    internal static int AssertOwned(BattleSnapshot snapshot, ushort effect)
    {
        if (snapshot.NativeGraphics is not { OriginalEffect: { } original, PrimitiveFrame: { } frame } || original.Binding.Effect != effect)
        {
            return 0;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> owned = original.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
        int captured = 0;
        foreach (NativeBattleLogicalCommand command in frame.LastCommands.Select(item => item.Command).Where(command => owned.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x80))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.InRange(geometry.CornerCount, (byte)3, (byte)4);
            captured++;
        }
        return captured;
    }
}
