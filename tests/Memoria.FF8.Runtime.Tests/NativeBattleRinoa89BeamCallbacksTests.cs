using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89BeamCallbacksTests
{
    [Theory]
    [InlineData(false, 20780)]
    [InlineData(true, 6444)]
    public void InitialBeamMeshUsesItsNativeBankAndDrawsBeforeMoving(bool narrow, int expectedZ)
    {
        var fixture = Fixture(narrow, 0);
        List<bool> banks = [];
        NativeBattleRinoa89BeamServices services = Services((frame, _, common) =>
        {
            banks.Add(common);
            return frame;
        }, _ => throw new InvalidOperationException("Initial beam has no contact callback."));
        NativeBattleRinoa89TaskStep step = narrow ? NativeBattleRinoa89Controller.AdvanceBeam6FDC20(fixture.Frame, fixture.Pool, 0, services) : NativeBattleRinoa89Controller.AdvanceBeam6FD580(fixture.Frame, fixture.Pool, 0, services);
        Assert.Equal(new[] { !narrow }, banks);
        Assert.Equal(unchecked((uint)expectedZ), step.Pool.Slots[0].ReadDword(40));
        Assert.Equal(narrow ? 8492 : 24876, BinaryPrimitives.ReadInt32LittleEndian(step.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 28, 4)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Throws<NotSupportedException>(() => step.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 18, 2));
        Assert.Equal((ushort)1, step.Pool.Slots[0].ReadWord(44));
        Assert.Equal(0, step.Result);
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void NarrowTranslationDividesSignedOddComponentsTowardZero()
    {
        var fixture = Fixture(true, 0);
        NativeBattleModulePoolTask task = fixture.Pool.Slots[0].WriteWord(16, unchecked((ushort)-3)).WriteWord(22, 3).WriteWord(28, unchecked((ushort)-5));
        NativeBattleEffectTaskPool pool = NativeBattleTaskFixture.EditPool(fixture.Pool, data => data with { Slots = fixture.Pool.Slots.SetItem(0, task) });
        NativeBattleRinoa89TaskStep step = NativeBattleRinoa89Controller.AdvanceBeam6FDC20(fixture.Frame, pool, 0, Services((frame, _, _) => frame, _ => throw new InvalidOperationException()));
        Assert.Equal(101u, step.Pool.Slots[0].ReadDword(32));
        Assert.Equal(199u, step.Pool.Slots[0].ReadDword(36));
        Assert.Equal(8494u, step.Pool.Slots[0].ReadDword(40));
    }

    [Fact]
    public void WideBeamAgeSixEmitsInNativeOrderAndReleasesBeforeCounterIncrement()
    {
        var fixture = Fixture(false, 6);
        int contacts = 0;
        NativeBattleRinoa89BeamServices services = Services((frame, _, common) =>
        {
            Assert.True(common);
            return frame;
        }, frame =>
        {
            contacts++;
            Assert.Equal(32, ActiveSparks(frame));
            Assert.Equal(8, ActiveEmitters(frame));
            return frame;
        });
        NativeBattleRinoa89TaskStep step = NativeBattleRinoa89Controller.AdvanceBeam6FD580(fixture.Frame, fixture.Pool, 0, services);
        Assert.Equal(1, contacts);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)6, step.Pool.Slots[0].ReadWord(44));
        Assert.Equal(AfterDraws(fixture.Frame.Random, 80), step.Frame.Random);
        Assert.Equal(new byte[] { 13, 0 }, step.Frame.Memory.Arena!.Read(16960, 2));
        Assert.Equal(new byte[] { 13, 1 }, step.Frame.Memory.Arena!.Read(16960 + 4 * 48, 2));
        Assert.Throws<NotSupportedException>(() => step.Frame.Memory.Arena!.Read(16960 + 34, 2));
    }

    [Fact]
    public void NarrowAgeThreeSpawnsItsBurstThenPublishesKnownQuarterTurnPadding()
    {
        var fixture = Fixture(true, 3);
        NativeBattleRinoa89TaskStep step = NativeBattleRinoa89Controller.AdvanceBeam6FDC20(fixture.Frame, fixture.Pool, 0, Services((frame, _, common) =>
        {
            Assert.False(common);
            return frame;
        }, _ => throw new InvalidOperationException("Narrow beam has no contact callback.")));
        Assert.Equal(16, ActiveEmitters(step.Frame));
        Assert.Equal(16, ActiveSparks(step.Frame));
        Assert.Equal(AfterDraws(fixture.Frame.Random, 48), step.Frame.Random);
        NativeBattleModulePoolTask task = step.Pool.Slots[0];
        Assert.Equal((ushort)4096, task.ReadWord(12));
        Assert.Equal((ushort)4096, task.ReadWord(22));
        Assert.Equal(unchecked((ushort)-4096), task.ReadWord(26));
        Assert.Equal((ushort)0, task.ReadWord(30));
        Assert.Equal((ushort)4, task.ReadWord(44));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void NarrowAgeTwelveSkipsMeshButEmitsFinalBandAndIncrementsBeforeRelease()
    {
        var fixture = Fixture(true, 12);
        NativeBattleRinoa89BeamServices services = Services((_, _, _) => throw new InvalidOperationException("Native age twelve skips its mesh draw."), _ => throw new InvalidOperationException("Narrow beam has no contact callback."));
        NativeBattleRinoa89TaskStep step = NativeBattleRinoa89Controller.AdvanceBeam6FDC20(fixture.Frame, fixture.Pool, 0, services);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)13, step.Pool.Slots[0].ReadWord(44));
        Assert.Equal(48, ActiveEmitters(step.Frame));
        Assert.Equal(0, ActiveSparks(step.Frame));
        Assert.Equal(AfterDraws(fixture.Frame.Random, 96), step.Frame.Random);
        Assert.Equal(new byte[] { 13, 4 }, step.Frame.Memory.Arena!.Read(16960, 2));
    }

    private static (NativeBattleRinoa89Frame Frame, NativeBattleEffectTaskPool Pool) Fixture(bool narrow, ushort age)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa89Controller.Beams, 0x20DFAB8, 392, 8);
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Beams) { Pools = [pool] };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2556600, BitConverter.GetBytes(0x20DFAB8u)).Write(0x2556594, BitConverter.GetBytes(0x20DFAB8u + 16960u)).Write(0x2556590, BitConverter.GetBytes(0x20DFAB8u + 4672u)).Write(0x25565F8, new byte[] { 100, 0, 200, 0, 44, 1 });
        staging = NativeBattleTemporaryMemory.Reset5082F0(staging);
        NativeBattleRinoa89Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, staging, NativeCrtRandomState.FromColdProcess());
        NativeBattleRinoa89BeamBirth born = narrow ? NativeBattleRinoa89Controller.SpawnBeam6FDB80(frame, pool, 0, 0, Programme()) : NativeBattleRinoa89Controller.SpawnBeam6FD4D0(frame, pool, 0, 0, Programme());
        pool = NativeBattleTaskFixture.EditPool(born.Pool, data => data with { Slots = born.Pool.Slots.SetItem(0, born.Pool.Slots[0].WriteWord(44, age)) });
        return (born.Frame with { Effect = born.Frame.Effect with { Pools = [pool] } }, pool);
    }

    private static NativeBattleRinoa89BeamServices Services(Func<NativeBattleRinoa89Frame, NativeBattleGfGeometryBatch, bool, NativeBattleRinoa89Frame> publish, Func<NativeBattleRinoa89Frame, NativeBattleRinoa89Frame> contacts)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        byte[] mesh = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 8);
        return new(view, new(view, 256, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, mesh.ToImmutableArray(), Programme(), publish, contacts);
    }

    private static NativeBattleGfAnimationSource Programme() => new(0x14A3EB0, new byte[8].ToImmutableArray());
    private static int ActiveSparks(NativeBattleRinoa89Frame frame) => Enumerable.Range(0, 512).Count(index => BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(4672 + index * 24 + 10, 2)) != 0);
    private static int ActiveEmitters(NativeBattleRinoa89Frame frame) => Enumerable.Range(0, 128).Count(index => frame.Memory.Arena!.Read(16960 + index * 48, 1)[0] != 0);

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
