using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaEntryTests
{
    [Fact]
    public void Entry_uses_contact_target_basis_and_selected_position_bounds_instead_of_caster_transform()
    {
        var fixture = Fixture();
        NativeBattleShivaEntryStep result = Initialize(fixture);
        Assert.Equal((byte)0, result.State.SourceActor);
        Assert.Equal((byte)3, result.State.FirstContactActor);
        Assert.Equal(new NativeBattleVector(200, 0, 388), result.State.TargetCenter);
        Assert.Equal(NativeBattleGfMatrices.Euler56CD50(new(0, 0x900, 0)).Rotation.ToArray(), result.State.World.Rotation.ToArray());
        Assert.Equal(-4000, result.State.World.X);
        Assert.Equal(0, result.State.World.Y);
        Assert.Equal(388, result.State.World.Z);
        ImmutableArray<NativeBattleGraphicsModel> changed = fixture.Models.SetItem(0, fixture.Models[0] with { Rotation14 = 999, GeometryWorldMatrix = null }).SetItem(3, fixture.Models[3] with { Transform = null });
        NativeBattleShivaEntryStep independent = Initialize((fixture.Memory, fixture.Effect, fixture.Packets, changed));
        Assert.Equal(JsonSerializer.Serialize(result.State), JsonSerializer.Serialize(independent.State));
    }

    [Fact]
    public void Entry_clears_only_workspace_flags_and_queues_actual_initializer_TIM_even_for_repeated_entry_flag()
    {
        var fixture = Fixture();
        NativeBattleShivaEntryStep result = Initialize(fixture);
        for (int index = 0; index < 150; index++)
        {
            Assert.Equal(new byte[4], result.Memory.Arena!.Read(0x2C000 + index * 24, 4));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 20), result.Memory.Arena.Read(0x2C004 + index * 24, 20));
        }
        for (int index = 0; index < 100; index++)
        {
            Assert.Equal(new byte[4], result.Memory.Arena!.Read(0x2D000 + index * 24, 4));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 20), result.Memory.Arena.Read(0x2D004 + index * 24, 20));
        }
        Assert.Equal(fixture.Memory.Arena!.Read(0x2BFFF, 1), result.Memory.Arena!.Read(0x2BFFF, 1));
        Assert.Equal(fixture.Memory.Arena.Read(0x2CE10, 1), result.Memory.Arena.Read(0x2CE10, 1));
        Assert.Equal(fixture.Memory.Arena.Read(0x2D960, 1), result.Memory.Arena.Read(0x2D960, 1));
        Assert.Equal(0, fixture.Memory.Textures!.Count);
        Assert.Equal(1, result.Memory.Textures!.Count);
        Assert.Equal(0x40000, result.Memory.Textures.Slots[0]!.ArenaOffset);
        Assert.Null(result.Memory.Textures.Animation);
        NativeBattleShivaEntryStep repeated = Initialize((fixture.Memory, fixture.Effect with { Parameters = fixture.Effect.Parameters! with { Flags79 = 1 } }, fixture.Packets, fixture.Models));
        Assert.Equal(1, repeated.Memory.Textures!.Count);
        Assert.Equal(0x22BC180u, result.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 100 }, result.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x5C7F50u, result.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(0x5C0F30u, result.Effect.Pools[1].Slots[0].Callback);
        Assert.Equal((ushort)0, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, result.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(0, result.State.Counter22BD010);
    }

    [Fact]
    public void Independent_JSON_entry_preserves_exact_result_and_rejects_missing_actual_owners()
    {
        var fixture = Fixture();
        NativeBattleShivaEntryStep expected = Initialize(fixture);
        NativeBattleShivaEntryStep actual = Initialize((RoundTrip(fixture.Memory), RoundTrip(fixture.Effect), RoundTrip(fixture.Packets), RoundTrip(fixture.Models)));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(RoundTrip(expected)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Arena = null }, fixture.Effect, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory with { Textures = null }, fixture.Effect, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect with { ResourcePointers = [] }, fixture.Packets, fixture.Models)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect with { Parameters = null }, fixture.Packets, fixture.Models)));
        Assert.Throws<InvalidDataException>(() => Initialize((fixture.Memory, fixture.Effect with { Parameters = fixture.Effect.Parameters! with { TargetMask7A = 0 } }, fixture.Packets, fixture.Models)));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(3, fixture.Models[3] with { Rotation14 = null }))));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(3, fixture.Models[3] with { GeometryWorldMatrix = null }))));
        Assert.Throws<NotSupportedException>(() => Initialize((fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models.SetItem(1, fixture.Models[1] with { Transform = null }))));
    }

    private static NativeBattleShivaEntryStep Initialize((NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) fixture) => NativeBattleShivaEntry.Initialize5C0D50(fixture.Memory, fixture.Effect, fixture.Packets, fixture.Models);

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, ImmutableArray<NativeBattleGraphicsModel> Models) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 254, 14, 0, 65, 185, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(185, 184, new(185, 0x5C0D50, 0x5C0D30), new(0, 0, (1 << 1) | (1 << 4) | (1 << 6), 0, 0, 0), null) { ResourcePointers = [new(0x22BC150, 0x40000)] };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x2BFFF, Enumerable.Repeat((byte)0xA5, 0x2D962 - 0x2BFFF).ToArray()).Write(0x40000, Tim());
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
