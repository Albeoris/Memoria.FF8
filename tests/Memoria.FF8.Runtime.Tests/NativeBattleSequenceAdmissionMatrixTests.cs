using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceAdmissionMatrixTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(16)]
    [InlineData(30)]
    public void Every_byte_selector_preserves_control_for_missing_entries_and_selects_present_directory_boundaries(int count)
    {
        byte[] bytes = new byte[(count + 1) * 2 + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)count));
        ushort start = checked((ushort)((count + 1) * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), start);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(count * 2), start);
        bytes[start] = 0;
        bytes[start + 1] = 0xA2;
        NativeBattleGraphicsResource resource = new(bytes.ToImmutableArray()) { AnimationFrames = [1] };
        foreach (int actor in new[] { 0, 3, 6 })
        {
            foreach (ushort flags in new ushort[] { 0x40, 0x100, 0x140 })
            {
                NativeBattleGraphicsModel source = new(actor, 2)
                {
                    ModelId4 = actor == 0 ? (byte)4 : (byte)84,
                    Flags8 = 0,
                    ResourceFlags44 = flags,
                    Sequence = 1,
                    SequenceFrame = 23,
                    SequenceState = new() { Pending3 = 7, Offset46 = 19, Callback42 = 11 }
                };
                Assert.True(NativeBattleSequenceSelections.CanSelectReady505CE0(source));
                for (int selector = 1; selector <= byte.MaxValue; selector++)
                {
                    NativeBattleGraphicsModel selected = NativeBattleSequenceSelections.SelectResource505BC0(source, resource, selector);
                    if (selector == 1 || selector == count)
                    {
                        Assert.Equal(source with { Sequence = checked((byte)selector), SequenceFrame = 0, ResourceFlags44 = 1, SequenceState = source.SequenceState! with { Pending3 = 0, Offset46 = 0, Callback42 = 0 } }, selected);
                    }
                    else
                    {
                        Assert.Equal(source with { ResourceFlags44 = (ushort)(flags | 0x40) }, selected);
                        Assert.Same(source.SequenceState, selected.SequenceState);
                    }
                }
            }
        }
    }
}
