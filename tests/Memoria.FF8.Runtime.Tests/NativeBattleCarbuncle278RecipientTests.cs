using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278RecipientTests
{
    [Fact]
    public void Recipients_reverse_contacts_clear_their_whole_payload_and_cache_anchor_before_zeroing_height()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame current = fixture.Frame;
        for (int index = 0; index < 3; index++)
        {
            uint expected = (uint)(300 - 100 * index);
            current = NativeBattleCarbuncle278Recipients.Spawn681B8C(current, fixture.Creature.WriteWord(0xC, (ushort)(145 + index * 4)), actor =>
            {
                Assert.Equal(expected, actor);
                return new(-7, 123, 45, 6);
            });
            NativeBattleModulePoolTask task = current.Effect.Pools[0].Slots[index];
            Assert.Equal(expected, task.ReadDword(0x10));
            Assert.Equal(unchecked((uint)-7), task.ReadDword(0x28));
            Assert.Equal(123u, task.ReadDword(0x2C));
            Assert.Equal(45u, task.ReadDword(0x30));
            Assert.Equal((ushort)0, task.ReadWord(0x5E));
            Assert.Equal((ushort)6, task.ReadWord(0x62));
            Assert.Equal(0x10C1CD8u, task.ReadDword(0x64));
            Assert.Equal(0u, task.ReadDword(0x68));
            Assert.Equal(new byte[0x10C - 0x6C], task.Payload.AsSpan().Slice(0x6C - 12).ToArray());
            Assert.Equal(task.Payload, current.Memory.Arena!.Read(0x1FA0 + index * 0x10C + 12, 0x100));
        }
        Assert.All(fixture.Frame.Effect.Pools[0].Slots, task => Assert.Equal((ushort)0, task.Flags));
        NativeBattleCarbuncle278Frame restored = JsonSerializer.Deserialize<NativeBattleCarbuncle278Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature, _ => new(-7, 123, 45, 6))), JsonSerializer.Serialize(NativeBattleCarbuncle278Recipients.Spawn681B8C(restored, fixture.Creature, _ => new(-7, 123, 45, 6))));
        Assert.Same(current, NativeBattleCarbuncle278Recipients.Spawn681B8C(current, fixture.Creature, _ => throw new Exception("Full bank must skip anchor reads.")));
        Assert.Same(fixture.Frame, NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature.WriteWord(0xC, 146), _ => throw new Exception()));
        Assert.Same(fixture.Frame, NativeBattleCarbuncle278Recipients.Spawn681B8C(fixture.Frame, fixture.Creature.WriteWord(0xE, 0), _ => throw new Exception()));
    }

    [Theory]
    [InlineData(700, 2800)]
    [InlineData(2000, 4096)]
    [InlineData(-700, -2800)]
    [InlineData(-10000, 25536)]
    public void Matrix_uses_signed_scale_cap_native_yaw_and_current_view_preserving_both_padding_words(short input, short scale)
    {
        NativeBattleModulePoolTask source = new(1, 0, null, 0x6822F0, Enumerable.Repeat((byte)0xA5, 0x100).ToImmutableArray());
        source = source.WriteDword(0x28, 10).WriteDword(0x2C, 20).WriteDword(0x30, 30);
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleModulePoolTask result = NativeBattleCarbuncle278Recipients.Cache6DBCC0(source, 1024, input, view);
        short[] world = [0, 0, scale, 0, scale, 0, unchecked((short)-scale), 0, 0];
        short[] composed = [0, unchecked((short)-scale), 0, 0, 0, scale, unchecked((short)-scale), 0, 0];
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(world[index], unchecked((short)result.ReadWord(0x14 + index * 2)));
            Assert.Equal(composed[index], unchecked((short)result.ReadWord(0x34 + index * 2)));
        }
        Assert.Equal((ushort)0xA5A5, result.ReadWord(0x26));
        Assert.Equal((ushort)0xA5A5, result.ReadWord(0x46));
        Assert.Equal((80u, 210u, 330u), (result.ReadDword(0x48), result.ReadDword(0x4C), result.ReadDword(0x50)));
        Assert.Equal((10u, 20u, 30u), (result.ReadDword(0x28), result.ReadDword(0x2C), result.ReadDword(0x30)));
        Assert.Equal((ushort)0xA5A5, source.ReadWord(0x14));
    }

    [Fact]
    public void Actor_sparkle_uses_one_shared_draw_and_keeps_age_zero_until_the_next_pool_pass()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508178, NativeBattleEffectNodeMemory.ArenaAddress + 0x23A4, 0x14, 64);
        NativeBattleCarbuncle278Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] } };
        NativeBattleSharedEffectRandomState random = frame.Memory.SharedEffectRandom!.Draw7059E0(out ushort value);
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Recipients.SpawnSparkle6822F0(frame, 123, actor =>
        {
            Assert.Equal(123u, actor);
            return 30;
        }, (actor, bone) =>
        {
            Assert.Equal(123u, actor);
            Assert.Equal(value * 30 >> 15, bone);
            return new(1, -2, 3);
        });
        Assert.Equal(random, result.Memory.SharedEffectRandom);
        NativeBattleModulePoolTask task = result.Effect.Pools[0].Slots[0];
        Assert.Equal((ushort)0, task.ReadWord(0x12));
        Assert.Equal(unchecked((ushort)-2), task.ReadWord(0xE));
        for (int index = 0; index < 64; index++)
        {
            (pool, _) = pool.Append(0x683E80);
        }
        NativeBattleCarbuncle278Frame full = frame with { Memory = frame.Memory with { SharedEffectRandom = null }, Effect = frame.Effect with { Pools = [pool] } };
        Assert.Same(full, NativeBattleCarbuncle278Recipients.SpawnSparkle6822F0(full, 0, _ => throw new Exception(), (_, _) => throw new Exception()));
    }

    private static (NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Creature) Fixture()
    {
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 21);
        NativeBattleCarbuncle278Resources resources = new(new(0, 0, [])) { RecipientProgramme = new(0x10C1CD8, source.ToImmutableArray()) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508158, NativeBattleEffectNodeMemory.ArenaAddress + 0x1FA0, 0x10C, 3);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60) { Carbuncle278Resources = resources }, new(0, 0, 3, 0, 0, 0), null) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleModulePoolTask creature = new(1, 0, null, 0x681630, new byte[0x5E0 - 12].ToImmutableArray());
        creature = creature.WriteWord(0xC, 145).WriteWord(0xE, 3).WriteDword(0x14, 100).WriteDword(0x18, 200).WriteDword(0x1C, 300);
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), creature);
    }
}
