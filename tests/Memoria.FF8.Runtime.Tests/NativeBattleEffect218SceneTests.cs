using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218SceneTests
{
    [Theory]
    [InlineData(510, 0x02606060u, 0u)]
    [InlineData(518, 0x02303030u, 2048u)]
    [InlineData(525, 0x02060606u, 3840u)]
    public void Scene_fade_preserves_RGB_code_and_publishes_the_actual_FAR_and_DPCS_controls(short age, uint expected, uint cue)
    {
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        NativeBattleEffect218State state = NativeBattleEffect218State.FromColdProcess().WriteWord(StreamedModelAddress + 0x26, 0x1234).WriteWord(StreamedModelAddress + 0x2C, 0x5678);
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state);
        frame = FadeScene633F39(frame, age, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(expected, frame.State.ReadDword(StreamedModelAddress + 0x28));
        Assert.Equal((ushort)0x1234, frame.State.ReadWord(StreamedModelAddress + 0x26));
        Assert.Equal((ushort)0x5678, frame.State.ReadWord(StreamedModelAddress + 0x2C));
        Assert.Equal(cue, frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x02606060u, frame.Memory.ScriptedColorRegisters.Material);
        Assert.Equal(0u, frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0u, frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0u, frame.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
    }

    [Fact]
    public void Camera_displacement_is_written_before_pause_and_retires_at_eight_real_updates()
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(0x634B90);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 40)) };
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect218State.FromColdProcess());
        uint address = pool.BankAddress + (uint)(slot * pool.Stride);
        List<short> writes = [];
        NativeBattleEffect218Inputs inputs = new(_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            writes.Add(Assert.IsType<NativeBattleEffect218CameraWord1D97712>(operation).Value);
            return current;
        });
        var paused = AdvanceCamera634B90(frame, address, 0x200, inputs);
        Assert.Equal((short)-40, Assert.Single(writes));
        Assert.Equal((ushort)0, Read(paused.Frame, address).ReadWord(12));
        frame = paused.Frame;
        for (int age = 0; age < 8; age++)
        {
            var actual = AdvanceCamera634B90(frame, address, 0, inputs);
            Assert.Equal(age == 7 ? 2 : 0, actual.Result);
            frame = actual.Frame;
        }
        Assert.Equal((ushort)8, Read(frame, address).ReadWord(12));
        Assert.Equal(9, writes.Count);
    }
}
