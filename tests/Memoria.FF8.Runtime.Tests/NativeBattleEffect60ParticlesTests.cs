namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect60ParticlesTests
{
    [Fact]
    public void Emitter_initializes_at_age36_without_birth_then_emits_two_children_on_ages37_through44()
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        fixture.Rotations[3] = NativeBattleEffect60ControllerTests.Fixture.PutWord(fixture.Rotations[3], 2, 0);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(5, NativeBattleEffect60Profile.DebrisEmitterCallback, 140, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = append.Hierarchy.Write(append.Address, append.Hierarchy.Read(append.Address).WriteWord(0x24, 36));
        NativeBattleEffect60Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleEffect60Particles.AdvanceEmitter(frame, append.Address, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        Assert.DoesNotContain(frame.Effect.Pools[5].Slots, task => task.Callback == NativeBattleEffect60Profile.DebrisCallback);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect60Rotate>());
        NativeCrtRandomState random = frame.Random;
        for (int callback = 0; callback < 8; callback++)
        {
            frame = NativeBattleEffect60Particles.AdvanceEmitter(frame, append.Address, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        }
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[5].Slots.Where(task => task.Callback == NativeBattleEffect60Profile.DebrisCallback).ToArray();
        Assert.Equal(16, children.Length);
        foreach (NativeBattleModulePoolTask child in children)
        {
            random = random.Draw(out ushort gravity);
            random = random.Draw(out ushort x);
            random = random.Draw(out ushort z);
            random = random.Draw(out ushort vx);
            random = random.Draw(out ushort vz);
            Assert.Equal(unchecked((ushort)-(gravity & 0x7F)), child.ReadWord(0x82));
            Assert.Equal(unchecked((ushort)(300 + (x & 0x1FF) - 0x100)), child.ReadWord(0x1C));
            Assert.Equal((ushort)0, child.ReadWord(0x1E));
            Assert.Equal(unchecked((ushort)(900 + (z & 0xFF) - 0x80)), child.ReadWord(0x20));
            Assert.Equal((ushort)0xABCD, child.ReadWord(0x22));
            Assert.Equal(unchecked((ushort)((vx & 0x1FF) - 0x100)), child.ReadWord(0x78));
            Assert.Equal(unchecked((ushort)(-512 - (vz & 0x7FF))), child.ReadWord(0x7C));
        }
        Assert.Equal(random, frame.Random);
        NativeBattleModulePoolTask emitter = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address);
        Assert.Equal((byte)2, emitter.ReadByte(0x29));
        Assert.Equal(1, emitter.ReadByte(0x26) & 1);
        Assert.Equal((byte)16, emitter.ReadByte(0x28));
    }

    [Fact]
    public void Gravity_precedes_signed_damping_and_one_final_hidden_draw_preserves_packed_W()
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(5, NativeBattleEffect60Profile.DebrisCallback, 140, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = append.Hierarchy.Read(append.Address).WriteByte(0x29, 1).WriteWord(0x50, 15).WriteWord(0x52, 15).WriteWord(0x1C, 32764).WriteWord(0x1E, 100).WriteWord(0x20, 100).WriteWord(0x22, 0xABCD).WriteWord(0x78, 129).WriteWord(0x7A, 128).WriteWord(0x7C, unchecked((ushort)-129)).WriteWord(0x82, unchecked((ushort)-256));
        hierarchy = append.Hierarchy.Write(append.Address, task);
        int draws = 0;
        NativeBattleEffect60Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattleEffect60DebrisDraw>(operation);
            NativeBattleModulePoolTask actual = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address);
            Assert.Equal(unchecked((ushort)-32765), actual.ReadWord(0x1C));
            Assert.Equal((ushort)93, actual.ReadWord(0x1E));
            Assert.Equal((ushort)93, actual.ReadWord(0x20));
            Assert.Equal((ushort)0xABCD, actual.ReadWord(0x22));
            Assert.Equal(unchecked((ushort)-112), actual.ReadWord(0x7A));
            Assert.Equal((byte)5, actual.ReadByte(0x26));
            draws++;
            return frame;
        } };
        NativeBattleEffect60LeafStep step = NativeBattleEffect60Particles.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, append.Address, inputs);
        Assert.Equal(1, draws);
        Assert.Equal(2, step.Result);
    }

    [Fact]
    public void Stage_four_group_operations_ramp_to1536_wait_until_age54_and_finish_at_zero()
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(5, NativeBattleEffect60Profile.StageCallback, 140, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = append.Hierarchy.Write(append.Address, append.Hierarchy.Read(append.Address).WriteWord(0x24, 5));
        NativeBattleEffect60Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleEffect60Particles.AdvanceStage(frame, append.Address, fixture.Inputs).Frame;
        for (int index = 0; index < 6; index++)
        {
            frame = NativeBattleEffect60Particles.AdvanceStage(frame, append.Address, fixture.Inputs).Frame;
        }
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal((byte)2, hierarchy.Read(append.Address).ReadByte(0x29));
        hierarchy = hierarchy.Write(append.Address, hierarchy.Read(append.Address).WriteWord(0x24, 53));
        frame = NativeBattleEffect60Particles.AdvanceStage(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, append.Address, fixture.Inputs).Frame;
        Assert.Equal((byte)2, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address).ReadByte(0x29));
        frame = NativeBattleEffect60Particles.AdvanceStage(frame, append.Address, fixture.Inputs).Frame;
        NativeBattleEffect60LeafStep step = new(frame, 0);
        for (int index = 0; index < 6; index++)
        {
            step = NativeBattleEffect60Particles.AdvanceStage(step.Frame, append.Address, fixture.Inputs);
        }
        Assert.Equal(2, step.Result);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect60StageInitialize>());
        Assert.Equal(new short[] { 256, 512, 768, 1024, 1280, 1536, 1280, 1024, 768, 512, 256, 0 }, fixture.Operations.OfType<NativeBattleEffect60StageFade>().Select(operation => operation.Value));
    }
}
