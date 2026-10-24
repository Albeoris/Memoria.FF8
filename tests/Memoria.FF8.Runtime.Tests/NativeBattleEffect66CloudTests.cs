using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66CloudTests
{
    [Fact]
    public void Full_first_pool_consumes_no_birth_random_and_balances_actual_two_allocations()
    {
        NativeBattleEffect66Frame frame = Frame(0x624DF0);
        for (int index = 0; index < 100; index++)
        {
            frame = WriteBytes(frame, FirstPointAddress + (uint)(index * 24), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)), 4);
        }
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x624DF0, ChildBank, Resources(), Inputs());
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(2, result.Result);
        Assert.Equal(1, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new int?[] { 0, 0, 2000 }, Enumerable.Range(0, 3).Select(index => result.Frame.Memory.ScriptedLighting!.BackColor!.ReadLane(index)));
    }

    [Fact]
    public void Pause_skips_birth_and_clock_but_publishes_actual_alternate_view_registers()
    {
        NativeBattleEffect66Frame frame = Frame(0x625110);
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x625110, ChildBank, Resources(), Inputs(flags: 0x200));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(0, result.Result);
        Assert.Equal(0, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.NotNull(result.Frame.Memory.ScriptedLighting!.LightMatrix);
        Assert.Equal(2000, result.Frame.Memory.ScriptedLighting.BackColor!.ReadLane(2));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void First_sprite_birth_uses_constant_scale_before_exactly_three_CRT_offsets()
    {
        NativeBattleEffect66Frame frame = Frame(0x624DF0);
        NativeCrtRandomState random = frame.Random.Draw(out ushort x).Draw(out ushort y).Draw(out ushort z);
        List<NativeBattleEffect66DeliverBatch> deliveries = [];
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x624DF0, ChildBank, Resources(), Inputs(deliveries));
        NativeBattleEffectArena row = ReadBytes(result.Frame, FirstPointAddress, 24);
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(1u, U32(row, 0));
        Assert.Equal(1, S16(row, 4));
        Assert.Equal(0x1200, S16(row, 6));
        Assert.Equal(unchecked((short)(100 + x % 800 - 400)), S16(row, 8));
        Assert.Equal(unchecked((short)(200 + y % 800 - 400)), S16(row, 10));
        Assert.Equal(unchecked((short)(300 + z % 800 - 400)), S16(row, 12));
        Assert.Single(deliveries);
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Five_moving_births_use_five_CRT_draws_each_and_update_positions_and_drag_after_drawing()
    {
        NativeBattleEffect66Frame frame = Frame(0x625110);
        NativeCrtRandomState random = frame.Random;
        for (int draw = 0; draw < 25; draw++)
        {
            random = random.Draw(out _);
        }
        List<NativeBattleEffect66DeliverBatch> deliveries = [];
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x625110, ChildBank, Resources(), Inputs(deliveries));
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(5, deliveries.Count);
        for (int index = 0; index < 5; index++)
        {
            NativeBattleEffectArena row = ReadBytes(result.Frame, SecondPointAddress + (uint)(index * 24), 24);
            Assert.Equal(1u, U32(row, 0));
            Assert.Equal(1, S16(row, 4));
        }
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Timed_pool_cleanup_preserves_other_marker_bits_and_retained_record_payload()
    {
        NativeBattleEffect66Frame frame = Frame(0x626F80, 119);
        frame = WriteBytes(frame, SecondPointAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)).Write(8, BitConverter.GetBytes(0xA55A1234u)), 12);
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x626F80, ChildBank, Resources(), Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(120, Read(result.Frame, ChildBank).ReadWord(12));
        Assert.Equal(2u, U32(ReadBytes(result.Frame, SecondPointAddress, 12), 0));
        Assert.Equal(0xA55A1234u, U32(ReadBytes(result.Frame, SecondPointAddress, 12), 8));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Last_sprite_frame_retires_before_age_and_velocity_updates()
    {
        NativeBattleEffect66Frame frame = Frame(0x625110, 2);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((short)1)).Write(6, BitConverter.GetBytes((short)4096)).Write(8, BitConverter.GetBytes((short)100)).Write(10, BitConverter.GetBytes((short)200)).Write(12, BitConverter.GetBytes((short)300)).Write(16, BitConverter.GetBytes((short)100));
        frame = WriteBytes(frame, SecondPointAddress, row, 24);
        NativeBattleEffect66Step result = NativeBattleEffect66Clouds.Advance(frame, 0x625110, ChildBank, Resources(), Inputs());
        row = ReadBytes(result.Frame, SecondPointAddress, 24);
        Assert.Equal(0u, U32(row, 0));
        Assert.Equal(1, S16(row, 4));
        Assert.Equal(100, S16(row, 8));
        Assert.Equal(100, S16(row, 16));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(2, result.Result);
    }

    private static NativeBattleEffect66Frame Frame(uint callback, ushort age = 0)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (child, _) = child.Append(callback);
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, age).WriteWord(14, 1).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 300)) };
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(matrix, 0) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]);
        return new(memory, new(66, 65, new(66, Entry, Initializer), null, null) { Pools = [root, child] }, new(123), NativeBattleEffect66State.FromColdProcess().DetachBank(0).DetachBank(1)) { Staging = staging };
    }
    private static NativeBattleEffect66Inputs Inputs(List<NativeBattleEffect66DeliverBatch>? deliveries = null, uint flags = 0) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => flags, null!, _ => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096), null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, null!, null!, null!, null!, (frame, operation) =>
    {
        deliveries?.Add(Assert.IsType<NativeBattleEffect66DeliverBatch>(operation));
        return frame;
    });
    private static NativeBattleEffect66Resources Resources()
    {
        ImmutableDictionary<uint, NativeBattleModuleResource>.Builder sprites = ImmutableDictionary.CreateBuilder<uint, NativeBattleModuleResource>();
        foreach (uint address in SpriteAddresses)
        {
            byte[] bytes = new byte[64];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x06010524);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 40);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), ushort.MaxValue);
            foreach (int start in new[] { 16, 40 })
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start), 0x80000001);
                bytes[start + 4] = bytes[start + 6] = 2;
                bytes[start + 5] = 100;
                bytes[start + 7] = 0x2C;
            }
            sprites.Add(address, new(0, address, 0, 0, bytes.ToImmutableArray()));
        }
        return new(new("texture", []), new("embedded", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites.ToImmutable(), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }
    private static short S16(NativeBattleEffectArena source, int at) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(at, 2));
    private static uint U32(NativeBattleEffectArena source, int at) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(at, 4));
}
