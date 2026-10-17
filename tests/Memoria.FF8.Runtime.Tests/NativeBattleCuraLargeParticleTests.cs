using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraLargeParticleTests
{
    [Theory]
    [InlineData(0x88A740u, 0x1611C20u)]
    [InlineData(0x88B690u, 0x161194Cu)]
    public void Scene_workspace_begins_at_native_task_plus_sixty_without_clearing_the_retained_C4_yaw(uint callback, uint sourceAddress)
    {
        var fixture = Fixture(callback);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0xC4, 0x800));
        NativeBattleCuraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 2);
        NativeBattleCuraResources resources = Resources() with { Scenes = [new(sourceAddress, bytes.ToImmutableArray())] };
        NativeBattleCuraParticleStep initialized = NativeBattleCuraParticles.AdvanceLarge(frame, fixture.Address, resources, fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(initialized.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(sourceAddress, task.ReadDword(0x60));
        Assert.Equal(0u, task.ReadDword(0x64));
        Assert.Equal((ushort)0x800, task.ReadWord(0xC4));
        NativeBattleCuraParticleStep next = NativeBattleCuraParticles.AdvanceLarge(initialized.Frame, fixture.Address, resources, fixture.Inputs);
        task = new NativeBattleEffectHierarchy(next.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(1u, task.ReadDword(0x64));
        Assert.Equal((ushort)0x800, task.ReadWord(0xC4));
    }

    [Fact]
    public void Stage_fade_preserves_the_actual_increase_wait_decrease_phases_and_immediate_operations()
    {
        var fixture = Fixture(0x88B3D0);
        NativeBattleCuraFrame frame = fixture.Frame;
        List<NativeBattleCuraStageFadeOperation> fades = [];
        for (int callback = 0; callback < 29; callback++)
        {
            NativeBattleCuraParticleStep step = NativeBattleCuraParticles.AdvanceLarge(frame, fixture.Address, Resources(), fixture.Inputs with { Execute = (current, operation) =>
            {
                fades.Add(Assert.IsType<NativeBattleCuraStageFadeOperation>(operation));
                return current;
            } });
            Assert.Equal(callback == 28 ? 2 : 0, step.Result);
            frame = step.Frame;
        }
        Assert.Equal(new short[] { 0, 192, 384, 576, 768, 960, 1152, 1344, 1536, 1344, 1152, 960, 768, 576, 384, 192, 0 }, fades.Select(fade => fade.Fade));
        Assert.True(fades[0].ClearRgb);
        Assert.All(fades.Skip(1), fade => Assert.False(fade.ClearRgb));
    }

    [Fact]
    public void Scene_emitter_copies_the_real_midpoint_and_consumes_six_random_draws_per_child_before_same_pass_dispatch()
    {
        var fixture = Fixture(0x88B530);
        NativeBattleCuraParticleStep initialized = NativeBattleCuraParticles.AdvanceLarge(fixture.Frame, fixture.Address, Resources(), fixture.Inputs);
        NativeBattleModulePoolTask emitter = new NativeBattleEffectHierarchy(initialized.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)400, emitter.ReadWord(0x1C));
        Assert.Equal((ushort)600, emitter.ReadWord(0x20));
        NativeBattleCuraParticleStep emitted = NativeBattleCuraParticles.AdvanceLarge(initialized.Frame, fixture.Address, Resources(), fixture.Inputs);
        NativeBattleModulePoolTask child = Assert.Single(emitted.Frame.Effect.Pools[2].Slots.Where(item => item.Callback == 0x88B690));
        Assert.Equal(4096u, child.ReadDword(0x30));
        NativeCrtRandomState random = fixture.Inputs.CrtRandom;
        ushort[] values = new ushort[6];
        for (int index = 0; index < values.Length; index++)
        {
            random = random.Draw(out values[index]);
        }
        Assert.Equal((ushort)(values[0] & 0xFFF), child.ReadWord(0x50));
        Assert.Equal((ushort)(values[1] & 0xFFF), child.ReadWord(0x54));
        int dx = (values[2] & 63) + 32;
        int dz = (values[3] & 63) + 32;
        Assert.Equal((ushort)((values[4] & 1) == 0 ? dx : 4096 - dx), child.ReadWord(0x58));
        Assert.Equal((ushort)((values[5] & 1) == 0 ? dz : 4096 - dz), child.ReadWord(0x5C));
        Assert.Equal(random, emitted.CrtRandomAfter);
    }

    [Theory]
    [InlineData(0x88B810u, 0x88B950u, 18, true)]
    [InlineData(0x88CC70u, 0x88CDA0u, 20, false)]
    public void Rising_emitter_uses_actual_bottom_bound_signed_division_and_distinct_random_Y_contract(uint callback, uint spriteCallback, int duration, bool overwritesRandomY)
    {
        var fixture = Fixture(callback);
        NativeBattleCuraParticleStep initialized = NativeBattleCuraParticles.AdvanceLarge(fixture.Frame, fixture.Address, Resources(), fixture.Inputs);
        NativeBattleModulePoolTask emitter = new NativeBattleEffectHierarchy(initialized.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(unchecked((ushort)-1000), emitter.ReadWord(0x1E));
        Assert.Equal((ushort)(1256 / duration), emitter.ReadWord(0x42));
        NativeBattleCuraParticleStep emitted = NativeBattleCuraParticles.AdvanceLarge(initialized.Frame, fixture.Address, Resources(), fixture.Inputs);
        NativeBattleModulePoolTask child = Assert.Single(emitted.Frame.Effect.Pools[3].Slots.Where(item => item.Callback == spriteCallback));
        emitter = new NativeBattleEffectHierarchy(emitted.Frame.Effect.Pools).Read(fixture.Address);
        if (overwritesRandomY)
        {
            Assert.Equal(emitter.ReadWord(0x1E), child.ReadWord(0x1E));
            Assert.Equal(4096u, child.ReadDword(0x30));
        }
        else
        {
            NativeCrtRandomState random = fixture.Inputs.CrtRandom.Draw(out ushort yaw);
            random = random.Draw(out ushort pitch);
            random = random.Draw(out ushort distance);
            NativeBattleVector offset = NativeBattlePoseMatrices.RotateVector(new((short)(pitch & 0xFFF), (short)(yaw & 0xFFF), 0), new(0, 0, (short)(distance % 512)));
            Assert.Equal(unchecked((ushort)(emitter.ReadWord(0x1E) - 384 + offset.Y)), child.ReadWord(0x1E));
            Assert.Equal(0u, child.ReadDword(0x30));
        }
        NativeBattleCuraFrame restored = JsonSerializer.Deserialize<NativeBattleCuraFrame>(JsonSerializer.Serialize(emitted.Frame))!;
        var next = NativeBattleCuraParticles.AdvanceLarge(emitted.Frame, fixture.Address, Resources(), fixture.Inputs with { CrtRandom = emitted.CrtRandomAfter });
        var clone = NativeBattleCuraParticles.AdvanceLarge(restored, fixture.Address, Resources(), fixture.Inputs with { CrtRandom = emitted.CrtRandomAfter });
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(clone));
    }

    [Fact]
    public void Copied_actor_hides_before_first_draw_and_restores_at_counter_eighteen_before_drawing_again()
    {
        var fixture = Fixture(0x88BBD0);
        NativeBattleCuraFrame frame = fixture.Frame;
        List<NativeBattleCuraOperation> operations = [];
        NativeBattleCuraParticleInputs inputs = fixture.Inputs with { Execute = (current, operation) =>
        {
            operations.Add(operation);
            return current;
        } };
        NativeBattleCuraParticleStep first = NativeBattleCuraParticles.AdvanceLarge(frame, fixture.Address, Resources(), inputs);
        Assert.IsType<NativeBattleCuraActorInitializeOperation>(operations[0]);
        Assert.True(Assert.IsType<NativeBattleCuraActorVisibilityOperation>(operations[1]).Hidden);
        NativeBattleCuraActorDrawOperation draw = Assert.IsType<NativeBattleCuraActorDrawOperation>(operations[2]);
        Assert.Equal((short)-1536, draw.VerticalOffset);
        Assert.Equal(new NativeBattleVector(100, -1536, -724), draw.Center);
        NativeBattleEffectHierarchy hierarchy = new(first.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, 18));
        frame = first.Frame with { Effect = first.Frame.Effect with { Pools = hierarchy.Pools } };
        operations.Clear();
        NativeBattleCuraParticleStep restored = NativeBattleCuraParticles.AdvanceLarge(frame, fixture.Address, Resources(), inputs);
        Assert.False(Assert.IsType<NativeBattleCuraActorVisibilityOperation>(Assert.Single(operations)).Hidden);
        Assert.Equal(2, restored.Result);
        Assert.Equal((byte)0, restored.Frame.Effect.Pools[0].Slots[0].ReadByte(0x63));
    }

    private static NativeBattleCuraResources Resources() => new(new("skipped-registration", []), [], 0x4942D) { EmissionSchedules = Enumerable.Range(0, 3).Select(_ => Enumerable.Repeat((short)1, 30).ToImmutableArray()).ToImmutableArray() };

    private static (NativeBattleCuraFrame Frame, uint Address, NativeBattleCuraParticleInputs Inputs) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 22, 25, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(25, 24, new(25, 0x889D60, 0x889D40), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(NativeBattleEffectMemory.FromColdProcess(), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, 0x889F40, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = first.Hierarchy;
        hierarchy = hierarchy.Write(first.Address, hierarchy.Read(first.Address).WriteWord(0x30, 100).WriteWord(0x34, 300).WriteWord(0x38, 400).WriteWord(0x3C, 600).WriteWord(0x4A, unchecked((ushort)-800)));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(2, callback, 200, first.Address, (_, _, _) => new(0, 3));
        frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Pools } };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCuraParticleInputs inputs = new(new(packets, index => new(index, 0) { Transform = new(new(0, 0, 0), new(0, -200, 0), 0, 0, 4096) }, _ => throw new InvalidOperationException("No new pose is sampled by these callbacks.")), NativeCrtRandomState.FromColdProcess(), (current, _) => current) { Projection = new(camera, 216, 160, 108) };
        return (frame, child.Address, inputs);
    }
}
