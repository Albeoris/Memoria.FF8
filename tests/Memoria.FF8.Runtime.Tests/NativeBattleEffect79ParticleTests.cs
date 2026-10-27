using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect79Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79ParticleTests
{
    [Fact]
    public void Stage_fall_waits_past_age90_and_clamps_after_eight_updates()
    {
        Harness harness = new();
        (NativeBattleEffect79Frame frame, uint task) = Child(0x7BCE40, harness);
        frame = Write(frame, task, Read(frame, task).WriteWord(0x24, 90));
        frame = AdvanceParticle7BCE40(frame, task, null!, harness.Inputs()).Frame;
        Assert.Equal((byte)0, Read(frame, task).ReadByte(0x29));
        frame = AdvanceParticle7BCE40(frame, task, null!, harness.Inputs()).Frame;
        Assert.Equal((ushort)2048, Read(frame, task).ReadWord(0x1C));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect79StageTransition>());
        for (int index = 0; index < 8; index++)
        {
            frame = AdvanceParticle7BCE40(frame, task, null!, harness.Inputs()).Frame;
        }
        Assert.Equal(Enumerable.Range(0, 8).Select(index => (7 - index) * 256), harness.Operations.OfType<NativeBattleEffect79StageTransition>().Select(operation => operation.Intensity));
        Assert.Equal((byte)2, Read(frame, task).ReadByte(0x29));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 6)]
    [InlineData(2, 8)]
    public void Births_consume_their_own_CRT_sequence_copy_parent_position_W_and_clear_velocity_W(int kind, int count)
    {
        Harness harness = new();
        (NativeBattleEffect79Frame frame, uint parent) = Child(0x7BBC20, harness);
        frame = Write(frame, parent, Read(frame, parent).WriteDword(0x1C, 0x0014000A).WriteDword(0x20, 0xBEEF001E));
        Assert.Equal((ushort)0xCAFE, frame.Effect.Pools[5].Slots[1].ReadWord(0x92));
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < count; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = kind switch
        {
            0 => Birth7BCBF0(frame, parent, harness.Inputs()),
            1 => Birth7BC720(frame, parent, true, 512, harness.Inputs()),
            _ => Birth7BBCD0(frame, parent, harness.Inputs())
        };
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask particle = frame.Effect.Pools[5].Slots[1];
        Assert.Equal((ushort)0xBEEF, particle.ReadWord(0x72));
        if (kind != 0)
        {
            Assert.Equal((ushort)0xBEEF, particle.ReadWord(0x7A));
        }
        Assert.Equal((ushort)0, particle.ReadWord(0x92));
        Assert.Equal((byte)1, Read(frame, parent).ReadByte(0x28));
    }

    [Fact]
    public void Current_caller_rotation_unknownness_is_not_replaced_by_a_bone_rotation()
    {
        Harness harness = new();
        (NativeBattleEffect79Frame frame, uint parent) = Child(0x7BC610, harness);
        NativeBattleEffect79Inputs inputs = harness.Inputs() with
        {
            ParticleBasis = (current, address, selector, bone, angle) => new(current, new(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray()))
        };
        Assert.Throws<NotSupportedException>(() => Birth7BC720(frame, parent, false, 0, inputs));
    }

    [Fact]
    public void Gravity_wraps_as_WORD_before_damping_and_positions_use_truncation_toward_zero()
    {
        Harness harness = new();
        (NativeBattleEffect79Frame frame, uint address) = Child(0x7BC840, harness);
        NativeBattleModulePoolTask task = Read(frame, address).WriteByte(0x29, 2).WriteByte(0x26, 4).WriteWord(0x8C, unchecked((ushort)-17)).WriteWord(0x8E, 32760).WriteWord(0x90, 17).WriteWord(0x6C, 100).WriteWord(0x6E, 200).WriteWord(0x70, 300).WriteWord(0x7A, 0xBEEF);
        frame = Write(frame, address, task);
        NativeBattleEffect79Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect79Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        frame = AdvanceParticle7BC840(frame, address, null!, harness.Inputs()).Frame;
        restored = AdvanceParticle7BC840(restored, address, null!, harness.Inputs()).Frame;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        task = Read(frame, address);
        Assert.Equal((short)-15, Signed(task, 0x8C));
        Assert.Equal((short)-28455, Signed(task, 0x8E));
        Assert.Equal((short)15, Signed(task, 0x90));
        Assert.Equal((ushort)100, task.ReadWord(0x6C));
        Assert.Equal(unchecked((ushort)(200 - 1778)), task.ReadWord(0x6E));
        Assert.Equal((ushort)300, task.ReadWord(0x70));
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x22));
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Sprite_lifetime_clamps_after_the_strict_signed_endpoint_and_sets_both_retirement_bits()
    {
        Harness harness = new();
        (NativeBattleEffect79Frame frame, uint address) = Child(0x7BCC80, harness);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1).WriteByte(0x26, 4).WriteWord(0x50, 7).WriteWord(0x52, 7));
        NativeBattleEffect79Step step = AdvanceParticle7BCC80(frame, address, null!, harness.Inputs());
        Assert.Equal((ushort)7, Read(step.Frame, address).ReadWord(0x50));
        Assert.Equal((byte)5, Read(step.Frame, address).ReadByte(0x26));
        Assert.Equal((byte)2, Read(step.Frame, address).ReadByte(0x29));
    }

    private static (NativeBattleEffect79Frame Frame, uint Address) Child(uint callback, Harness harness)
    {
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        NativeBattleEffectHierarchyAppend root = new NativeBattleEffectHierarchy(pools).Append8DC540(0, RootCallback, 100);
        NativeBattleEffect79Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect with { Pools = root.Hierarchy.Pools }, NativeCrtRandomState.FromColdProcess(), NativeBattleEffect79State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = Write(frame, RootBank, NativeBattleEffectRootTask.Initialize(Read(frame, RootBank), effect.Parameters!, Packets()));
        (frame, uint address) = AppendHierarchy(frame, 5, callback, 0xB4, RootBank, harness.Inputs());
        frame = Write(frame, address, CopyTask(Read(frame, address), 0x30, NativeBattleEffectArena.Clear(), 0xB4 - 0x30));
        frame = Write(frame, address, Read(frame, address).WriteByte(0x2C, 4));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[5];
        NativeBattleModulePoolTask retained = CopyTask(pool.Slots[1], 12, NativeBattleEffectArena.Clear(), 0xB4 - 12).WriteWord(0x92, 0xCAFE);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(5, pool.WriteSlot(1, retained)) } };
        return (frame, address);
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(4, 8, 16, -1, 351, 79, null));
        (packets, _) = packets.AppendContact(new(new(3, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect79Operation> Operations { get; } = [];

        public NativeBattleEffect79Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, frame => Packets(), frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), frame => new(null, 100, 160, 108), (frame, index) => new(index, 2), (frame, index) => throw new NotSupportedException(), frame => 0, (frame, source) => 0, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        })
        {
            ParticleBasis = (frame, address, selector, bone, angle) => new(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null))
        };
    }
}


