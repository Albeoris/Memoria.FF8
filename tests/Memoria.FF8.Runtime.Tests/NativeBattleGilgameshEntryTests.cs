using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshEntryTests
{
    [Fact]
    public void Entry_uses_first_contact_rotation_and_published_matrix_with_physical_target_bounds()
    {
        var fixture = Fixture();
        NativeBattleGilgameshEntryStep result = Initialize(fixture);
        Assert.Equal((byte)0, result.State.SourceActor);
        Assert.Equal((byte)3, result.State.FirstContactActor);
        Assert.Equal(NativeBattleGfMatrices.Euler56CD50(new(0, 0x900, 0)).Rotation.ToArray(), result.State.World.Rotation.ToArray());
        Assert.Equal(-4500, result.State.World.X);
        Assert.Equal(0, result.State.World.Y);
        Assert.Equal(388, result.State.World.Z);
        ImmutableArray<NativeBattleGraphicsModel> changed = fixture.Models.SetItem(0, fixture.Models[0] with { Rotation14 = 900, GeometryWorldMatrix = null }).SetItem(3, fixture.Models[3] with { Transform = fixture.Models[3].Transform! with { Angle36 = 1234, Position28 = new(20000, 123, -20000) } });
        NativeBattleGilgameshEntryStep independent = Initialize((fixture.Memory, fixture.Effect, fixture.Packets, changed));
        Assert.Equal(NativeBattleTestSnapshots.Json(result.State), NativeBattleTestSnapshots.Json(independent.State));
    }

    [Fact]
    public void Entry_clears_only_record_first_DWORD_and_queues_actual_initializer_once_without_animation()
    {
        var fixture = Fixture();
        NativeBattleGilgameshEntryStep result = Initialize(fixture);
        for (int record = 0; record < 200; record++)
        {
            foreach ((int start, int stride) in new[] { (0x2E000, 0x18), (0x2F800, 0x20) })
            {
                byte[] actual = result.Memory.Arena!.Read(start + record * stride, stride);
                Assert.Equal(new byte[4], actual.AsSpan(0, 4).ToArray());
                Assert.All(actual.Skip(4), value => Assert.Equal((byte)0xA5, value));
                Assert.All(fixture.Memory.Arena!.Read(start + record * stride, stride), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(fixture.Memory.Arena!.Read(0x2DFFF, 1), result.Memory.Arena!.Read(0x2DFFF, 1));
        Assert.Equal(fixture.Memory.Arena.Read(0x31100, 1), result.Memory.Arena.Read(0x31100, 1));
        Assert.Equal(1, result.Memory.Textures!.Count);
        Assert.Equal(0x40000, result.Memory.Textures.Slots[0]!.ArenaOffset);
        Assert.Null(result.Memory.Textures.Animation);
        Assert.Equal(0, fixture.Memory.Textures!.Count);
        Assert.Equal(0x21FF4A8u, result.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 100, 4 }, result.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x596B70u, result.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0x592300u, result.Effect.Pools[1].Slots[0].Callback);
        Assert.Equal((ushort)0, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, result.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Null(result.Effect.Pools[2].Head);
    }

    [Fact]
    public void Entry_restarts_from_independent_JSON_owners_without_reconstructing_prior_actions()
    {
        var fixture = Fixture();
        NativeBattleEffectMemory memory = RoundTrip(fixture.Memory);
        NativeBattleOriginalEffectState effect = RoundTrip(fixture.Effect);
        NativeBattleActionPacketsState packets = RoundTrip(fixture.Packets);
        ImmutableArray<NativeBattleGraphicsModel> models = RoundTrip(fixture.Models);
        NativeBattleGilgameshEntryStep expected = Initialize(fixture);
        NativeBattleGilgameshEntryStep actual = Initialize((memory, effect, packets, models));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(RoundTrip(expected)));
    }

    [Fact]
    public void Entry_requires_actual_parameter_contact_matrix_rotation_target_and_initializer_owners()
    {
        var fixture = Fixture();
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Arena = null }, fixture.Effect, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Textures = null }, fixture.Effect, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect with { ResourcePointers = [] }, fixture.Packets, fixture.Models)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect with { Parameters = null }, fixture.Packets, fixture.Models)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect with { Parameters = fixture.Effect.Parameters! with { TargetMask7A = 0 } }, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(3, fixture.Models[3] with { Rotation14 = null }))));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(3, fixture.Models[3] with { GeometryWorldMatrix = null }))));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(1, fixture.Models[1] with { Transform = null }))));
        NativeBattleActionPacketsState empty = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, empty, fixture.Models)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Controller_entry_publishes_variant3_over_cold_or_retained_DWORD_without_replacing_other_process_owners(int? retainedVariant)
    {
        var fixture = Fixture();
        NativeBattleGilgameshProcess retained = fixture.Memory.Gilgamesh! with { Variant = retainedVariant };
        NativeBattleGilgameshFrame frame = new(fixture.Memory with { Gilgamesh = retained }, fixture.Effect, new(123));
        NativeBattleGilgameshFrame entered = NativeBattleGilgameshController.Initialize58DCF0(frame, fixture.Packets, fixture.Models);
        NativeBattleGilgameshProcess current = entered.Memory.Gilgamesh!;
        Assert.Equal(3, current.Variant);
        Assert.Equal(retainedVariant, frame.Memory.Gilgamesh!.Variant);
        Assert.NotNull(current.Entry);
        Assert.Same(retained.Models, current.Models);
        Assert.Same(retained.SwordTrail, current.SwordTrail);
        Assert.Same(retained.OpeningFlare, current.OpeningFlare);
        Assert.Equal(frame.Random, entered.Random);
        NativeBattleGilgameshEntryStep expected = Initialize(fixture);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.State), NativeBattleTestSnapshots.Json(current.Entry));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Effect), NativeBattleTestSnapshots.Json(entered.Effect));
    }
    private static NativeBattleGilgameshEntryStep Initialize((NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) fixture) => NativeBattleGilgameshEntry.Initialize58DCF0(NativeBattleTestSnapshots.Independent(fixture.Memory), NativeBattleTestSnapshots.Independent(fixture.Effect), fixture.Packets, fixture.Models);

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 2, 330, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(330, 329, new(330, 0x58DCF0, 0x58D740), new(0, 0, (1 << 1) | (1 << 4) | (1 << 6), 0, 0, 0), null) { ResourcePointers = [new(0x2200EC0, 0x40000)] };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x2DFFF, Enumerable.Repeat((byte)0xA5, 0x31102 - 0x2DFFF).ToArray()).Write(0x40000, Tim());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena };
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2)
        {
            Rotation14 = (short)(index == 3 ? 0x100 : 0x500),
            Transform = new(new(0, 0, 0), index switch { 1 => new(-300, 100, -111), 4 => new(700, -500, 888), 6 => new(100, 900, 200), _ => new(25000, 0, 20000) }, 0, 0x600, 4096),
            GeometryWorldMatrix = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 111, 222, 333, 4096)
        }).ToImmutableArray();
        return (memory, effect, packets, models);
    }

    private static byte[] Tim()
    {
        byte[] bytes = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 0x1234);
        return bytes;
    }
}
