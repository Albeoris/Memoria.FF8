using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanNameContentTests
{
    [Fact]
    public void Canonical_encoded_name_owner_bypasses_pointer_resolver_and_copied_zero_is_overwritten_by_source_suffix()
    {
        NativeBattleScanTextFrame frame = Frame();
        string before = JsonSerializer.Serialize(frame);
        List<byte> selectors = [];
        NativeBattleScanTextExpansionServices services = Services([3, 0x34, 0x46, 2], operand =>
        {
            selectors.Add(operand);
            return ImmutableArray.Create<byte>(0x40, 0x41, 0, 0xA5);
        });
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(frame, 0x100, -1, services);
        Assert.Equal(new byte[] { 0x34 }, selectors);
        Assert.Equal(new byte[] { 0x40, 0x41, 0x46, 2 }, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.False(result.ExpandedContext128.Known[4]);
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Name_copy_precedes_prefix_limit_check_and_retains_copied_suffix_beyond_new_zero()
    {
        List<uint> reads = [];
        byte[] source = [3, 0x34, 0x46, 2];
        NativeBattleScanTextExpansionServices services = new(pointer =>
        {
            reads.Add(pointer);
            return source[(int)(pointer - 0x100)];
        }, _ => throw new InvalidOperationException("Canonical name bytes must not manufacture a native pointer."))
        {
            ReadNameBytes = _ => ImmutableArray.Create<byte>(0x40, 0x41, 0x42, 0)
        };
        NativeBattleScanTextFrame result = NativeBattleScanTextExpansion.Expand4B8B30(Frame(), 0x100, 1, services);
        Assert.Equal(new byte[] { 0x40, 0, 0x42, 0 }, result.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(new uint[] { 0x100, 0x101, 0x102 }, reads);
        Assert.Equal(4, result.ExpandedContext128.Known.Count(known => known));
    }

    [Fact]
    public void Empty_name_and_embedded_control_bytes_are_copied_as_encoded_content()
    {
        NativeBattleScanTextFrame empty = NativeBattleScanTextExpansion.Expand4B8B30(Frame(), 0x100, -1, Services([3, 0x30, 0x46, 0], _ => ImmutableArray.Create<byte>(0)));
        Assert.Equal(new byte[] { 0x46, 0 }, empty.ExpandedContext128.Bytes.AsSpan().Slice(0, 2).ToArray());
        NativeBattleScanTextFrame styled = NativeBattleScanTextExpansion.Expand4B8B30(Frame(), 0x100, -1, Services([3, 0x50, 2], _ => ImmutableArray.Create<byte>(6, 0x24, 0x40, 0)));
        Assert.Equal(new byte[] { 6, 0x24, 0x40, 2 }, styled.ExpandedContext128.Bytes.AsSpan().Slice(0, 4).ToArray());
    }

    [Fact]
    public void Unterminated_canonical_name_fails_without_inventing_a_zero_or_native_pointer()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextExpansion.Expand4B8B30(Frame(), 0x100, -1, Services([3, 0x34, 0], _ => ImmutableArray.Create<byte>(0x40, 0x41))));
    }

    private static NativeBattleScanTextExpansionServices Services(byte[] source, Func<byte, ImmutableArray<byte>> readName)
    {
        return new(pointer => source[checked((int)(pointer - 0x100))], _ => throw new InvalidOperationException("Canonical name bytes must not manufacture a native pointer.")) { ReadNameBytes = readName };
    }

    private static NativeBattleScanTextFrame Frame()
    {
        NativeBattleScanByteOwner output = new(Enumerable.Repeat((byte)0xA5, 128).ToImmutableArray(), new bool[128].ToImmutableArray());
        return new(new([], ImmutableDictionary<byte, NativeBattleScanByteOwner>.Empty), output, 0, 0, new(new byte[12].ToImmutableArray(), new bool[12].ToImmutableArray()));
    }
}
