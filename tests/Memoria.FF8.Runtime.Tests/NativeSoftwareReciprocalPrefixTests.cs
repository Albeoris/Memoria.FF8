using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareReciprocalPrefixTests
{
    [Theory]
    [InlineData(-28, 0, 0)]
    [InlineData(-19, 0, 36)]
    [InlineData(-18, 1, 0)]
    [InlineData(-9, 1, 36)]
    [InlineData(-8, 2, 0)]
    [InlineData(-1, 2, 28)]
    public void Negative_height_reads_exact_current_record_prefix_bits(int height, int record, int offset)
    {
        NativeSoftwareScanlineEdges owners = Owners();
        owners = Store(owners, record, offset, -0.375f);
        float value = NativeSoftwareReciprocalTable.Initialize463280().AtHeight(height, owners);
        Assert.Equal(BitConverter.SingleToInt32Bits(-0.375f), BitConverter.SingleToInt32Bits(value));
    }

    [Fact]
    public void Prefix_alias_is_read_from_the_current_edge_snapshot_without_caching_or_division()
    {
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        NativeSoftwareScanlineEdges first = Store(Owners(), 2, 28, 0.25f);
        NativeSoftwareScanlineEdges second = Store(first, 2, 28, 0.75f);
        Assert.Equal(0.25f, table.AtHeight(-1, first));
        Assert.Equal(0.75f, table.AtHeight(-1, second));
        Assert.Equal(0.25f, table.AtHeight(-1, first));
        Assert.NotEqual(-1f, table.AtHeight(-1, first));
        Assert.Equal(table.AtHeight(1), table.AtHeight(1, second));
    }

    [Fact]
    public void Missing_prefix_and_address_before_its_start_fail_without_synthesizing_a_reciprocal()
    {
        NativeSoftwareReciprocalTable table = NativeSoftwareReciprocalTable.Initialize463280();
        Assert.Throws<NotSupportedException>(() => table.AtHeight(-1));
        Assert.Throws<NotSupportedException>(() => table.AtHeight(-29, Owners()));
    }

    [Theory]
    [InlineData(0x7F800000)]
    [InlineData(unchecked((int)0xFF800000))]
    [InlineData(0x7FC00001)]
    public void Nonfinite_float_bits_in_the_physical_prefix_fail_at_consumption(int bits)
    {
        NativeSoftwareScanlineEdges owners = Store(Owners(), 2, 28, BitConverter.Int32BitsToSingle(bits));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareReciprocalTable.Initialize463280().AtHeight(-1, owners));
    }

    [Fact]
    public void Negative_textured_setup_uses_actual_prefix_for_all_slopes_and_retains_other_fields()
    {
        NativeSoftwareScanlineEdges owners = Store(Owners(), 2, 28, 0.25f);
        NativeSoftwareEdgeBuffers result = NativeSoftwareRasterEdges.SetupTextured4632C0(owners.Left, new(10, 7, 20, 30), new(18, 6, 32, 10), NativeSoftwareReciprocalTable.Initialize463280(), owners);
        Assert.Equal(2, Read(result.Step, 0));
        Assert.Equal(3, Read(result.Step, 12));
        Assert.Equal(-5, Read(result.Step, 16));
        Assert.Equal(10, Read(result.Current, 0));
        Assert.Equal(20, Read(result.Current, 12));
        Assert.Equal(30, Read(result.Current, 16));
        Assert.Equal(owners.Left.Current.AsSpan().Slice(20).ToArray(), result.Current.AsSpan().Slice(20).ToArray());
        Assert.Equal(owners.Left.Step.AsSpan().Slice(20).ToArray(), result.Step.AsSpan().Slice(20).ToArray());
        Assert.Equal(BitConverter.SingleToInt32Bits(0.25f), Read(owners.Right.Step, 28));
    }

    [Fact]
    public void Negative_untextured_setup_reads_updated_prefix_before_writing_its_X_fields()
    {
        NativeSoftwareScanlineEdges owners = Store(Owners(), 0, 0, 0.5f);
        NativeSoftwareEdgeBuffers result = NativeSoftwareRasterEdges.SetupUntextured463370(owners.Left, new(7, 28, 100, 200), new(19, 0, 300, 400), NativeSoftwareReciprocalTable.Initialize463280(), owners);
        Assert.Equal(6, Read(result.Step, 0));
        Assert.Equal(7, Read(result.Current, 0));
        Assert.Equal(owners.Left.Current.AsSpan().Slice(4).ToArray(), result.Current.AsSpan().Slice(4).ToArray());
        Assert.Equal(owners.Left.Step.AsSpan().Slice(4).ToArray(), result.Step.AsSpan().Slice(4).ToArray());
        Assert.Equal(BitConverter.SingleToInt32Bits(0.5f), Read(owners.Left.Current, 0));
    }

    [Fact]
    public void Older_twenty_byte_right_step_does_not_supply_the_unrepresented_prefix_tail()
    {
        NativeSoftwareScanlineEdges owners = Owners();
        owners = owners with { Right = owners.Right with { Step = new byte[20].ToImmutableArray() } };
        Assert.Throws<NotSupportedException>(() => NativeSoftwareReciprocalTable.Initialize463280().AtHeight(-1, owners));
        Assert.Equal(0f, NativeSoftwareReciprocalTable.Initialize463280().AtHeight(-8, owners));
    }

    [Fact]
    public void Initialized_renderer_owns_zeroed_right_step_prefix_without_expanding_left_step()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        Assert.Equal(20, state.Raster.Edges.Left.Step.Length);
        Assert.Equal(32, state.Raster.Edges.Right.Step.Length);
        Assert.All(state.Raster.Edges.Right.Step, value => Assert.Equal((byte)0, value));
        Assert.Equal(0f, state.Reciprocals.AtHeight(-1, state.Raster.Edges));
    }
    private static NativeSoftwareScanlineEdges Owners()
    {
        NativeSoftwareEdgeBuffers left = new(new byte[40].ToImmutableArray(), new byte[20].ToImmutableArray());
        NativeSoftwareEdgeBuffers right = new(new byte[40].ToImmutableArray(), new byte[32].ToImmutableArray());
        return new(left, right);
    }

    private static NativeSoftwareScanlineEdges Store(NativeSoftwareScanlineEdges owners, int record, int offset, float value)
    {
        ImmutableArray<byte> original = record switch
        {
            0 => owners.Left.Current,
            1 => owners.Right.Current,
            2 => owners.Right.Step,
            _ => throw new ArgumentOutOfRangeException(nameof(record))
        };
        byte[] bytes = original.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));
        ImmutableArray<byte> updated = bytes.ToImmutableArray();
        return record switch
        {
            0 => owners with { Left = owners.Left with { Current = updated } },
            1 => owners with { Right = owners.Right with { Current = updated } },
            2 => owners with { Right = owners.Right with { Step = updated } },
            _ => throw new ArgumentOutOfRangeException(nameof(record))
        };
    }

    private static int Read(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
