using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325LateResetTests
{
    [Fact]
    public void Reset_clears_only_each_life_byte_and_module_mode_cursor_without_consuming_unknown_record_fields()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] records = Enumerable.Repeat((byte)0xA5, 160 * 16).ToArray();
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x4C5C, records).MarkUnknown(0x4C5C, 15);
        frame = frame with { Memory = frame.Memory with { Arena = arena, Diablos325 = frame.Memory.Diablos325! with { Mode51F0 = 3, ParticleCursor51F4 = 0x20DFAB8u + 0x564C, FarZ51DC = 40000 } } };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), 123, 160, 108), FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        NativeBattleDiablos325TailStep result = NativeBattleDiablos325LateReset.Reset65659F(frame, camera);
        Assert.Equal((byte)0, result.Frame.Memory.Diablos325!.Mode51F0);
        Assert.Equal(0x20DFAB8u + 0x4C5C, result.Frame.Memory.Diablos325!.ParticleCursor51F4);
        Assert.Equal(frame.Memory.Diablos325! with { Mode51F0 = 0, ParticleCursor51F4 = 0x20DFAB8u + 0x4C5C }, result.Frame.Memory.Diablos325);
        for (int index = 0; index < 160; index++)
        {
            Assert.Equal((byte)0, result.Frame.Memory.Arena!.Read(0x4C5C + index * 16 + 15, 1)[0]);
            if (index != 0)
            {
                Assert.Equal(Enumerable.Repeat((byte)0xA5, 15), result.Frame.Memory.Arena.Read(0x4C5C + index * 16, 15));
            }
        }
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(0x4C5C, 15));
        Assert.Equal(camera.WriteEffectGlobals(new(new(375, -8170, -25825), new(-175, -8111, -25776), 123, 17)), result.Camera);
        Assert.Same(camera.Projection, result.Camera.Projection);
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Dropping_descriptor_links_retains_every_slot_and_physical_byte_even_when_bank_is_reused_by_file_data()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2505190, 0x20DFAB8u + 0x3505C, 0xE8, 16);
        (pool, int index) = pool.Append(0x659960);
        pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index].WriteWord(0xC, 1234)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x3505C, Enumerable.Repeat((byte)0xAA, 64).ToArray()) } };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleDiablos325Frame result = NativeBattleDiablos325LateReset.ClearParticleLinks655567(frame);
        NativeBattleEffectTaskPool cleared = result.Effect.Pools.Single(item => item.DescriptorAddress == 0x2505190);
        cleared.Validate();
        Assert.Null(cleared.Head);
        Assert.Null(cleared.Tail);
        Assert.Equal(pool.Slots, cleared.Slots);
        Assert.Same(frame.Memory, result.Memory);
        Assert.Equal(Enumerable.Repeat((byte)0xAA, 64), result.Memory.Arena!.Read(0x3505C, 64));
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

    [Theory]
    [InlineData(217, -469, -9765, 4184)]
    [InlineData(218, -459, -9763, 4183)]
    [InlineData(236, 457, -9677, 4124)]
    public void Cosine_pan_keeps_target_fixed_and_rounds_native_eye_products_before_summing(int counter, short x, short y, short z)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 4000 } } };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 123, Roll = 17 };
        NativeBattleDiablos325TailStep result = NativeBattleDiablos325LateReset.Advance6565FA(frame, camera, (ushort)counter);
        Assert.Same(frame, result.Frame);
        Assert.Equal(camera.WriteEffectGlobals(new(new(x, y, z), new(-176, -8109, 3759), 123, 17)), result.Camera);
    }
    [Fact]
    public void Ready_stream_boundary_publishes_direct_WORD_camera_without_replacing_projection_or_effect_memory()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 40000 } } };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { Projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), 123, 160, 108), FocalLength = 123, Roll = 17, EyePaddingF6 = 7, TargetPaddingFE = 9 };
        NativeBattleDiablos325TailStep result = NativeBattleDiablos325LateReset.Reset6566B7(frame, camera);
        Assert.Same(frame, result.Frame);
        Assert.Equal(camera.WriteEffectGlobals(new(new(-838, -8389, -26700), new(39, -8496, -25771), 123, 17)), result.Camera);
        Assert.Same(camera.Projection, result.Camera.Projection);
    }

}
