using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshRawModelTests
{
    [Fact]
    public void Explicit_control_binding_reuses_current_cursor_and_preserves_unwritten_model_and_control_bytes()
    {
        using GameAssets game = Installed();
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeCactuar199Resources().Model;
        NativeBattleRawModelBinding binding = new(0x2200D20, 4);
        byte[] model = Enumerable.Repeat((byte)0x80, 0x90).ToArray();
        NativeBattleCactuarRawModelState source = new(model.ToImmutableArray(), Enumerable.Repeat((byte)0xA5, 0x34).ToImmutableArray(), null, null, null, null);
        NativeBattleCactuarRawModelState state = NativeBattleCactuarRawModel.Initialize5A8F40(source, resource, 0x20DFAB8, 16, binding);
        Assert.Equal(0x2200D2Cu, BinaryPrimitives.ReadUInt32LittleEndian(state.Model.AsSpan().Slice(0x64)));
        Assert.Equal(0x2200D20u, BinaryPrimitives.ReadUInt32LittleEndian(state.Model.AsSpan().Slice(0x84)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(state.Control.AsSpan().Slice(2)));
        Assert.Equal((byte)0xA5, state.Control[0]);
        Assert.Equal((byte)0x80, state.Model[0x6D]);
        Assert.Equal((byte)0x80, state.Model[0x14]);
        Assert.Throws<InvalidDataException>(() => NativeBattleCactuarRawModel.Validate(state, resource));
        state = NativeBattleCactuarRawModel.Select5A8FB0(state, resource, 0, binding);
        NativeBattleCactuarRawModelStep expected = NativeBattleCactuarRawModel.Advance5A8FD0(state, resource, binding);
        NativeBattleCactuarRawModelState restored = JsonSerializer.Deserialize<NativeBattleCactuarRawModelState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(NativeBattleCactuarRawModel.Advance5A8FD0(restored, resource, binding)));
    }

    [Fact]
    public void Shared_projected_prefix_and_selected_depth_shift_keep_one_current_draw_algorithm()
    {
        using GameAssets game = Installed();
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeCactuar199Resources().Model;
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleRawModelBinding binding = new(0x2200D20, 4);
        NativeBattleCactuarRawModelState state = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, 0x20DFAB8, 16, binding);
        state = NativeBattleCactuarRawModel.Select5A8FB0(state, resource, 0, binding);
        byte[] model = state.Model.ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(0x40 + index * 2), index % 4 == 0 ? (short)4096 : (short)0);
        }
        state = state with { Model = model.ToImmutableArray() };
        int length = resource.Geometry[0].Vertices.Length * 8;
        byte[] sentinel = Enumerable.Repeat((byte)0xA5, length + 32).ToArray();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x31800, sentinel);
        NativeBattleActionMatrix parent = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(parent, 216, 160, 108);
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuarRawModel.Draw5A9010(state, resource, parent, projection, colors, binding));
        NativeBattleGilgameshRawModelDraw depth4 = NativeBattleGilgameshRawModel.Draw595E40(state, resource, binding.ControlAddress, arena, parent, projection, colors);
        NativeBattleGilgameshRawModelDraw depth3 = NativeBattleGilgameshRawModel.Draw595E40(state, resource, binding.ControlAddress, arena, parent, projection, colors, 3);
        Assert.Null(depth4.State.ProjectedScratch);
        Assert.Equal(sentinel.AsSpan(length).ToArray(), depth4.Arena.Read(0x31800 + length, 32));
        Assert.NotEqual(sentinel.AsSpan(0, length).ToArray(), depth4.Arena.Read(0x31800, length));
        Assert.Equal(depth4.Arena.Read(0x31800, length), depth3.Arena.Read(0x31800, length));
        Assert.NotEmpty(depth4.Packets.Attempts);
        Assert.Equal(depth4.Packets.Attempts.Length, depth3.Packets.Attempts.Length);
        for (int index = 0; index < depth4.Packets.Attempts.Length; index++)
        {
            NativeBattleGeometryPrimitive first = depth4.Packets.Attempts[index].Primitive!;
            NativeBattleGeometryPrimitive second = depth3.Packets.Attempts[index].Primitive!;
            Assert.Equal(first.Words.ToArray(), second.Words.ToArray());
            Assert.InRange(second.Bucket - 17 - 2 * (first.Bucket - 17), 0, 1);
        }
        NativeBattleGilgameshRawModelDraw restored = NativeBattleGilgameshRawModel.Draw595E40(JsonSerializer.Deserialize<NativeBattleCactuarRawModelState>(JsonSerializer.Serialize(depth4.State))!, resource, binding.ControlAddress, JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(depth4.Arena))!, parent, projection, colors);
        Assert.Equal(JsonSerializer.Serialize(depth4), JsonSerializer.Serialize(restored));
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x7C), 0);
        NativeBattleGilgameshRawModelDraw masked = NativeBattleGilgameshRawModel.Draw595E40(state with { Model = model.ToImmutableArray() }, resource, binding.ControlAddress, arena, parent, projection, colors);
        Assert.Same(arena, masked.Arena);
        Assert.Empty(masked.Packets.Attempts);
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
