using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect80ParticleTests
{
    [Fact]
    public void Hidden_sprite_returns_before_allocating_or_reading_sources()
    {
        NativeBattleEffect80Frame frame = Frame(0x7AE7D0);
        Assert.Same(frame, NativeBattleEffect80Sprite.DrawTask(frame, 0x200, null!, null!));
    }

    [Fact]
    public void Orbit_birth_consumes_four_crt_values_then_increments_y_angle_and_native_age()
    {
        NativeBattleEffect80Frame frame = Frame(0x7AF030);
        NativeCrtRandomState random = frame.Random;
        ushort[] values = new ushort[4];
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out values[index]);
        }
        NativeBattleEffect80Step result = NativeBattleEffect80Particles.Advance7AF030(frame, 0x200, Resources(), null!);
        NativeBattleModulePoolTask task = NativeBattleEffect80Controller.Read(result.Frame, 0x200);
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal((ushort)(values[0] & 0xFFF), task.ReadWord(0x40));
        Assert.Equal(unchecked((ushort)((values[1] & 0xFFF) + 0x40)), task.ReadWord(0x42));
        Assert.Equal((ushort)(values[2] & 0xFFF), task.ReadWord(0x44));
        Assert.Equal((ushort)((values[3] & 0xFF) + 0x180), task.ReadWord(0x9E));
        Assert.Equal(1, task.ReadByte(0x29));
        Assert.Equal(1, task.ReadWord(0x24));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Sprite_end_is_signed_inclusive_and_hides_only_after_last_frame()
    {
        NativeBattleEffect80Frame frame = Frame(0x7AE7D0);
        NativeBattleModulePoolTask task = NativeBattleEffect80Controller.Read(frame, 0x200).WriteByte(0x29, 1).WriteWord(0x50, 10).WriteWord(0x52, 11);
        frame = NativeBattleEffect80Controller.Write(frame, 0x200, task);
        frame = NativeBattleEffect80Particles.Advance7AE7D0(frame, 0x200, null!, null!).Frame;
        task = NativeBattleEffect80Controller.Read(frame, 0x200);
        Assert.Equal(11, task.ReadWord(0x50));
        Assert.Equal(1, task.ReadByte(0x29));
        frame = NativeBattleEffect80Particles.Advance7AE7D0(frame, 0x200, null!, null!).Frame;
        task = NativeBattleEffect80Controller.Read(frame, 0x200);
        Assert.Equal(11, task.ReadWord(0x50));
        Assert.Equal(2, task.ReadByte(0x29));
        Assert.Equal(5, task.ReadByte(0x26) & 5);
    }

    private static NativeBattleEffect80Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 160, 1);
        pool.WriteSlot(0, pool.Slots[0].WriteByte(0x26, 4).WriteByte(0x28, 1).UpdateHeader(pool.Slots[0].Flags, pool.Slots[0].State, pool.Slots[0].Next, callback));
        NativeBattleOriginalEffectState effect = new(80, 79, new(80, NativeBattleEffect80Profile.Entry, NativeBattleEffect80Profile.Initializer), null, null) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), NativeBattleEffect80State.FromColdProcess());
    }
    private static NativeBattleEffect80Resources Resources() => new(new("texture", []), new("camera", []), new("vibration", []), ImmutableDictionary<uint, NativeBattleEffect80NodeResourceBank>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, NativeBattleEffect80Profile.TargetAddress);
}
