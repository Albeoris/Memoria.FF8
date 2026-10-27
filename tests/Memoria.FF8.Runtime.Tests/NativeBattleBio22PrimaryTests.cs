using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22PrimaryTests
{
    [Theory]
    [InlineData(-32768, 11264)]
    [InlineData(-4096, 11264)]
    [InlineData(-1, 2816)]
    [InlineData(0, 2816)]
    [InlineData(432, 2816)]
    [InlineData(2560, 11264)]
    [InlineData(32767, 11264)]
    public void Initialization_uses_raw_center_anchor_capped_half_height_and_matrix_before_sound(short height, ushort expectedScale)
    {
        NativeBattleModulePoolTask task = Task().WriteDword(0x38, 0x12345678).WriteDword(0x3C, 0xBEEF2345).WriteDword(0x30, 0xDEADBEEF).WriteWord(0x6A, 0xCAFE);
        List<uint> appended = [];
        bool sound = false;
        NativeBattleBio22PrimaryInputs inputs = Inputs() with
        {
            CenterY = _ => -1000,
            HalfHeight = _ => height,
            Append = (current, callback, variant) =>
            {
                appended.Add(callback);
                Assert.Equal(callback == 0x89AAE0 ? (short?)0 : null, variant);
                return current.WriteByte(0x28, (byte)appended.Count);
            },
            Sound = current =>
            {
                sound = true;
                Assert.Equal((ushort)0xFC0, current.ReadWord(0x82));
                Assert.Equal((ushort)0xCAFE, current.ReadWord(0x6A));
                Assert.Equal((ushort)0, current.ReadWord(0x24));
                Assert.NotEqual((ushort)0, current.ReadWord(0x58));
                return current;
            }
        };
        NativeBattleBio22PrimaryStep step = NativeBattleBio22Primary.Advance89A8D0(task, inputs);
        Assert.Equal(new uint[] { 0x89B8A0, 0x89AAE0 }, appended);
        Assert.Equal(expectedScale, step.Task.ReadWord(0x86));
        Assert.Equal((ushort)0x5678, step.Task.ReadWord(0x78));
        Assert.Equal(0xBEEF2345u, step.Task.ReadDword(0x7C));
        Assert.Equal((byte)2, step.Task.ReadByte(0x28));
        Assert.Equal((ushort)1, step.Task.ReadWord(0x24));
        Assert.True(sound);
    }

    [Theory]
    [InlineData(1, 2, 0x89BA80, 0x89AAE0)]
    [InlineData(2, 4, 0x89AAE0, 0)]
    [InlineData(3, 6, 0x89AAE0, 0)]
    [InlineData(4, 8, 0x89AAE0, 0)]
    [InlineData(5, 10, 0x89AAE0, 0)]
    [InlineData(6, 30, 0x89CA70, 0x89C270)]
    [InlineData(7, 32, 0x89CD60, 0x89D020)]
    [InlineData(8, 34, 0x89D3D0, 0)]
    public void Native_counter_threshold_advances_one_phase_with_ordered_actual_append_services(byte phase, ushort threshold, uint first, uint second)
    {
        NativeBattleModulePoolTask task = Task().WriteByte(0x29, phase).WriteWord(0x24, (ushort)(threshold - 1));
        List<uint> appended = [];
        NativeBattleBio22PrimaryInputs inputs = Inputs() with
        {
            Append = (current, callback, _) =>
            {
                appended.Add(callback);
                return current;
            }
        };
        NativeBattleBio22PrimaryStep wait = NativeBattleBio22Primary.Advance89A8D0(task, inputs);
        Assert.Empty(appended);
        Assert.Equal(phase, wait.Task.ReadByte(0x29));
        NativeBattleBio22PrimaryStep step = NativeBattleBio22Primary.Advance89A8D0(wait.Task, inputs);
        Assert.Equal(second == 0 ? new[] { first } : new[] { first, second }, appended);
        Assert.Equal((byte)(phase + 1), step.Task.ReadByte(0x29));
    }

    [Fact]
    public void Contact_requires_exact_signal_one_and_holds_children_after_single_contact_with_JSON_continuation()
    {
        NativeBattleModulePoolTask task = Task().WriteByte(0x29, 9).WriteWord(0x24, 50).WriteWord(0x80, 2).WriteByte(0x28, 1);
        int contacts = 0;
        NativeBattleBio22PrimaryInputs inputs = Inputs() with
        {
            Contact = current =>
            {
                contacts++;
                return current;
            }
        };
        NativeBattleBio22PrimaryStep wait = NativeBattleBio22Primary.Advance89A8D0(task, inputs);
        Assert.Equal(0, contacts);
        NativeBattleBio22PrimaryStep step = NativeBattleBio22Primary.Advance89A8D0(wait.Task.WriteWord(0x80, 1), inputs);
        Assert.Equal(1, contacts);
        Assert.Equal(0u, step.Result);
        NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(step.Task))!;
        NativeBattleBio22PrimaryStep complete = NativeBattleBio22Primary.Advance89A8D0(restored.WriteByte(0x28, 0), inputs);
        Assert.Equal(1, contacts);
        Assert.Equal(2u, complete.Result);
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x89A8D0, new byte[0x8C - 12].ToImmutableArray());
    private static NativeBattleBio22PrimaryInputs Inputs() => new(task => task, _ => 0, _ => 0, (task, _, _) => task, task => task, task => task);
}
