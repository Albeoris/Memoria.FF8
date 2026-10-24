using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect213LowerTests
{
    [Theory]
    [InlineData(0x6A0A50u, 13)]
    [InlineData(0x6A0AF0u, 12)]
    [InlineData(0x6A0BE0u, 12)]
    [InlineData(0x6A0C80u, 8)]
    public void Sprite_draw_precedes_final_motion_and_lifetime_even_when_battle_pause_flags_are_set(uint callback, int lifetime)
    {
        Fixture fixture = new(callback, 32);
        fixture.Change(task => task.WriteWord(12, (ushort)(lifetime - 1)).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 30).WriteWord(0x16, 4096).WriteWord(0x18, 2).WriteWord(0x1A, 3).WriteWord(0x1C, 4));
        NativeCrtRandomState random = fixture.Frame.Random;
        NativeBattleEffect213Step result = NativeBattleEffect213Lower.Advance(fixture.Frame, Fixture.Bank, fixture.Resources, fixture.Inputs);
        Assert.Equal(lifetime - 1, fixture.DrawAge);
        Assert.Equal(1, fixture.Deliveries);
        Assert.Equal(lifetime, Read(result.Frame, Fixture.Bank).ReadWord(12));
        Assert.Equal(callback == 0x6A0AF0 ? 10 : 12, Read(result.Frame, Fixture.Bank).ReadWord(0x10));
        Assert.Equal(2, result.Result);
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(fixture.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Debris_draws_before_gravity_and_retains_the_native_bounce_overshoot()
    {
        Fixture fixture = new(0x6A0760, 48);
        fixture.Change(task => task.WriteWord(12, 29).WriteWord(0x12, unchecked((ushort)-5)).WriteWord(0x1A, 10).WriteWord(0x28, 4).WriteWord(0x2A, 5).WriteWord(0x2C, 6));
        NativeBattleEffect213Step result = NativeBattleEffect213Lower.Advance(fixture.Frame, Fixture.Bank, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask task = Read(result.Frame, Fixture.Bank);
        Assert.Equal(29, fixture.DrawAge);
        Assert.Equal((short)25, unchecked((short)task.ReadWord(0x12)));
        Assert.Equal((short)-50, unchecked((short)task.ReadWord(0x1A)));
        Assert.Equal((short)-4, unchecked((short)task.ReadWord(0x20)));
        Assert.Equal((short)-5, unchecked((short)task.ReadWord(0x22)));
        Assert.Equal((short)-6, unchecked((short)task.ReadWord(0x24)));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void Morph_publishes_the_two_weighted_instructions_and_preserves_unknown_W()
    {
        Fixture fixture = new(0x6A0760, 48);
        const uint destination = Fixture.Bank + 0x2000;
        fixture.Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.MarkUnknown(0x2006, 2) } };
        byte[] bytes = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -1000);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1000);
        NativeBattleEffect213Frame result = NativeBattleEffect213Programme.Morph(fixture.Frame, new(0x1000, bytes.ToImmutableArray()), 0x1000, 0, 1, 2048, destination, fixture.Inputs);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(result, destination, 6).Read(0, 6)));
        Assert.DoesNotContain(true, ReadBytes(result, destination, 8).ReadSnapshot(6, 2).Known);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.NotNull(result.Memory.ScriptedColorRegisters.Mac[0]);
    }

    private sealed class Fixture
    {
        public const uint Bank = 0x20DFAB8;
        public NativeBattleEffect213Frame Frame;
        public NativeBattleEffect213Resources Resources;
        public int DrawAge = -1;
        public int Deliveries;
        public NativeBattleEffect213Inputs Inputs;

        public Fixture(uint callback, int stride)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25101B8, Bank, checked((ushort)stride), 1);
            (pool, _) = pool.Append(callback);
            NativeBattleEffectMemory memory = NativeBattleEffect213StreamTests.Memory() with { ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleEffect213StreamTests.Identity(), null) };
            Frame = new(memory, new(213, 212, new(213, 0x69EAC0, 0x69EAD0), null, null) { Pools = [pool] }, new(123), NativeBattleEffect213State.FromColdProcess().WriteDword(0x2510178, Bank)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Change(task =>
            {
                for (int offset = 12; offset < stride; offset += 2)
                {
                    task = task.WriteWord(offset, 0);
                }
                return task;
            });
            byte[] mesh = new byte[48];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 16);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 1);
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = new uint[] { 0x117D8B8, 0x117DC38, 0x117DAF4, 0x117DA18 }.ToImmutableDictionary(address => address, Sprite);
            Resources = new(new("fixture", []), new("fixture", []), ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x117D6C8, new(0, 0x117D6C8, mesh.ToImmutableArray())), sprites, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0x201, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleEffect213StreamTests.Identity(2000), null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, null!, (frame, operation) =>
            {
                Assert.IsType<NativeBattleEffect213DrawBatch>(operation);
                DrawAge = Read(frame, Bank).ReadWord(12);
                Deliveries++;
                return frame;
            });
        }

        public void Change(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Bank, change(Read(Frame, Bank)));

        private static NativeBattleModuleResource Sprite(uint address)
        {
            byte[] bytes = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
            for (int index = 0; index <= 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), 44);
            }
            return new(5, address, 0, 0, bytes.ToImmutableArray());
        }
    }
}
