using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleJunk44StateTests
{
    [Fact]
    public void Cold_state_owns_only_scalar_BSS_and_inactive_root()
    {
        var state = NativeBattleJunk44State.FromColdProcess();
        state.Validate();
        Assert.Equal(0u, state.ReadDword(InitializerGlobal));
        Assert.Equal(0u, state.ReadDword(CursorGlobal));
        Assert.Equal(new byte[RootBankLength], state.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new[] { 20, 88, 52, 32, 76 }, Pools.Select(pool => (int)pool.Stride).ToArray());
        Assert.Equal(new[] { 1, 32, 16, 32, 1 }, Pools.Select(pool => (int)pool.Capacity).ToArray());
    }

    [Fact]
    public void COMMON_banks_scratch_packets_descriptors_and_gaps_are_not_State_aliases()
    {
        var state = NativeBattleJunk44State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, CursorGlobal + 4, ArenaGlobal + 4, RootBank, ArenaAddress, ScratchAddress, ArenaAddress + PacketOffset, RootDescriptor })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(12, new byte[1]) }).Validate());
    }

    [Fact]
    public void Partial_writes_and_exclusive_root_transfer_retain_masks_through_JSON()
    {
        var state = NativeBattleJunk44State.FromUnknown().WriteWord(CasterGlobal, 0x1234) with { RootBankStorage = null };
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CasterGlobal + 2));
        var restored = JsonSerializer.Deserialize<NativeBattleJunk44State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)0x1234, restored.ReadWord(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CasterGlobal + 2));
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(RootBankLength, 1) }).Validate());
    }

    [Fact]
    public void Current_COMMON_XYZ_scratch_retains_W_without_changing_immutable_programme()
    {
        var resources = Resources();
        resources.Validate();
        var arena = NativeBattleEffectArena.Clear().MarkUnknown(ScratchOffset, ScratchLength).Write(ScratchOffset, new byte[] { 1, 2, 3, 4, 5, 6 });
        Assert.Throws<NotSupportedException>(() => arena.Read(ScratchOffset + 6, 2));
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadGeometry(ProgrammeAddress + 12).Bytes.AsSpan().Slice(4)));
        Assert.Equal(6, resources.Sprite.Kind);
        Assert.Throws<InvalidDataException>(() => resources.Sprite.Validate());
    }

    internal static NativeBattleJunk44Resources Resources()
    {
        byte[] programme = new byte[ProgrammeLength];
        Put(programme, 0, 1);
        Put(programme, 4, 0xE0);
        Put(programme, 8, 12);
        Put(programme, 12, 84);
        Put(programme, 16, 9);
        Put(programme, 20, 1);
        Put(programme, 108, 4);
        Put(programme, 0xE0, 1);
        Put(programme, 0xE4, 12);
        Put(programme, 0xE8, 12);
        byte[] sprite = new byte[SpriteLength];
        Put(sprite, 0, 0x06010524);
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 13);
        for (int frame = 0; frame <= 13; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 40);
        }
        byte[] table = new byte[168];
        var meshes = ImmutableArray.CreateBuilder<NativeBattleStaticMeshResource>();
        int meshIndex = 0;
        for (int row = 0; row < 7; row++)
        {
            int count = row == 5 ? 4 : 1;
            BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(row * 24 + 16), checked((short)count));
            for (int frame = 0; frame < count; frame++)
            {
                uint address = checked(0x100000u + (uint)meshIndex * 0x100);
                byte[] mesh = new byte[48];
                Put(mesh, 0, 16);
                Put(mesh, 4, 1);
                meshes.Add(new(checked((byte)meshIndex), address, mesh.ToImmutableArray()));
                Put(table, row * 24 + frame * 4, address);
                meshIndex++;
            }
        }
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        return new(new(ProgrammeAddress, programme.ToImmutableArray()), new(0, SpriteAddress, 0, 0, sprite.ToImmutableArray()), table.ToImmutableArray(), meshes.ToImmutable(), new("FF8_EN:1354A30", camera.ToImmutableArray()), 0x64292, [new(360, "MA8DEF_0.TIM", new byte[33824].ToImmutableArray()), new(361, "MA8DEF_1.TIM", new byte[33824].ToImmutableArray())]);

        static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
