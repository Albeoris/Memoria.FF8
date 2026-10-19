using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianCameraTemporaryTests
{
    [Fact]
    public void PausedPublicationWritesOnlyWorldXyzAndRetainsScratchTails()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess() with { Sign = 1 };
        process = process with { Eye = process.Eye with { Current = new(10, 20, 30), Remaining = 2 }, Target = process.Target with { Current = new(40, 50, 60), Remaining = 2 } };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 432).ToArray());
        NativeBattleGuardianCameraStep step = Advance(process, temporary, true);
        Assert.Equal(10, Short(step.Temporary432!, 0));
        Assert.Equal(60, Short(step.Temporary432!, 12));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, step.Temporary432!.Read(6, 2));
        Assert.Equal(temporary.Read(16, 416), step.Temporary432.Read(16, 416));
    }

    [Fact]
    public void PolynomialCameraPublishesActualBernsteinScratchWithoutClearingTails()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess() with { Sign = 1 };
        process = process with { Eye = process.Eye with { Opcode = 4, Remaining = 2, Step = 1024, ControlPoints = ImmutableArray.Create(new NativeBattleVector(0, 0, 0), new NativeBattleVector(2, 4, 6), new NativeBattleVector(10, 20, 30), new NativeBattleVector(20, 40, 60)) }, Target = process.Target with { Remaining = 2 } };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 432).ToArray());
        NativeBattleGuardianCameraStep step = Advance(process, temporary, false);
        Assert.Equal(60, Dword(step.Temporary432!, 72));
        Assert.Equal(6, Dword(step.Temporary432!, 32));
        Assert.Equal(temporary.Read(28, 4), step.Temporary432!.Read(28, 4));
        Assert.Equal(temporary.Read(392, 4), step.Temporary432.Read(392, 4));
        Assert.Equal(temporary.Read(416, 16), step.Temporary432.Read(416, 16));
    }

    [Fact]
    public void TerminatedProgramRetainsItsLastReadDestinationWithoutWorldPublication()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess() with { Sign = 1 };
        byte[] program = new byte[12];
        ushort[] words = [0x42, 2, 10, 20, 30, 11];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(index * 2), words[index]);
        }
        NativeBattleGuardianCameraTask task = new(process, program.ToImmutableArray(), 0, 0, false);
        NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), false, temporary432: NativeBattleEffectArena.Clear());
        Assert.Equal(2, step.Result);
        Assert.False(step.Published);
        Assert.Equal(10, Short(step.Temporary432!, 0));
        Assert.Equal(20, Short(step.Temporary432!, 2));
        Assert.Equal(30, Short(step.Temporary432!, 4));
    }

    [Fact]
    public void DistanceCameraPublishesItsFullDwordDifferenceBeforeNormalization()
    {
        NativeBattleGuardianCameraProcess process = NativeBattleGuardianCameraProgram.FromColdProcess() with { Sign = 1 };
        process = process with { Eye = process.Eye with { Opcode = 5, Remaining = 2, Origin = new(1, 2, 3), Delta = new(4, 5, 6), Accumulator = 10 }, Target = process.Target with { Current = new(100, 200, 300), Remaining = 2 } };
        NativeBattleGuardianCameraStep step = Advance(process, NativeBattleEffectArena.Clear(), false);
        Assert.Equal(95, Dword(step.Temporary432!, 16));
        Assert.Equal(193, Dword(step.Temporary432!, 20));
        Assert.Equal(291, Dword(step.Temporary432!, 24));
    }

    private static NativeBattleGuardianCameraStep Advance(NativeBattleGuardianCameraProcess process, NativeBattleEffectArena temporary, bool paused)
    {
        NativeBattleGuardianCameraTask task = new(process, ImmutableArray.Create((byte)0, (byte)0), 0, 1, false);
        return NativeBattleGuardianCameraProgram.Advance63E9C0(task, Identity(), Pose(), paused, temporary432: temporary);
    }
    private static NativeBattleActionMatrix Identity() => NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
    private static NativeBattleCameraPose Pose() => new(new(0, 0, 0), new(0, 0, 0), 320, 0);
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static int Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
