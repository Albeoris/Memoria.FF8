using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleChocoPresentationOwnership(ushort effect)
{
    private int frames;
    private int triangles;
    private readonly HashSet<(uint Bank, int Slot, long Generation)> owners = [];

    internal void Accept(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { } selected || selected.Binding.Effect != effect || snapshot.NativeGraphics.PrimitiveFrame is not { } frame)
        {
            return;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> references = selected.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
        NativeBattleLogicalCommand[] owned = frame.LastCommands.Select(item => item.Command).Where(command => references.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && command.Opcode is >= 0x20 and < 0x40).ToArray();
        foreach (NativeBattleLogicalCommand command in owned)
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(effect, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            Assert.True(visual.CornerCount is 3 or 4);
            owners.Add((visual.Identity.Bank, visual.Identity.Slot, visual.Identity.Generation));
            triangles++;
        }
        if (owned.Length > 0)
        {
            frames++;
        }
    }

    internal void Complete()
    {
        Assert.True(frames > 1, "The complete original component must reach captured geometry on multiple callbacks.");
        Assert.True(triangles > 1);
        Assert.True(owners.Count > 1, "The original component must reach multiple physical producer owners.");
    }
}
