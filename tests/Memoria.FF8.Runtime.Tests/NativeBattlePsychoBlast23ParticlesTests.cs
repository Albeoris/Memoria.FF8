using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePsychoBlast23ParticlesTests
{
    [Fact]
    public void Emitter_consumes_eight_CRT_draws_per_child_and_keeps_packed_anchor_W()
    {
        NativeBattlePsychoBlast23ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(5, NativeBattlePsychoBlast23Profile.DebrisEmitterCallback, 176, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        byte[] basis = new byte[56];
        BinaryPrimitives.WriteInt16LittleEndian(basis, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(basis.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(basis.AsSpan(16), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(basis.AsSpan(0x30), 0xFFFE1234);
        BinaryPrimitives.WriteUInt32LittleEndian(basis.AsSpan(0x34), 0xABCD5678);
        NativeBattlePsychoBlast23Frame initial = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = append.Hierarchy.Pools }, State = fixture.Frame.State with { Basis56 = NativeBattleEffectArena.Clear().Write(0, basis) } };
        NativeBattlePsychoBlast23LeafStep step = NativeBattlePsychoBlast23Particles.AdvanceEmitter(initial, append.Address, fixture.Inputs, (_, _, _) => new(0, 3));
        Assert.Equal(8, step.Frame.Effect.Pools[5].Slots.Count(task => task.Callback == NativeBattlePsychoBlast23Profile.DebrisCallback));
        NativeCrtRandomState random = initial.Random;
        for (int index = 0; index < 64; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(32, fixture.Operations.OfType<NativeBattlePsychoBlast23Rotate>().Count());
        foreach (NativeBattleModulePoolTask child in step.Frame.Effect.Pools[5].Slots.Where(task => task.Callback == NativeBattlePsychoBlast23Profile.DebrisCallback))
        {
            for (int point = 0; point < 4; point++)
            {
                Assert.Equal(0xFFFE1234u, child.ReadDword(0x6C + point * 8));
                Assert.Equal(0xABCD5678u, child.ReadDword(0x70 + point * 8));
            }
        }
        int observed = 0;
        NativeBattlePsychoBlast23Inputs secondInputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattlePsychoBlast23Rotate>(operation);
            NativeBattleModulePoolTask emitter = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address);
            Assert.Equal((byte)1, emitter.ReadByte(0x29));
            Assert.Equal(1, emitter.ReadByte(0x26) & 1);
            NativeBattleModulePoolTask child = frame.Effect.Pools[5].Slots.Last(slot => slot.Callback == NativeBattlePsychoBlast23Profile.DebrisCallback);
            int velocity = 0x8C + (observed % 4) * 8;
            Assert.Equal((ushort)0, child.ReadWord(velocity));
            Assert.InRange(child.ReadWord(velocity + 2), (ushort)0x400, (ushort)0x13FF);
            Assert.Equal((ushort)0, child.ReadWord(velocity + 4));
            observed++;
            return frame;
        } };
        step = NativeBattlePsychoBlast23Particles.AdvanceEmitter(step.Frame, append.Address, secondInputs, (_, _, _) => new(0, 3));
        Assert.Equal(16, observed);
        Assert.Equal(12, step.Frame.Effect.Pools[5].Slots.Count(task => task.Callback == NativeBattlePsychoBlast23Profile.DebrisCallback));
        for (int index = 0; index < 32; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, step.Frame.Random);
    }

    [Fact]
    public void Four_trajectories_use_signed_damping_without_gravity_and_draw_even_on_death_transition()
    {
        NativeBattlePsychoBlast23ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(5, NativeBattlePsychoBlast23Profile.DebrisCallback, 176, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = append.Hierarchy.Read(append.Address).WriteByte(0x29, 1).WriteWord(0x50, 8).WriteWord(0x52, 8);
        for (int point = 0; point < 4; point++)
        {
            task = task.WriteWord(0x6C + point * 8, 32764).WriteWord(0x72 + point * 8, (ushort)(0xAB00 + point)).WriteWord(0x8C + point * 8, 129).WriteWord(0x8E + point * 8, unchecked((ushort)-129));
        }
        hierarchy = append.Hierarchy.Write(append.Address, task);
        List<NativeBattleModulePoolTask> draws = [];
        NativeBattlePsychoBlast23Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattlePsychoBlast23DebrisDraw>(operation);
            draws.Add(NativeBattleTaskFixture.ObserveTask(new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address)));
            return frame;
        } };
        NativeBattlePsychoBlast23LeafStep step = NativeBattlePsychoBlast23Particles.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, append.Address, inputs);
        Assert.Equal(4, draws.Count);
        for (int point = 0; point < 4; point++)
        {
            Assert.Equal(unchecked((ushort)-32765), draws[point].ReadWord(0x1C));
            Assert.Equal(unchecked((ushort)-7), draws[point].ReadWord(0x1E));
            Assert.Equal((ushort)(0xAB00 + point), draws[point].ReadWord(0x22));
            Assert.Equal((ushort)113, draws[point].ReadWord(0x8C + point * 8));
            Assert.Equal(unchecked((ushort)-113), draws[point].ReadWord(0x8E + point * 8));
            Assert.Equal((byte)5, draws[point].ReadByte(0x26));
        }
        Assert.Equal(2, step.Result);
    }
}
