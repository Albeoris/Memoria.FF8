using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167ParticleTests
{
    [Fact]
    public void Paused_callback_still_publishes_current_bone_history_and_view_without_birth_clock_or_CRT()
    {
        List<(byte Selector, short Distance)> calls = [];
        NativeBattleEffect167Frame frame = Frame(1);
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceParticles(frame, ChildBank, null!, Inputs(0x201, calls));
        Assert.Equal(new (byte, short)[] { (40, 0), (40, 4096) }, calls);
        Assert.Equal(1, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(1u, step.Frame.State.ReadDword(FirstHistoryAddress + 20));
        Assert.Equal(BitConverter.GetBytes((short)100), step.Frame.State.Read(FirstHistoryAddress + 24, 2));
        Assert.Equal(0u, step.Frame.State.ReadDword(FirstRecordsAddress));
        Assert.NotNull(step.Frame.Memory.ScriptedLighting!.LightMatrix);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Births_follow_existing_scan_consume_eight_CRT_each_and_copy_retained_sample_W()
    {
        NativeBattleEffect167Frame frame = Frame(1);
        for (int sample = 0; sample < 6; sample++)
        {
            frame = frame with { State = frame.State.WriteWord(FirstSamplesAddress + (uint)(sample * 8 + 6), 0xCAFE) };
        }
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceParticles(frame, ChildBank, null!, Inputs(0, []));
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 40; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Frame.Random);
        for (int index = 0; index < 5; index++)
        {
            uint row = FirstRecordsAddress + (uint)(index * FirstRecordStride);
            Assert.Equal(1u, step.Frame.State.ReadDword(row));
            Assert.Equal((ushort)0, step.Frame.State.ReadWord(row + 4));
            Assert.Equal((ushort)0xCAFE, step.Frame.State.ReadWord(row + 14));
        }
        Assert.Equal(0u, step.Frame.State.ReadDword(FirstRecordsAddress + 5 * FirstRecordStride));
        Assert.Equal(2, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(0, step.Result);
        NativeBattleEffect167Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect167Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Frame)))!);
        Assert.Equal(step.Frame.State.Read(FirstRecordsAddress, 120), restored.State.Read(FirstRecordsAddress, 120));
        Assert.Equal(step.Frame.Random, restored.Random);
    }

    [Fact]
    public void Full_marker_bank_is_checked_before_random_and_retirement_uses_existing_survivors()
    {
        NativeBattleEffect167Frame frame = Frame(3);
        for (int index = 0; index < FirstRecordCount; index++)
        {
            frame = frame with { State = frame.State.WriteDword(FirstRecordsAddress + (uint)(index * FirstRecordStride), 2) };
        }
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceParticles(frame, ChildBank, null!, Inputs(0, []));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(2, step.Result);
        Assert.Equal(4, Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal(2u, step.Frame.State.ReadDword(FirstRecordsAddress));
    }

    [Fact]
    public void Normalization_and_history_fullcopies_preserve_unwritten_W_knownness()
    {
        NativeBattleEffect167Frame frame = Frame(0);
        NativeBattleEffect167Step step = NativeBattleEffect167Lower.AdvanceParticles(frame, ChildBank, null!, Inputs(0x201, []));
        NativeBattleEffectArena history = step.Frame.State.ReadView(FirstHistoryAddress, 20);
        Assert.Equal(BitConverter.GetBytes((short)4096), history.Read(12, 2));
        Assert.Throws<NotSupportedException>(() => history.Read(10, 2));
        Assert.Throws<NotSupportedException>(() => history.Read(18, 2));
    }

    private static NativeBattleEffect167Frame Frame(short age)
    {
        NativeBattleEffect167Frame frame = NativeBattleEffect167ControllerTests.Frame(age, 0x5E2F30) with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleEffectArena previous = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(1u)).Write(4, BitConverter.GetBytes((short)40)).Write(12, BitConverter.GetBytes((short)4096));
        return frame with { State = frame.State.WriteView(FirstHistoryAddress, previous, 20) };
    }

    private static NativeBattleEffect167Inputs Inputs(uint flags, List<(byte Selector, short Distance)> calls)
    {
        return new NativeBattleEffect167ControllerTests.Harness().Inputs() with
        {
            ReadFlags = _ => flags,
            ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0),
            Point502170 = (frame, actor, selector, distance, retained) =>
            {
                calls.Add((selector, distance));
                return new(frame, retained.Write(0, BitConverter.GetBytes((short)(distance == 0 ? 0 : 100))).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes((short)0)));
            }
        };
    }
}
