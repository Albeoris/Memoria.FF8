using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22LaterParticleTests
{
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)-30000)]
    [InlineData((short)30000)]
    public void Placement_shares_Y_then_X_RotateShort_and_preserves_exact_two_vs_three_CRT_draws(short radius)
    {
        NativeBattleBio22ParticleFrame frame = Particle(0x89CE60);
        frame = frame with { Task = frame.Task.WriteWord(0x1C, 32000).WriteWord(0x1E, 32000).WriteWord(0x20, 32000).WriteWord(0x22, 0xBEEF) };
        NativeCrtRandomState after2 = frame.Random.Draw(out ushort yaw).Draw(out ushort pitch);
        NativeCrtRandomState after3 = after2.Draw(out ushort distance);
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), (short)(yaw & 0xFFF)), (short)(pitch & 0xFFF));
        short actualRadius = radius == 0 ? (short)1 : radius;
        NativeBattleVector fixedPoint = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, actualRadius));
        NativeBattleVector randomPoint = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, (short)(distance % actualRadius)));
        NativeBattleBio22ParticleFrame fixedFrame = NativeBattleBio22LaterParticles.Place89D280(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), radius);
        NativeBattleBio22ParticleFrame randomFrame = NativeBattleBio22LaterParticles.Place89CF30(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), radius);
        Assert.Equal(after2, fixedFrame.Random);
        Assert.Equal(after3, randomFrame.Random);
        Assert.Equal(unchecked((ushort)(32000 + fixedPoint.X)), fixedFrame.Task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)(32000 + fixedPoint.Y)), fixedFrame.Task.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)(32000 + fixedPoint.Z)), fixedFrame.Task.ReadWord(0x20));
        Assert.Equal(unchecked((ushort)(32000 + randomPoint.X)), randomFrame.Task.ReadWord(0x1C));
        Assert.Equal((ushort)0xBEEF, fixedFrame.Task.ReadWord(0x22));
        Assert.Equal((ushort)0xBEEF, randomFrame.Task.ReadWord(0x22));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Memory), NativeBattleTestSnapshots.Json(randomFrame.Memory));
    }

    [Fact]
    public void Placed_particle_replaces_inherited_position_then_remains_stationary_through_animation()
    {
        NativeBattleBio22ParticleFrame frame = Particle(0x89CE60);
        frame = frame with { Primary = frame.Primary.WriteWord(0x86, 4096).WriteWord(0x78, 31).WriteWord(0x7A, unchecked((ushort)-3000)).WriteWord(0x7C, 67).WriteWord(0x7E, 0xCAFE), Task = frame.Task.WriteWord(0x58, 300).WriteWord(0x60, 900) };
        NativeBattleBio22ParticleStep initial = NativeBattleBio22LaterParticles.Advance89CE60(frame, Draw);
        Assert.Equal(0x1615858u, initial.Frame.Task.ReadDword(0x4C));
        Assert.Equal((ushort)15, initial.Frame.Task.ReadWord(0x52));
        Assert.Equal((ushort)0xCAFE, initial.Frame.Task.ReadWord(0x22));
        NativeBattleBio22ParticleStep moving = NativeBattleBio22LaterParticles.Advance89CE60(initial.Frame, Draw);
        Assert.Equal(initial.Frame.Task.ReadDword(0x1C), moving.Frame.Task.ReadDword(0x1C));
        Assert.Equal(initial.Frame.Task.ReadDword(0x20), moving.Frame.Task.ReadDword(0x20));
        Assert.Equal(initial.Frame.Random, moving.Frame.Random);
        Assert.Equal((ushort)300, moving.Frame.Task.ReadWord(0x58));
        Assert.Equal((ushort)900, moving.Frame.Task.ReadWord(0x60));
    }

    [Fact]
    public void Decelerating_initial_velocity_uses_full_signed_difference_before_division_not_wrapped_WORD()
    {
        NativeBattleBio22ParticleFrame frame = Particle(0x89D120);
        frame = frame with { Primary = frame.Primary.WriteWord(0x86, 32767).WriteWord(0x78, unchecked((ushort)-32760)).WriteWord(0x7A, unchecked((ushort)-15000)).WriteWord(0x7C, 0) };
        NativeBattleBio22ParticleStep step = NativeBattleBio22LaterParticles.Advance89D120(frame, Draw);
        NativeCrtRandomState expectedRandom = frame.Random.Draw(out _).Draw(out _).Draw(out ushort value);
        int denominator = (value & 7) + 12;
        bool wrapDistinguishes = false;
        for (int axis = 0; axis < 3; axis++)
        {
            int difference = unchecked((short)step.Frame.Task.ReadWord(0x1C + axis * 2)) - unchecked((short)frame.Primary.ReadWord(0x78 + axis * 2));
            short velocity = (short)(difference / denominator);
            wrapDistinguishes |= velocity != unchecked((short)difference) / denominator;
            Assert.Equal(unchecked((ushort)velocity), step.Frame.Task.ReadWord(0x58 + axis * 2));
            Assert.Equal(unchecked((ushort)-(velocity / 12)), step.Frame.Task.ReadWord(0x60 + axis * 2));
        }
        Assert.True(wrapDistinguishes);
        Assert.Equal(expectedRandom, step.Frame.Random);
        Assert.Equal(0x16155B4u, step.Frame.Task.ReadDword(0x4C));
        Assert.Equal((ushort)11, step.Frame.Task.ReadWord(0x52));
    }

    [Fact]
    public void Decelerating_updates_all_three_new_velocities_without_emitted_particle_gravity_minus_six()
    {
        NativeBattleBio22ParticleFrame frame = Particle(0x89D120);
        frame = frame with { Task = frame.Task.WriteByte(0x29, 1).WriteWord(0x52, 11).WriteWord(0x58, unchecked((ushort)-13)).WriteWord(0x60, 1).WriteWord(0x5A, 13).WriteWord(0x62, unchecked((ushort)-1)).WriteWord(0x5C, 32767).WriteWord(0x64, 1).WriteWord(0x1E, unchecked((ushort)-1000)) };
        NativeBattleBio22ParticleStep step = NativeBattleBio22LaterParticles.Advance89D120(frame, Draw);
        Assert.Equal(unchecked((ushort)-12), step.Frame.Task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-988), step.Frame.Task.ReadWord(0x1E));
        Assert.Equal((ushort)0x8000, step.Frame.Task.ReadWord(0x20));
        Assert.Equal(unchecked((ushort)-1), step.Frame.Task.ReadWord(0x62));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Theory]
    [InlineData(0x89CE60u)]
    [InlineData(0x89D120u)]
    public void Later_particles_apply_positive_Y_gate_before_draw_and_preserve_JSON_next_for_every_phase(uint callback)
    {
        NativeBattleBio22ParticleFrame frame = Particle(callback);
        frame = frame with { Primary = frame.Primary.WriteWord(0x7A, unchecked((ushort)-3000)).WriteWord(0x86, 4096) };
        for (int tick = 0; tick < 25; tick++)
        {
            NativeBattleBio22ParticleFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBio22ParticleFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleBio22ParticleStep expected = Advance(callback, restored, Draw);
            NativeBattleBio22ParticleStep next = Advance(callback, frame, Draw);
            Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(next));
            frame = next.Frame;
            if (next.CompleteRequested)
            {
                break;
            }
            Assert.True(tick < 24);
        }
        NativeBattleBio22ParticleFrame terminal = frame with { Task = frame.Task.WriteByte(0x29, 2).WriteByte(0x26, 0).WriteWord(0x1E, 1).WriteWord(0x24, 0xFFFF) };
        NativeBattleBio22ParticleStep hidden = Advance(callback, terminal, (current, scale) =>
        {
            Assert.Equal((byte)5, current.Task.ReadByte(0x26));
            return current;
        });
        Assert.True(hidden.CompleteRequested);
        Assert.Equal((ushort)0, hidden.Frame.Task.ReadWord(0x24));
        Assert.Equal(terminal.Random, hidden.Frame.Random);
    }

    private static NativeBattleBio22ParticleStep Advance(uint callback, NativeBattleBio22ParticleFrame frame, Func<NativeBattleBio22ParticleFrame, short, NativeBattleBio22ParticleFrame> draw) => callback == 0x89CE60 ? NativeBattleBio22LaterParticles.Advance89CE60(frame, draw) : NativeBattleBio22LaterParticles.Advance89D120(frame, draw);
    private static NativeBattleBio22ParticleFrame Draw(NativeBattleBio22ParticleFrame frame, short scale) => frame;
    private static NativeBattleBio22ParticleFrame Particle(uint callback) => new(new(0, 0, null, callback, new byte[0x70 - 12].ToImmutableArray()), new(0, 0, null, 0x89A8D0, new byte[0x90].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess(), NativeCrtRandomState.FromColdProcess());
}
