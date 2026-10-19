using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112HierarchyTests
{
    [Theory]
    [InlineData(0x80000030u, 4096, 0, true)]
    [InlineData(0x30u, 0, 0, true)]
    [InlineData(0x30u, 4096, 4096, true)]
    [InlineData(0x30u, 4096, 4095, false)]
    [InlineData(0x30u, 4096, -32768, false)]
    public void OriginalSkipGuardsDoNotConsumeUnneededShapeCameraOrCallerStackOperands(uint flags, short scale, short fade, bool skipped)
    {
        byte[] raw = new byte[checked((int)(NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address))];
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x200), flags);
        NativeBattleDuel112Resources resources = new(raw.ToImmutableArray(), [], [], []);
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel112 = new(true, parameters, resources) };
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0);
        NativeBattleDuel112Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));
        byte[] data = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24), scale);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), fade);
        NativeBattleGfAnimationNode node = new(0, 0, data.ToImmutableArray());
        NativeBattleDuel112HierarchyStep Run() => NativeBattleDuel112Hierarchy.Draw6CD280(frame, new(0, []), node, new(NativeBattleDuel112Resources.Address + 0x200, 0, null, null, null), null!, null!, null!);
        if (skipped)
        {
            NativeBattleDuel112HierarchyStep result = Run();
            Assert.Same(frame, result.Frame);
            Assert.Empty(result.Batch.Attempts);
            Assert.Equal(0, result.Batch.AllocationBytes);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => Run());
        }
    }
}
