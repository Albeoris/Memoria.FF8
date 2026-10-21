namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect20ParticlesTests
{
    [Fact]
    public void Spawn_consumes_six_CRT_draws_in_X_Z_Y_order_and_preserves_both_packed_W_words()
    {
        NativeBattleEffect20ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint parent = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(parent, hierarchy.Read(parent).WriteDword(0x1C, 0xFFFE1234).WriteDword(0x20, 0xABCD5678));
        NativeBattleEffect20Frame initial = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleEffect20Frame frame = NativeBattleEffect20Particles.Spawn(initial, parent, 1, (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[3].Slots, slot => slot.Callback == NativeBattleEffect20Profile.ParticleCallback);
        NativeCrtRandomState random = initial.Random;
        for (int point = 0; point < 2; point++)
        {
            random = random.Draw(out ushort x);
            random = random.Draw(out ushort z);
            random = random.Draw(out ushort y);
            Assert.Equal(unchecked((ushort)((x & 0x3FFF) - 0x2000)), task.ReadWord(0x74 + point * 8));
            Assert.Equal(unchecked((ushort)((z & 0x3FFF) - 0x2000)), task.ReadWord(0x78 + point * 8));
            Assert.Equal(unchecked((ushort)(-256 - (y & 0xFFF))), task.ReadWord(0x76 + point * 8));
            Assert.Equal(0xFFFE1234u, task.ReadDword(0x54 + point * 8));
            Assert.Equal(0xABCD5678u, task.ReadDword(0x58 + point * 8));
        }
        Assert.Equal(random, frame.Random);
    }

    [Fact]
    public void Gravity_damping_and_integration_use_signed_truncation_then_WORD_wrap_before_two_draws()
    {
        NativeBattleEffect20ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(3, NativeBattleEffect20Profile.ParticleCallback, 152, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = append.Hierarchy.Read(append.Address).WriteByte(0x29, 1).WriteWord(0x24, 7).WriteDword(0x38, 768).WriteWord(0x54, 100).WriteWord(0x56, 200).WriteWord(0x58, 32764).WriteWord(0x5A, 0xABCD).WriteWord(0x74, unchecked((ushort)-129)).WriteWord(0x76, unchecked((ushort)-384)).WriteWord(0x78, 129).WriteWord(0x62, 0xFEDC);
        hierarchy = append.Hierarchy.Write(append.Address, task);
        List<NativeBattleModulePoolTask> draws = [];
        NativeBattleEffect20Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattleEffect20ParticleDraw>(operation);
            draws.Add(new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address));
            return frame;
        } };
        NativeBattleEffect20Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleEffect20LeafStep step = NativeBattleEffect20Particles.AdvanceParticle(frame, append.Address, inputs);
        Assert.Equal(2, draws.Count);
        Assert.Equal((ushort)93, draws[0].ReadWord(0x1C));
        Assert.Equal((ushort)193, draws[0].ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)-32765), draws[0].ReadWord(0x20));
        Assert.Equal((ushort)0xABCD, draws[0].ReadWord(0x22));
        Assert.Equal((ushort)0xFEDC, draws[1].ReadWord(0x22));
        Assert.Equal(unchecked((ushort)-113), draws[0].ReadWord(0x74));
        Assert.Equal(unchecked((ushort)-112), draws[0].ReadWord(0x76));
        Assert.Equal((ushort)113, draws[0].ReadWord(0x78));
        Assert.Equal(768u, draws[0].ReadDword(0x38));
        step = NativeBattleEffect20Particles.AdvanceParticle(step.Frame, append.Address, inputs);
        Assert.Equal(576u, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(append.Address).ReadDword(0x38));
    }

    [Fact]
    public void Final_particle_shrink_still_emits_two_native_draws_with_scale16_before_release()
    {
        NativeBattleEffect20ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(3, NativeBattleEffect20Profile.ParticleCallback, 152, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = append.Hierarchy.Write(append.Address, append.Hierarchy.Read(append.Address).WriteByte(0x29, 1).WriteWord(0x24, 11).WriteDword(0x38, 192));
        int draws = 0;
        NativeBattleEffect20Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            Assert.IsType<NativeBattleEffect20ParticleDraw>(operation);
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(append.Address);
            Assert.Equal(16u, task.ReadDword(0x30));
            Assert.Equal(16u, task.ReadDword(0x34));
            Assert.Equal(16u, task.ReadDword(0x38));
            Assert.Equal(1, task.ReadByte(0x26) & 1);
            draws++;
            return frame;
        } };
        NativeBattleEffect20LeafStep step = NativeBattleEffect20Particles.AdvanceParticle(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, append.Address, inputs);
        Assert.Equal(2, draws);
        Assert.Equal(2, step.Result);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(hierarchy.Address(0, 0)).ReadByte(0x28));
    }
}
