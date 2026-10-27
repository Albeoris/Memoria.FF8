using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175DrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 512, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0x5D3A50u, 0, 4096, 2)]
    [InlineData(0x5D3A50u, 7, 512, 2)]
    [InlineData(0x5D3A50u, 8, -1, 2)]
    [InlineData(0x5D3CE0u, 7, -1, 2)]
    [InlineData(0x5D3CE0u, 8, 0, 2)]
    [InlineData(0x5D3CE0u, 9, 512, 2)]
    [InlineData(0x5D3E20u, 7, 512, 2)]
    [InlineData(0x5D3E20u, 8, -1, 2)]
    [InlineData(0x5D3E20u, 16, 0, 2)]
    [InlineData(0x5D3E20u, 17, 409, 2)]
    [InlineData(0x5D43B0u, 0, 4096, 14)]
    [InlineData(0x5D43B0u, 4, -1, 14)]
    [InlineData(0x5D43B0u, 18, 0, 14)]
    [InlineData(0x5D43B0u, 19, 682, 14)]
    public void Standard_mesh_fade_branches_keep_actual_zero_fade_and_native_depth_shift(uint callback, ushort counter, int fade, byte shift)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(0xC, counter);
        NativeBattleHoly175MeshDraw mesh = NativeBattleHoly175Drawing.DescribeStandardMesh(task, NativeBattleHoly175State.FromColdProcess(), Resources(), View, Projection);
        Assert.Equal(fade < 0 ? null : (int?)fade, mesh.Context.Fade);
        Assert.Equal(fade < 0 ? 0x33u : 0xF3u, mesh.Context.Flags);
        Assert.Equal(shift, mesh.Context.DepthShift);
        Assert.Equal(17, mesh.Context.TableBaseBucket);
        Assert.Equal(0u, mesh.Context.FarColor);
    }

    [Fact]
    public void Screen_anchor_writes_only_diagonal_and_translation_and_uses_signed_truncation_with_literal_center()
    {
        byte[] raw = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess() with { StaticMatrix = NativeBattleEffectArena.Clear().Write(0, raw) };
        NativeBattleActionMatrix view = View with { Z = 600 };
        NativeBattleCameraProjection projection = new(view, 512, 0, 0);
        (NativeBattleHoly175State next, NativeBattleActionMatrix matrix) = NativeBattleHoly175Drawing.ScreenAnchor5D41F0(NativeBattleTestSnapshots.Independent(state), new(-1, -1, 0), 256, view, projection);
        Assert.Equal((-160, -108, 512), (matrix.X, matrix.Y, matrix.Z));
        byte[] changed = next.StaticMatrix.Read(0, 32);
        for (int index = 0; index < 20; index++)
        {
            bool diagonal = index is 0 or 1 or 8 or 9 or 16 or 17;
            if (!diagonal)
            {
                Assert.Equal(raw[index], changed[index]);
            }
        }
        Assert.Equal((short)256, matrix.Rotation[0]);
        Assert.Equal((short)256, matrix.Rotation[4]);
        Assert.Equal((short)256, matrix.Rotation[8]);
        Assert.Equal(raw, state.StaticMatrix.Read(0, 32));
        NativeBattleHoly175State unknown = state with { StaticMatrix = state.StaticMatrix.MarkUnknown(2, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175Drawing.ScreenAnchor5D41F0(unknown, default, 256, view, projection));
    }

    [Fact]
    public void Flare_reloads_its_two_diagonal_overrides_and_billboard_mesh_uses_rotation_without_extra_roll()
    {
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess();
        NativeBattleModulePoolTask flare = Task(0x5D43B0).WriteWord(0x1C, 512);
        NativeBattleHoly175MeshDraw loaded = NativeBattleHoly175Drawing.DescribeStandardMesh(flare, state, Resources(), View, Projection);
        Assert.Equal(new short[] { 512, 0, 0, 0, 4096, 0, 0, 0, 512 }, loaded.Matrix.Rotation.ToArray());
        Assert.Equal(loaded.Matrix.Rotation.ToArray(), NativeBattleHoly175State.LoadMatrix(loaded.State.StaticMatrix, 0).Rotation.ToArray());
        NativeBattleModulePoolTask billboard = Task(0x5D42B0).WriteWord(0x18, 1024);
        NativeBattleHoly175MeshDraw oriented = NativeBattleHoly175Drawing.DescribeStandardMesh(billboard, state, Resources(), View, Projection);
        Assert.Equal((byte)13, oriented.Context.DepthShift);
        Assert.Equal(0xD52DD8u, oriented.Resource.Address);
        Assert.Equal((10, -20, 10030), (oriented.Matrix.X, oriented.Matrix.Y, oriented.Matrix.Z));
        Assert.Equal(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }, oriented.Matrix.Rotation.ToArray());
    }

    [Fact]
    public void Orbit_uses_selected_driver_rotation_and_low_WORD_translation_before_actual_billboard_and_roll()
    {
        NativeBattleActionMatrix driver = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 65535, 65530, 0, 4096);
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess().WriteMatrix(1, driver);
        NativeBattleModulePoolTask task = Task(0x5D3590).WriteWord(0xE, 1).WriteWord(0x16, 500).WriteWord(0x20, 500).WriteWord(0x18, 0).WriteWord(0x1C, 512);
        NativeBattleHoly175DrawingResult actual = NativeBattleHoly175Drawing.DrawTask(task, state, Resources(), View, Projection, 123, default, Colors);
        NativeBattleModuleResource resource = new(0, 0xD523E8, 0, 0, Sprite(0xD523E8).Bytes);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Anchor = new(-1, 994, 0, 0), Scale = 512, DepthOffset = -32, SpriteFlags = 0 };
        NativeBattleModulePacketBatch expected = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection, 123, 17, 2);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual.Sprites));
        Assert.False(actual.Common);
        Assert.Same(state, actual.State);
    }

    [Theory]
    [InlineData(0, 0x2C000000u)]
    [InlineData(1, 0x2C404000u)]
    [InlineData(2, 0x2C808080u)]
    [InlineData(16, 0x2C808000u)]
    [InlineData(23, 0x2C101000u)]
    public void Rays_preserve_native_zero_red_override_and_mutate_shared_static_matrix_in_eight_record_order(ushort phase, uint command)
    {
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess();
        NativeBattleModulePoolTask task = Task(0x5D3FB0).WriteWord(0xC, phase);
        NativeBattleHoly175DrawingResult actual = NativeBattleHoly175Drawing.DrawTask(task, state, Resources(), View, Projection, 0, new(100, -200, -1000), Colors);
        Assert.False(actual.Common);
        Assert.Equal(8, actual.Sprites!.Primitives.Length);
        Assert.All(actual.Sprites.Primitives, primitive => Assert.Equal(command, primitive.Primitive.Words[1]));
        Assert.Equal(240, NativeBattleHoly175State.LoadMatrix(actual.State.StaticMatrix, 0).Rotation[0]);
        Assert.Equal(320, actual.Sprites.AllocationBytes);
        NativeBattleHoly175State cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleHoly175State>(NativeBattleTestSnapshots.Json(state))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(actual), NativeBattleTestSnapshots.Json(NativeBattleHoly175Drawing.DrawTask(task, cold, Resources(), View, Projection, 0, new(100, -200, -1000), Colors)));
    }

    [Fact]
    public void Delayed_mesh_skips_all_draws_and_unowned_callback_or_ray_rows_fail_fast()
    {
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess();
        NativeBattleHoly175DrawingResult delayed = NativeBattleHoly175Drawing.DrawTask(Task(0x5D3E20).WriteWord(0xE, 1), state, Resources(), View, Projection, 0, default, Colors);
        Assert.Empty(delayed.Geometry!.Attempts);
        Assert.Equal(0, delayed.Geometry.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175Drawing.DrawTask(Task(123), state, Resources(), View, Projection, 0, default, Colors));
        Assert.Throws<NotSupportedException>(() => NativeBattleHoly175Drawing.DrawTask(Task(0x5D3FB0), state, Resources() with { RayRows = null }, View, Projection, 0, default, Colors));
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x24 - 12].ToImmutableArray()).WriteWord(0x10, 10).WriteWord(0x12, unchecked((ushort)-20)).WriteWord(0x14, 30).WriteWord(0x1C, 4096);
    private static NativeBattleHoly175Resources Resources()
    {
        uint[] addresses = [0xD54588, 0xD53BA0, 0xD53688, 0xD52DD8, 0xD527F8];
        byte[] rows = new byte[64];
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 8 + 2), (short)(128 + 16 * index));
        }
        return new(new("animation", []), new("tim", []), [Sprite(0xD522CC), Sprite(0xD523E8)], addresses.Select(address => new NativeBattleStaticMeshResource(0, address, [])).ToImmutableArray(), new("sound", [0, 0, 0, 0])) { RayRows = new("rows", rows.ToImmutableArray()) };
    }
    private static NativeBattleHoly175SpriteResource Sprite(uint address)
    {
        byte[] bytes = new byte[56];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 8);
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), 32);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), -7);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 1);
        bytes[36] = 4;
        bytes[37] = 128;
        bytes[38] = 5;
        bytes[39] = 0x2C;
        return new(address, bytes.ToImmutableArray());
    }
}
