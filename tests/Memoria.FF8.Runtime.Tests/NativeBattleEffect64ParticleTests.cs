using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect64LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64ParticleTests
{
    [Fact]
    public void Fixed_overlay_capture_preserves_original_packet_and_holds_screen_operation_discrete()
    {
        NativeBattleGfGeometryBatch? plain = null;
        NativeBattleGfGeometryBatch? captured = null;
        NativeBattleEffect64Inputs a = Inputs() with
        {
            Execute = (frame, operation) =>
            {
                plain = Assert.IsType<NativeBattleEffect64DeliverBatch>(operation).Batch;
                return frame;
            }
        };
        NativeBattleEffect64Inputs b = Inputs() with
        {
            Execute = (frame, operation) =>
            {
                captured = Assert.IsType<NativeBattleEffect64DeliverBatch>(operation).Batch;
                return frame;
            }
        };
        _ = NativeBattleEffect64Particles.Overlay6E6B60(Frame(12, 34, 56), Script, a);
        _ = NativeBattleEffect64Particles.Overlay6E6B60(Frame(12, 34, 56), Script, b, new(64, 1, 0, 1, 0));
        NativeBattleGfGeometryAttempt x = Assert.Single(plain!.Attempts);
        NativeBattleGfGeometryAttempt y = Assert.Single(captured!.Attempts);
        Assert.Equal(x.Data.ToArray(), y.Data.ToArray());
        Assert.Equal(x.WriteMask.ToArray(), y.WriteMask.ToArray());
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(y.Primitive!.PilotGeometry);
        Assert.False(visual.Continuous);
        Assert.True(visual.ScreenSpace);
        Assert.Equal(new System.Numerics.Vector3(320, 216, 0), visual.D);
    }

    [Fact]
    public void Zero_range_does_not_advance_CRT_and_negative_ranges_use_signed_shift()
    {
        NativeBattleEffect64Frame frame = Frame();
        (NativeBattleEffect64Frame same, int constant) = NativeBattleEffect64Particles.Random6E6B30(frame, 0, -250);
        Assert.Same(frame, same);
        Assert.Equal(-250, constant);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        (NativeBattleEffect64Frame result, int sampled) = NativeBattleEffect64Particles.Random6E6B30(frame, -500, 250);
        Assert.Equal(expected, result.Random);
        Assert.Equal(250 + ((-500 * value) >> 15), sampled);
    }

    [Fact]
    public void Batch_failure_observes_pose_and_bone_before_allocation_but_does_not_sample()
    {
        short[] words = new short[24];
        words[1] = 1;
        words[5] = 4;
        words[6] = 4000;
        NativeBattleEffect64Frame frame = Frame(words);
        (frame, _) = Append(frame, ParticleDescriptor, 0x6E67F0);
        List<string> calls = [];
        (NativeBattleEffect64Frame result, uint next) = NativeBattleEffect64Particles.Birth6E68C0(frame, Script, 0x1D975CC, Row, Resources(), Inputs(calls));
        Assert.Equal(Script + 48, next);
        Assert.Equal(new[] { nameof(NativeBattleEffect64ApplyOffsetPose662C00), "bone:1:0" }, calls);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Batch_birth_samples_only_nonzero_ranges_in_position_then_velocity_order()
    {
        short[] words = new short[24];
        words[1] = 1;
        words[3] = 1;
        words[4] = 4096;
        words[5] = 1;
        words[6] = 500;
        words[7] = -250;
        words[9] = 7;
        words[11] = -8;
        words[12] = 100;
        words[13] = -50;
        words[15] = 3;
        NativeBattleEffect64Frame frame = Frame(words);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort x).Draw(out ushort vx);
        (NativeBattleEffect64Frame result, uint next) = NativeBattleEffect64Particles.Birth6E68C0(frame, Script, 0x1D975CC, Row, Resources(), Inputs());
        NativeBattleModulePoolTask particle = Read(result, Common + ParticleOffset);
        Assert.Equal(expected, result.Random);
        Assert.Equal(unchecked((ushort)(10 - 250 + ((500 * x) >> 15))), particle.ReadWord(12));
        Assert.Equal(27, particle.ReadWord(14));
        Assert.Equal(22, particle.ReadWord(16));
        Assert.Equal(unchecked((byte)(-50 + ((100 * vx) >> 15))), particle.ReadByte(0x16));
        Assert.Equal(3, particle.ReadByte(0x17));
        Assert.Equal(1, particle.ReadByte(0x15));
        Assert.Equal(Script + 48, next);
    }

    [Fact]
    public void Direct_failure_reads_only_the_flag_and_advances_eleven_words()
    {
        NativeBattleEffect64Frame frame = Frame();
        (frame, _) = Append(frame, ParticleDescriptor, 0x6E67F0);
        frame = frame with { State = frame.State.ReplaceScriptSource(Script, frame.State.ReadScriptSource(Script).MarkUnknown(2, 20)) };
        List<string> calls = [];
        (NativeBattleEffect64Frame result, uint next) = NativeBattleEffect64Particles.Birth6E6710(frame, Script, 0, Row, Inputs(calls));
        Assert.Equal(Script + 22, next);
        Assert.Empty(calls);
        Assert.Equal(frame.Random, result.Random);
    }
}
