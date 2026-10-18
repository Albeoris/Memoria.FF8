using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175ModelColorsTests
{
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private const uint Normal = 0xABBF6D25;
    private const uint Transition = 0x71A17F35;

    [Theory]
    [InlineData(0x5D27C0, 1)]
    [InlineData(0x5D27C0, 0x200)]
    [InlineData(0x5D3030, 1)]
    [InlineData(0x5D3030, 0x200)]
    public void Pause_preserves_counter_and_flags_but_keeps_actual_color_and_fadeout_alpha_writes(int callback, int pause)
    {
        NativeBattleModulePoolTask task = Task((uint)callback, 0, 8, 0x800);
        ImmutableArray<NativeBattleGraphicsModel> models = Models(0xA806);
        NativeBattleHoly175ModelColorStep step = Advance(task, models, (uint)pause);
        Assert.Equal(task, step.Task);
        Assert.Equal(0, step.Result);
        Assert.Equal(models[4].Flags0, step.Models[4].Flags0);
        Assert.Equal(callback == 0x5D27C0 ? (byte?)77 : (byte?)0, step.Models[4].Alpha7);
        Assert.Equal(callback == 0x5D27C0 ? 0x32000000u : (Normal & 0xFFFFFFu) | 0x32000000u, step.Models[4].Color44);
        Assert.Equal(callback == 0x5D27C0 ? 0x02000000u : ((Transition & 0xFEFEFEu) >> 1) | 0x02000000u, step.Models[4].Color40);
        for (int index = 0; index < 7; index++)
        {
            if (index != 4)
            {
                Assert.Same(models[index], step.Models[index]);
            }
        }
    }

    [Theory]
    [InlineData(0x5D27C0, 0xA806, 0xA802)]
    [InlineData(0x5D3030, 0xA002, 0xA802)]
    public void First_unpaused_callback_updates_only_native_visibility_and_override_bits(int callback, int flags, int expected)
    {
        NativeBattleHoly175ModelColorStep step = Advance(Task((uint)callback, 0, 8, 0), Models((ushort)flags), 0);
        Assert.Equal((ushort)expected, step.Models[4].Flags0);
        Assert.Equal((ushort)1, step.Task.ReadWord(0xC));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(0x5D27C0, 0)]
    [InlineData(0x5D27C0, 0x800)]
    [InlineData(0x5D3030, 0)]
    [InlineData(0x5D3030, 0x800)]
    public void Terminal_write_restores_captured_override_and_preserves_last_fadeout_colors(int callback, int captured)
    {
        NativeBattleModulePoolTask task = Task((uint)callback, 7, 8, captured);
        NativeBattleHoly175ModelColorStep paused = Advance(task, Models(0xA802), 0x201);
        NativeBattleHoly175ModelColorStep step = Advance(task, Models(0xA802), 0);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)8, step.Task.ReadWord(0xC));
        Assert.Equal((ushort)(0xA002 | captured | (callback == 0x5D3030 ? 4 : 0)), step.Models[4].Flags0);
        Assert.Equal(paused.Models[4].Color44, step.Models[4].Color44);
        Assert.Equal(callback == 0x5D27C0 ? Transition : paused.Models[4].Color40, step.Models[4].Color40);
        Assert.Equal((byte?)0, step.Models[4].Alpha7);
    }

    [Theory]
    [InlineData(0x5D27C0, -2, 3)]
    [InlineData(0x5D3030, -2, 3)]
    [InlineData(0x5D27C0, -1, -3)]
    [InlineData(0x5D3030, -1, -3)]
    [InlineData(0x5D27C0, 32767, 32767)]
    public void Signed_division_WORD_wrap_and_independent_JSON_next_callback_preserve_retained_models(int callback, int counter, int duration)
    {
        NativeBattleModulePoolTask task = Task((uint)callback, counter, duration, 0x800);
        ImmutableArray<NativeBattleGraphicsModel> models = Models(0x2806);
        NativeBattleHoly175ModelColorStep paused = Advance(task, models, 1);
        int quotient = (counter << 12) / duration;
        int factor = callback == 0x5D27C0 ? quotient : 4096 - quotient;
        Assert.Equal(Colors.Interpolate(0x32000000, factor, Normal), paused.Models[4].Color44);
        Assert.Equal((Colors.Interpolate(0, factor, (Transition & 0xFEFEFEu) >> 1) & 0xFFFFFFu) | 0x02000000u, paused.Models[4].Color40);
        NativeBattleHoly175ModelColorStep cold = JsonSerializer.Deserialize<NativeBattleHoly175ModelColorStep>(JsonSerializer.Serialize(paused))!;
        NativeBattleHoly175ModelColorStep next = Advance(paused.Task, paused.Models, 0);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Advance(cold.Task, cold.Models, 0)));
        Assert.Equal(unchecked((ushort)(counter + 1)), next.Task.ReadWord(0xC));
        if (counter == 32767)
        {
            Assert.Equal(0, next.Result);
        }
    }

    [Fact]
    public void Missing_model_owner_and_zero_duration_fail_without_invented_defaults()
    {
        Assert.Throws<InvalidDataException>(() => Advance(Task(0x5D27C0, 0, 0, 0), Models(2), 0));
        Assert.Throws<InvalidDataException>(() => Advance(Task(0x5D27C0, 0, 8, 0).WriteWord(0xE, 7), Models(2), 0));
        Assert.Throws<InvalidDataException>(() => Advance(Task(0x5D27C0, 0, 8, 0), Models(2).RemoveAt(6), 0));
    }

    private static NativeBattleHoly175ModelColorStep Advance(NativeBattleModulePoolTask task, ImmutableArray<NativeBattleGraphicsModel> models, uint flags) => NativeBattleHoly175ModelColors.Advance(task, models, flags, Normal, Transition, Colors);
    private static ImmutableArray<NativeBattleGraphicsModel> Models(ushort flags) => Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index == 4 ? flags : (ushort)2) { Alpha7 = 77, Color40 = 0x12987654, Color44 = 0x34FEDCBA, Sequence = 9 }).ToImmutableArray();
    private static NativeBattleModulePoolTask Task(uint callback, int counter, int duration, int captured) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x18].ToImmutableArray()).WriteWord(0xC, unchecked((ushort)counter)).WriteWord(0xE, 4).WriteWord(0x18, unchecked((ushort)duration)).WriteWord(0x1A, unchecked((ushort)captured));
}
