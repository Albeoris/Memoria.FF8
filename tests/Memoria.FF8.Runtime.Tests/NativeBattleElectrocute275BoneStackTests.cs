using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275BoneStackTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(28)]
    [InlineData(255)]
    public void Last_composed_bone_saved_EDI_is_the_same_physical_first_draw_direction_DWORD(int boneCount)
    {
        NativeBattleStackFrame outer = new(0x200000);
        NativeBattleStackFrame compose = outer.EnterCdecl(0, 0, 24);
        NativeBattleStackFrame multiply = compose.EnterCdecl(0, 4, 12);
        NativeBattleStackFrame scan = outer.EnterCdecl(0, 0, 28);
        NativeBattleStackFrame callback = scan.EnterCdecl(0, 4, 4);
        NativeBattleStackFrame draw = callback.EnterCdecl(4, 4, 12);
        Assert.Equal(multiply.AddressAtOffset(-40), draw.AddressAtOffset(-8));
        uint savedEdi = checked((uint)(boneCount - 1));
        NativeBattleStackBytes observed = NativeBattleStackBytes.Unknown("current bone scan", -8, 4).StoreUInt32(-8, savedEdi, 0x56C2F8, "final bone composition saved EDI");
        Assert.Equal(BitConverter.GetBytes(savedEdi), observed.Bytes.ToArray());
        Assert.Equal(savedEdi, NativeBattleElectrocute275Ribbons.CaptureComposedBoneDirectionWord(boneCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void Missing_actual_bone_composition_does_not_supply_a_direction(int count)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleElectrocute275Ribbons.CaptureComposedBoneDirectionWord(count));
    }

    [Fact]
    public void Final_native_direction_is_reused_by_the_next_draw_and_survives_JSON()
    {
        NativeBattleRibbonPacketStep first = NativeBattleRibbonPackets.DrawWithDirection(Chain(100, 116, 132, 512), Address, 3, Bank, Distances, Policy, 27);
        Assert.Equal(0x10000000u, first.DirectionWord);
        NativeBattleRibbonPacketStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRibbonPacketStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleEffectArena clipped = Chain(100, 116, 132, -1);
        NativeBattleRibbonPacketStep next = NativeBattleRibbonPackets.DrawWithDirection(clipped, Address, 3, Bank, Distances, Policy, restored.DirectionWord);
        Assert.Equal(first.DirectionWord, next.DirectionWord);
        Assert.Single(next.Batch.Attempts);
        Assert.Throws<NotSupportedException>(() => NativeBattleRibbonPackets.DrawWithDirection(clipped, Address, 3, Bank, Distances, Policy));
    }

    [Fact]
    public void Existing_draw_API_uses_identical_packet_and_arena_operations()
    {
        NativeBattleEffectArena arena = Chain(100, 116, 132, 512);
        var original = NativeBattleRibbonPackets.Draw(arena, Address, 3, Bank, Distances, Policy, 27);
        NativeBattleRibbonPacketStep observed = NativeBattleRibbonPackets.DrawWithDirection(arena, Address, 3, Bank, Distances, Policy, 27);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original.Arena)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(observed.Arena)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original.Batch)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(observed.Batch)));
    }

    private const uint Address = NativeBattleEffectNodeMemory.ArenaAddress;
    private static readonly NativeBattleRibbonBank Bank = new(Address, 0, 3);
    private static readonly ImmutableArray<byte> Distances = Enumerable.Repeat((byte)16, 16384).ToImmutableArray();
    private static readonly NativeBattleRibbonPacketPolicy Policy = new(NativeBattleRibbonWidthPolicy.Anchored, 4, 17, 0x3D14C008, 0x00B7C038, 0xC008, 0xC038);

    private static NativeBattleEffectArena Chain(short first, short second, short third, short firstDepth)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        short[] xs = [first, second, third];
        for (int index = 0; index < 3; index++)
        {
            int offset = index * 40;
            uint next = index < 2 ? Address + (uint)(offset + 40) : 0;
            arena = arena.Write(offset + 2, BitConverter.GetBytes((short)32)).Write(offset + 0x10, BitConverter.GetBytes(xs[index])).Write(offset + 0x12, BitConverter.GetBytes((short)200)).Write(offset + 0x14, BitConverter.GetBytes(index == 0 ? firstDepth : (short)512)).Write(offset + 0x20, BitConverter.GetBytes((short)1024)).Write(offset + 0x24, BitConverter.GetBytes(next));
        }
        return arena;
    }
}
