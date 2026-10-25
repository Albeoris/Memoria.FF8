using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect308ControllerTests
{
    [Fact]
    public void First_root_callback_selects_second_packet_bank_without_reading_children()
    {
        NativeBattleEffect308Frame frame = NativeBattleEffect308TestFrame.Create(0, false);
        NativeBattleEffect308Step step = AdvanceRoot667720(frame, RootBank, null!, NativeBattleEffect308TestFrame.Inputs());
        Assert.Equal(NativeBattleEffect308TestFrame.Common + SecondPacketOffset, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(1u, Read(step.Frame, RootBank).ReadDword(16));
        Assert.Single(step.Frame.Effect.Pools);
    }

    [Fact]
    public void Terminal_main_restores_opaque_colors_then_root_waits_for_actual_IO()
    {
        NativeBattleEffect308Frame frame = NativeBattleEffect308TestFrame.Create(110);
        List<NativeBattleEffect308Operation> operations = [];
        NativeBattleEffect308Inputs inputs = NativeBattleEffect308TestFrame.Inputs(operations, true);
        NativeBattleEffect308Step busy = AdvanceRoot667720(frame, RootBank, null!, inputs);
        Assert.Equal(0, busy.Result);
        Assert.Equal((ushort)110, Read(busy.Frame, RootBank).ReadWord(12));
        Assert.Equal(3, operations.OfType<NativeBattleEffect308WriteModelBytes>().Count());
        Assert.All(operations.OfType<NativeBattleEffect308WriteModelBytes>(), operation => Assert.Equal(4, operation.Length));
        NativeBattleEffect308Step done = AdvanceRoot667720(busy.Frame, RootBank, null!, NativeBattleEffect308TestFrame.Inputs(operations));
        Assert.Equal(2, done.Result);
        Assert.Equal((ushort)110, Read(done.Frame, RootBank).ReadWord(12));
        Assert.IsType<NativeBattleEffect308Intensity5713E0>(operations[^1]);
    }

    [Theory]
    [InlineData(86, 4096)]
    [InlineData(101, 256)]
    [InlineData(102, -1)]
    [InlineData(103, -1)]
    public void Late_flash_and_IO_use_original_boundaries(short phase, int white)
    {
        List<NativeBattleEffect308Operation> operations = [];
        NativeBattleEffect308Frame frame = NativeBattleEffect308TestFrame.Create(phase);
        NativeBattleEffect308Step step = AdvanceMain668DE0(frame, NativeBattleEffect308TestFrame.Main, null!, NativeBattleEffect308TestFrame.Inputs(operations));
        Assert.Equal((ushort)(phase + 1), Read(step.Frame, NativeBattleEffect308TestFrame.Main).ReadWord(12));
        NativeBattleEffect308White7040B0? flash = operations.OfType<NativeBattleEffect308White7040B0>().SingleOrDefault();
        Assert.Equal(white, flash?.Amount ?? -1);
        NativeBattleEffect308QueueFile5341D0[] queued = operations.OfType<NativeBattleEffect308QueueFile5341D0>().ToArray();
        if (phase == 103)
        {
            Assert.Equal(new short[] { 360, 361 }, queued.Select(operation => operation.FileId));
            Assert.Equal(new short[] { 1, 0 }, queued.Select(operation => operation.Type));
            Assert.All(queued, operation => Assert.Equal(NativeBattleEffect308TestFrame.Common + FirstPacketOffset, operation.DestinationAddress));
        }
        else
        {
            Assert.Empty(queued);
        }
    }

    [Fact]
    public void Programme_clear_crosses_last_slot_into_particle_header_without_clipping()
    {
        NativeBattleEffect308Frame frame = NativeBattleEffect308TestFrame.Create(20);
        NativeBattleEffectTaskPool scripts = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ScriptDescriptor);
        for (int index = 0; index < scripts.Slots.Length - 1; index++)
        {
            (frame, _) = Append(frame, ScriptDescriptor, ScriptCallback);
        }
        (frame, uint particle) = Append(frame, ParticleDescriptor, 0x668A50);
        frame = Write(frame, particle, Read(frame, particle).WriteWord(12, 0xBEEF));
        frame = BirthScript667940(frame, 0x1D97494, NativeBattleEffect308TestFrame.Main + 0x20, ScriptRanges[0].Address);
        Assert.Equal((ushort)0, Read(frame, particle).Flags);
        Assert.Null(Read(frame, particle).Next);
        Assert.Equal((ushort)0xBEEF, Read(frame, particle).ReadWord(12));
        Assert.Equal(0x668A50u, Read(frame, particle).Callback);
    }
}

internal static class NativeBattleEffect308TestFrame
{
    public const uint Common = 0x20DFAB8;
    public const uint Main = Common + 0x1B3C;

    public static NativeBattleEffect308Frame Create(short phase, bool children = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[8]).WriteWord(12, unchecked((ushort)phase)).WriteByte(15, children ? (byte)1 : (byte)0)) };
        NativeBattleEffect308State state = NativeBattleEffect308State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Common).WriteDword(CasterGlobal, 0x1D97494) with
        {
            ScriptSourcesBound = true,
            ScriptStorage = ScriptRanges.ToImmutableDictionary(range => range.Address, range => NativeBattleEffectArena.Clear().Write(0, new byte[range.Length]))
        };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (children)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Common + shape.Bank, shape.Stride, shape.Capacity);
                if (shape.Descriptor == MainDescriptor)
                {
                    (pool, _) = pool.Append(MainCallback);
                    pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteBytes(12, new byte[shape.Stride - 12]).WriteWord(12, unchecked((ushort)phase))) };
                }
                pools = pools.Add(pool);
                state = state.DetachCommonQueue(shape.Descriptor);
            }
        }
        NativeBattleOriginalEffectState effect = new(308, 307, new(308, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    public static NativeBattleEffect308Inputs Inputs(List<NativeBattleEffect308Operation>? operations = null, bool busy = false)
    {
        return new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), (_, _) => null!, (_, _) => null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, _, retained) => new(frame, retained), _ => null!, (frame, _) => new(frame, busy), frame => new(frame, 0), (frame, operation) =>
        {
            operations?.Add(operation);
            return frame;
        });
    }
}
