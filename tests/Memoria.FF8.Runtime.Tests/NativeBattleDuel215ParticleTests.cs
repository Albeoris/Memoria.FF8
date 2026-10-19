using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215ParticleTests
{
    [Fact]
    public void ClearTouchesOnly128LifetimeWordsAndThePhysicalHint()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x69B070, Enumerable.Repeat((byte)0xA5, 5108).ToImmutableArray());
        NativeBattleModulePoolTask cleared = NativeBattleDuel215Controller.ClearParticles69B040(task, 0x20DFAB8);
        Assert.Equal(0x20DFAB8u + 2300, cleared.ReadDword(5116));
        for (int index = 0; index < 128; index++)
        {
            int row = 2300 + index * 22;
            Assert.Equal(0, cleared.ReadWord(row + 6));
            Assert.Equal(0xA5A5, cleared.ReadWord(row));
            Assert.Equal(0xA5A5, cleared.ReadWord(row + 20));
        }
    }

    [Fact]
    public void BirthHonorsItsHintAndConsumesFourDrawsWithoutClaimingTheLifetime()
    {
        NativeBattleModulePoolTask task = NativeBattleDuel215Controller.ClearParticles69B040(Task(), 0x20DFAB8);
        task = task.WriteDword(5116, 0x20DFAB8 + 2344);
        NativeBattleDuel215Frame frame = Frame();
        NativeCrtRandomState expected = frame.Random;
        List<ushort> draws = [];
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out ushort value);
            draws.Add(value);
        }
        NativeBattleDuel215ParticleBirth birth = NativeBattleDuel215Controller.AllocateParticle69CC10(frame, task, 0x20DFAB8);
        Assert.Equal(0x20DFAB8u + 2344, birth.Address);
        Assert.Equal(0x20DFAB8u + 2366, birth.Task.ReadDword(5116));
        Assert.Equal(draws[0], birth.Task.ReadWord(2344 + 16));
        Assert.Equal(draws[1], birth.Task.ReadWord(2344 + 18));
        Assert.Equal(unchecked((byte)draws[2]), birth.Task.ReadByte(2344 + 20));
        Assert.Equal(unchecked((byte)draws[3]), birth.Task.ReadByte(2344 + 21));
        Assert.Equal(0, birth.Task.ReadWord(2344 + 6));
        Assert.Equal(expected, birth.Frame.Random);
    }

    [Fact]
    public void FullPoolFailsBirthWithoutRandomDrawsOrHintChanges()
    {
        NativeBattleModulePoolTask task = NativeBattleDuel215Controller.ClearParticles69B040(Task(), 0x20DFAB8);
        for (int index = 0; index < 128; index++)
        {
            task = task.WriteWord(2306 + index * 22, 1);
        }
        NativeBattleDuel215Frame frame = Frame();
        NativeBattleDuel215ParticleBirth birth = NativeBattleDuel215Controller.AllocateParticle69CC10(frame, task, 0x20DFAB8);
        Assert.Null(birth.Address);
        Assert.Equal(frame.Random, birth.Frame.Random);
        Assert.Equal(task, birth.Task);
    }

    [Fact]
    public void MotionUsesSignedAngularBytesAndGroundBounceOnlyAfterPositionWrite()
    {
        NativeBattleModulePoolTask task = NativeBattleDuel215Controller.ClearParticles69B040(Task(), 0x20DFAB8);
        task = task.WriteWord(2306, 2).WriteWord(2302, unchecked((ushort)-10)).WriteWord(2310, 20).WriteWord(2316, 65535).WriteByte(2320, 254).WriteByte(2321, 127);
        task = task.WriteWord(2328, 1).WriteWord(2322, 0x1234);
        NativeBattleModulePoolTask updated = NativeBattleDuel215Controller.AdvanceParticles69D080(task, 0x20DFAB8);
        Assert.Equal(1, updated.ReadWord(2306));
        Assert.Equal(10, updated.ReadWord(2302));
        Assert.Equal(unchecked((ushort)-5), updated.ReadWord(2310));
        Assert.Equal(65527, updated.ReadWord(2316));
        Assert.Equal(508, updated.ReadWord(2318));
        Assert.Equal(0, updated.ReadWord(2328));
        Assert.Equal(0x1234, updated.ReadWord(2322));
        Assert.Equal(0x20DFAB8u + 2322, updated.ReadDword(5116));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x69B070, new byte[5108].ToImmutableArray());
    private static NativeBattleDuel215Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess(), new(215, 214, new(215, 0x69AB60, 0x69AB70), null, 0), NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
}
