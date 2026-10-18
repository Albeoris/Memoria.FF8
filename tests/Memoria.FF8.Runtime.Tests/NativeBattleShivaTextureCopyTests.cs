using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaTextureCopyTests
{
    [Fact]
    public void Ranked_physical_page_and_frame_offsets_copy_into_the_fixed_tile_in_the_original_direction()
    {
        var fixture = Fixture();
        NativeBattleEffectVramCopy copy = NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model, 1, 0, fixture.Read)!;
        Assert.Equal(new NativeBattleEffectVramCopy(788, 405, 2, 1, 773, 390), copy);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(copy.SourceX, copy.SourceY, 2, 1, [0x34, 0x12, 0x78, 0x56]);
        textures = textures.Upload45BD30(copy.DestinationX, copy.DestinationY, 2, 1, [0xAA, 0xBB, 0xCC, 0xDD]);
        textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        NativeBattleEffectTextureDrain drained = textures.Drain505D20();
        Assert.True(Assert.Single(drained.Uploads).Copied);
        byte[] vram = drained.State.ReadVram();
        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, vram.AsSpan(2 * (copy.DestinationY * 1024 + copy.DestinationX), 4).ToArray());
        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, vram.AsSpan(2 * (copy.SourceY * 1024 + copy.SourceX), 4).ToArray());
    }

    [Fact]
    public void Current_control_and_mutated_resource_bytes_are_read_instead_of_cached_fixed_metadata()
    {
        var fixture = Fixture();
        NativeBattleEffectVramCopy first = NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model, 1, 1, fixture.Read)!;
        Assert.Equal((short)798, first.SourceX);
        fixture.Bytes[8 + 5 + 2] = 47;
        byte[] control = fixture.Model.Control.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), (1 << 0) | (1 << 7));
        NativeBattleEffectVramCopy current = NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model with { Control = control.ToImmutableArray() }, 1, 1, fixture.Read)!;
        Assert.Equal((short)687, current.SourceX);
        Assert.Equal((short)415, current.SourceY);
        Assert.Equal((short)645, current.DestinationX);
        Assert.Equal((short)390, current.DestinationY);
    }

    [Fact]
    public void Null_directory_or_zero_row_offset_does_not_read_any_unused_metadata()
    {
        var fixture = Fixture();
        byte[] control = fixture.Model.Control.ToArray();
        control.AsSpan(0x30, 4).Clear();
        Assert.Null(NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model with { Control = control.ToImmutableArray() }, 1, 0, (_, _) => throw new InvalidOperationException("Null directory has no reads.")));
        List<uint> reads = [];
        fixture.Bytes[1] = 0;
        Assert.Null(NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model, 1, 0, (address, length) =>
        {
            reads.Add(address);
            return fixture.Read(address, length);
        }));
        Assert.Equal(new uint[] { 0x1001 }, reads);
    }

    [Fact]
    public void Invalid_rank_fails_without_inventing_a_texture_page_or_consuming_frame_metadata()
    {
        var fixture = Fixture();
        fixture.Bytes[8] = 2;
        List<uint> reads = [];
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaTextureCopy.Resolve5C4F80(fixture.Model, 1, 0, (address, length) =>
        {
            reads.Add(address);
            return fixture.Read(address, length);
        }));
        Assert.Equal(new uint[] { 0x1001, 0x1008 }, reads);
    }

    private static (NativeBattleCactuarRawModelState Model, byte[] Bytes, Func<uint, int, byte[]> Read) Fixture()
    {
        byte[] model = new byte[0x90];
        byte[] control = new byte[0x34];
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x84), NativeBattleShivaModelTask.Binding.ControlAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x30), 0x1000);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), (1 << 0) | (1 << 3));
        byte[] bytes = new byte[32];
        bytes[1] = 8;
        new byte[] { 1, 5, 6, 2, 1, 20, 21, 30, 31 }.CopyTo(bytes, 8);
        NativeBattleCactuarRawModelState state = new(model.ToImmutableArray(), control.ToImmutableArray(), null, null, null, null);
        return (state, bytes, (address, length) => bytes.AsSpan(checked((int)(address - 0x1000)), length).ToArray());
    }
}
