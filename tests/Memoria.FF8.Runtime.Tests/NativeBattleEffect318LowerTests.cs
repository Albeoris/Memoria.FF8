using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect318LowerTests
{
    [Theory]
    [InlineData(10, 0, 113, 13)]
    [InlineData(11, 2, 100, 10)]
    public void Oriented_sprite_integrates_velocity_before_position_but_terminal_draw_skips_both(int phase, int result, int x, int velocity)
    {
        Fixture fixture = new(0x65F100);
        fixture.Task(task => task.WriteWord(12, (ushort)phase).WriteWord(0x10, 100).WriteWord(0x14, 4000).WriteWord(0x18, 10).WriteWord(0x20, 3));
        NativeBattleEffect318Step step = NativeBattleEffect318Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(step.Frame, fixture.Address);
        Assert.Equal(result, step.Result);
        Assert.Equal(x, task.ReadWord(0x10));
        Assert.Equal(velocity, task.ReadWord(0x18));
        Assert.Equal(phase + 1, task.ReadWord(12));
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phase, NativeBattleEffect318Lower.Word318(temp, 4));
        Assert.Equal(1, NativeBattleEffect318Lower.Word318(temp, 0x24));
    }

    [Fact]
    public void Bone_factory_and_wrapper_use_neutral_progression_and_native_step_order()
    {
        Fixture fixture = new(0x65F2B0, false);
        fixture.Frame = NativeBattleEffect318Lower.Birth65F250(fixture.Frame, 35, 8, 1024, 512, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address);
        Assert.Equal(35, fixture.ResolvedBone);
        Assert.Equal(0x1234510u, task.ReadDword(12));
        Assert.Equal(1024, task.ReadWord(20));
        Assert.Equal(512, task.ReadWord(24));
        NativeBattleEffect318Step result = NativeBattleEffect318Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(new NativeBattleEffect318ScaleBone56BEF0(0x1234510, 0, 0), Assert.Single(fixture.Operations));
        Assert.Equal(7, Read(result.Frame, fixture.Address).ReadWord(16));
    }

    [Fact]
    public void Bone_terminal_progression_does_not_apply_final_full_scale()
    {
        Fixture fixture = new(0x65F2B0);
        fixture.Task(task => task.WriteDword(12, 0x1234510).WriteWord(18, 4000).WriteWord(20, 1024).WriteWord(22, 4000).WriteWord(24, 512));
        NativeBattleEffect318Step result = NativeBattleEffect318Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Empty(fixture.Operations);
        Assert.Equal(4096, Read(result.Frame, fixture.Address).ReadWord(18));
    }

    [Fact]
    public void Direction_helpers_read_distinct_angle_WORDs_and_preserve_output_W()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(6, 2);
        NativeBattleEffectArena first = NativeBattleEffect318Lower.Direction65F0A0(NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)0)).Write(2, BitConverter.GetBytes((short)1024)).MarkUnknown(4, 4), retained);
        Assert.Equal((short)4096, NativeBattleEffect318Lower.Word318(first, 0));
        Assert.Equal((short)0, NativeBattleEffect318Lower.Word318(first, 2));
        NativeBattleEffectArena second = NativeBattleEffect318Lower.Direction65F7E0(NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)1024)).Write(4, BitConverter.GetBytes((short)0)).MarkUnknown(2, 2).MarkUnknown(6, 2), retained);
        Assert.Equal((short)0, NativeBattleEffect318Lower.Word318(second, 0));
        Assert.Equal((short)4096, NativeBattleEffect318Lower.Word318(second, 2));
        Assert.DoesNotContain(true, first.ReadSnapshot(6, 2).Known);
        Assert.DoesNotContain(true, second.ReadSnapshot(6, 2).Known);
    }

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public NativeBattleEffect318Frame Frame;
        public readonly uint Address;
        public NativeBattleEffect318Resources Resources;
        public List<NativeBattleEffect318Operation> Operations = [];
        public int ResolvedBone = -1;

        public Fixture(uint callback, bool append = true)
        {
            bool main = callback == 0x65E790;
            bool bone = callback == 0x65F2B0;
            ushort stride = main ? (ushort)0x1304 : bone ? (ushort)28 : (ushort)48;
            Address = Arena + (main ? 0u : bone ? 0x1304u : 0x1390u);
            uint descriptor = main ? 0x2505448u : bone ? 0x2505438u : 0x2505428u;
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, Address, stride, 1);
            if (append)
            {
                (pool, int slot) = pool.Append(callback);
                pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[stride - 12])) };
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, [pool]);
            NativeBattleEffect318State state = NativeBattleEffect318State.FromColdProcess().WriteDword(0x2505488, Arena).WriteDword(0x2505424, 0x1D97494);
            Frame = new(memory, new(318, 317, new(318, 0x65E510, 0x65E520), null, null) { Pools = [pool] }, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] normal = new byte[40];
            BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(8), 12);
            for (int index = 0; index < 12; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(10 + index * 2), 36);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(34), 0xFFFF);
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0xFF7A00, new(5, 0xFF7A00, 40, 0, normal.ToImmutableArray())).Add(0xFF7B6C, Projected());
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
            if (main && append)
            {
                Task(task => task.WriteDword(0x1300, Address + 0xB00));
            }
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update) => Frame = Write(Frame, Address, update(Read(Frame, Address)));
        public NativeBattleEffect318Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, (_, _, bone) =>
        {
            ResolvedBone = bone;
            return 0x1234510;
        }, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        internal static NativeBattleModuleResource Projected()
        {
            byte[] bytes = new byte[324];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 12);
            for (int index = 0; index < 12; index++)
            {
                int offset = 36 + index * 24;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), (ushort)offset);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), 1);
                bytes[offset + 4] = 8;
                bytes[offset + 5] = 128;
                bytes[offset + 6] = 8;
                bytes[offset + 7] = 0x2C;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 20), 4096);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 22), 4096);
            }
            return new(5, 0xFF7B6C, 324, 0, bytes.ToImmutableArray());
        }
    }
}
