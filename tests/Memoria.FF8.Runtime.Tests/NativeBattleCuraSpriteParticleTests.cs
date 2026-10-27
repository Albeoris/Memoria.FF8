using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraSpriteParticleTests
{
    [Fact]
    public void Burst_consumes_one_base_angle_and_eight_jitters_before_its_normal_and_ground_draws()
    {
        var fixture = Fixture(0x88A070);
        List<NativeBattleCuraOperation> operations = [];
        NativeBattleCuraParticleStep step = Advance(fixture.Frame, fixture.Address, fixture.Inputs, operations);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom.Draw(out ushort baseAngle);
        NativeBattleModulePoolTask[] children = step.Frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x88A2D0).ToArray();
        Assert.Equal(8, children.Length);
        for (int index = 0; index < children.Length; index++)
        {
            expected = expected.Draw(out ushort jitter);
            Assert.Equal((ushort)(((baseAngle + index * 512) & 0xFFF) + (jitter & 127)), children[index].ReadWord(0x42));
            Assert.Equal((short)-1536, unchecked((short)children[index].ReadWord(0x1E)));
        }
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal(2, operations.Count);
        NativeBattleModuleDraw normal = Assert.IsType<NativeBattleCuraSpriteOperation>(operations[0]).Draw;
        NativeBattleModuleDraw ground = Assert.IsType<NativeBattleCuraSpriteOperation>(operations[1]).Draw;
        Assert.Equal(new NativeBattleModuleAnchor(100, -1536, 300, 0), normal.Anchor);
        Assert.Equal((ushort)0, normal.SpriteFlags);
        Assert.Equal(0x1610948u, normal.Resource!.Address);
        Assert.Equal(new NativeBattleModuleAnchor(100, 0, 300, 0), ground.Anchor);
        Assert.Equal((ushort)4, ground.SpriteFlags);
        Assert.Equal(0x202020u, ground.SpriteColor);
    }

    [Fact]
    public void Star_uses_exact_initial_acceleration_then_appends_one_odd_counter_trail_with_three_spherical_random_draws()
    {
        var fixture = Fixture(0x88A2D0);
        NativeBattleCuraParticleStep step = Advance(fixture.Frame, fixture.Address, fixture.Inputs, []);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom.Draw(out _).Draw(out ushort vertical).Draw(out ushort speed);
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        int initialSpeed = (speed & 31) + 80;
        Assert.Equal((short)(initialSpeed - initialSpeed / 16), unchecked((short)task.ReadWord(0x7C)));
        Assert.Equal((short)((vertical & 63) - 48 + 10), unchecked((short)task.ReadWord(0x5A)));
        NativeBattleCuraParticleStep next = Advance(step.Frame, fixture.Address, fixture.Inputs with { CrtRandom = expected }, []);
        expected = expected.Draw(out _).Draw(out _).Draw(out _);
        Assert.Equal(expected, next.CrtRandomAfter);
        NativeBattleModulePoolTask child = Assert.Single(next.Frame.Effect.Pools[3].Slots.Where(candidate => (candidate.Flags & 1) != 0 && candidate.Callback == 0x88A4E0));
        task = new NativeBattleEffectHierarchy(next.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(task.ReadWord(0x1E), child.ReadWord(0x1E));
        Assert.Equal(4096u, child.ReadDword(0x30));
        Assert.Equal((byte)1, task.ReadByte(0x28));
    }

    [Fact]
    public void Double_sprite_selects_actual_pair_and_ground_rotation_scale_then_json_continues_without_an_extra_random_draw()
    {
        var fixture = Fixture(0x88B950);
        List<NativeBattleCuraOperation> operations = [];
        NativeBattleCuraParticleStep step = Advance(fixture.Frame, fixture.Address, fixture.Inputs, operations);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom.Draw(out ushort pair).Draw(out ushort angle);
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModuleDraw normal = Assert.IsType<NativeBattleCuraSpriteOperation>(operations[0]).Draw;
        NativeBattleModuleDraw ground = Assert.IsType<NativeBattleCuraSpriteOperation>(operations[1]).Draw;
        Assert.Equal((pair & 1) != 0 ? 0x161079Cu : 0x1610C00u, normal.Resource!.Address);
        Assert.Equal((pair & 1) != 0 ? 0x1610DACu : 0x1610F58u, ground.Resource!.Address);
        Assert.Equal(-512, normal.DepthOffset);
        Assert.Equal(0x303030u, ground.SpriteColor);
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(1024, (short)((angle + 64) & 0xFFF), 0)).Select(word => unchecked((short)(word * 2))).ToArray(), ground.SpriteWorldRotation.ToArray());
        NativeBattleCuraFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuraFrame>(NativeBattleTestSnapshots.Json(step.Frame))!;
        var next = Advance(step.Frame, fixture.Address, fixture.Inputs with { CrtRandom = expected }, []);
        var independent = Advance(restored, fixture.Address, fixture.Inputs with { CrtRandom = expected }, []);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(independent));
        Assert.Equal(expected, next.CrtRandomAfter);
    }

    [Theory]
    [InlineData(0x88A4E0u, 0)]
    [InlineData(0x88CDA0u, -1024)]
    public void Eleven_frame_rising_sprites_use_their_native_depth_offset_and_finish_on_the_twelfth_callback(uint callback, int depthOffset)
    {
        var fixture = Fixture(callback);
        NativeBattleCuraFrame frame = fixture.Frame;
        NativeCrtRandomState random = fixture.Inputs.CrtRandom;
        for (int index = 0; index < 12; index++)
        {
            List<NativeBattleCuraOperation> operations = [];
            NativeBattleCuraParticleStep step = Advance(frame, fixture.Address, fixture.Inputs with { CrtRandom = random }, operations);
            Assert.Equal(index == 11 ? 2 : 0, step.Result);
            if (index < 11)
            {
                NativeBattleModuleDraw draw = Assert.IsType<NativeBattleCuraSpriteOperation>(Assert.Single(operations)).Draw;
                Assert.Equal(depthOffset, draw.DepthOffset);
                Assert.Equal(0x1610AD4u, draw.Resource!.Address);
                Assert.Equal((ushort)index, draw.Phase);
            }
            else
            {
                Assert.Empty(operations);
            }
            frame = step.Frame;
            random = step.CrtRandomAfter;
        }
        Assert.Equal(fixture.Inputs.CrtRandom.Draw(out _), random);
    }

    [Fact]
    public void Star_counter_sixteen_switches_to_the_actual_seven_frame_bank_and_stops_appending_trails()
    {
        var fixture = Fixture(0x88A2D0);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteByte(0x29, 1).WriteWord(0x24, 16).WriteWord(0x50, 1).WriteWord(0x52, 1).WriteDword(0x4C, 0x16109BC).WriteWord(0x1E, unchecked((ushort)-30000));
        hierarchy = hierarchy.Write(fixture.Address, task);
        NativeBattleCuraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        List<NativeBattleCuraOperation> operations = [];
        NativeBattleCuraParticleStep step = Advance(frame, fixture.Address, fixture.Inputs, operations);
        task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Equal((byte)16, (byte)(task.ReadByte(0x26) & 16));
        Assert.Equal((ushort)6, task.ReadWord(0x52));
        Assert.Equal(0x1610A10u, Assert.IsType<NativeBattleCuraSpriteOperation>(operations[0]).Draw.Resource!.Address);
        var next = Advance(step.Frame, fixture.Address, fixture.Inputs, []);
        Assert.DoesNotContain(next.Frame.Effect.Pools[3].Slots, child => (child.Flags & 1) != 0 && child.Callback == 0x88A4E0);
        Assert.Equal(fixture.Inputs.CrtRandom, next.CrtRandomAfter);
    }

    private static NativeBattleCuraParticleStep Advance(NativeBattleCuraFrame frame, uint address, NativeBattleCuraParticleInputs inputs, List<NativeBattleCuraOperation> operations) => NativeBattleCuraParticles.AdvanceSprite(frame, address, Resources(), inputs with { Execute = (current, operation) =>
    {
        operations.Add(operation);
        return current;
    } });

    private static (NativeBattleCuraFrame Frame, uint Address, NativeBattleCuraParticleInputs Inputs) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 22, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(25, 24, new(25, 0x889D60, 0x889D40), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(NativeBattleEffectMemory.FromColdProcess(), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, 0x889F40, 88, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        hierarchy = first.Hierarchy;
        hierarchy = hierarchy.Write(first.Address, hierarchy.Read(first.Address).WriteWord(0x30, 100).WriteWord(0x34, 300));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(3, callback, 132, first.Address, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy;
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteWord(0x1C, 100).WriteWord(0x1E, unchecked((ushort)-2000)).WriteWord(0x20, 300));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        return (frame, child.Address, new(new(packets, _ => throw new InvalidOperationException("Sprite callbacks use retained anchors."), _ => throw new InvalidOperationException("Sprite callbacks do not sample body poses.")), NativeCrtRandomState.FromColdProcess(), (current, _) => current));
    }

    private static NativeBattleCuraResources Resources() => new(new("unused-sprite-registration", []), [Sprite(0x161079C, 16), Sprite(0x1610948, 4), Sprite(0x16109BC, 2), Sprite(0x1610A10, 7), Sprite(0x1610AD4, 11), Sprite(0x1610C00, 16), Sprite(0x1610DAC, 16), Sprite(0x1610F58, 16)], 0x4942D);

    private static NativeBattleModuleResource Sprite(uint address, int count)
    {
        int offset = 12 + 2 * count;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int index = 0; index <= count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), (ushort)offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
