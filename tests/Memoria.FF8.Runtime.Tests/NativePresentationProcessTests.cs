using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePresentationProcessTests
{
    [Fact]
    public void SalaryResetImmediateTickAndNativePhaseBoundariesPreserveGlyphs()
    {
        NativeSalaryPresentation cold = NativeSalaryPresentation.FromColdProcess();
        Assert.True(cold.IsColdProcess);
        NativeSalaryPresentation payment = cold.StartPayment(5, 4, 5000, 4000);
        Assert.Equal((short)1, payment.Phase);
        Assert.Equal((short)256, payment.Scale9A);
        Assert.Equal(new byte[] { 7, 101, 0 }, payment.OldRankGlyphs.ToArray());
        Assert.Equal(new byte[] { 7, 101, 96, 96, 96, 0 }, payment.OldPayGlyphs.ToArray());
        Assert.Equal((byte)1, payment.LeadingBlank9F);
        NativeSalaryPresentation scale = payment.Advance(15);
        Assert.Equal((short)2, scale.Phase);
        Assert.Equal((short)4096, scale.Scale9A);
        NativeSalaryPresentation pause = scale.Advance(1);
        Assert.Equal((byte)23, pause.Delay9E);
        Assert.Equal((short)3, pause.Phase);
        NativeSalaryPresentation rank = pause.Advance(24);
        Assert.Equal((short)5, rank.Phase);
        NativeSalaryPresentation done = rank.Advance(13);
        Assert.Equal((short)6, done.Phase);
        Assert.Equal((short)4096, done.RankBlend98);
        NativeSalaryPresentation closed = done.Close().Advance(17);
        Assert.Equal((short)9, closed.Phase);
        Assert.Equal((short)0, closed.Scale9A);
        NativeSalaryPresentation reset = done.Reset();
        Assert.Equal((short)9, reset.Phase);
        Assert.Equal((byte)0, reset.OldRank9C);
        Assert.Equal(done.NewPayGlyphs.ToArray(), reset.NewPayGlyphs.ToArray());
        Assert.Equal(done.Delay9E, reset.Delay9E);
        Assert.Equal((short)768, payment.Advance(2).Scale9A);
        Assert.Equal((short)1280, payment.Advance(4).Scale9A);
    }

    [Fact]
    public void NativeFormatterKeepsActualTrailingBuffersAndRankA()
    {
        NativeSalaryPresentation payment = NativeSalaryPresentation.FromColdProcess().StartPayment(31, 31, 655350, 0);
        Assert.Equal(new byte[] { 7, 178, 0 }, payment.OldRankGlyphs.ToArray());
        Assert.Equal(new byte[] { 101, 101, 99, 101, 96, 0 }, payment.OldPayGlyphs.ToArray());
        Assert.Equal(new byte[] { 7, 7, 7, 7, 96, 0 }, payment.NewPayGlyphs.ToArray());
        Assert.Equal((short)6, payment.Advance(40).Phase);
        Assert.Equal((short)4096, payment.Advance(40).RankBlend98);
    }

    [Fact]
    public void SharedBufferlessAllocatorRetainsPhysicalIdsAndNeverInfersNativeBuffers()
    {
        NativeGlobalSoundProcess cold = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        Assert.Equal((byte)1, cold.Rotation);
        NativeGlobalSoundProcess next = cold;
        foreach ((uint effect, byte voice) in new[] { (91u, (byte)26), (92u, (byte)27), (93u, (byte)28) })
        {
            next = next.RecordEffect(effect, 0, out NativeGlobalSoundAllocation allocation);
            Assert.Equal(voice, allocation.Voice);
            NativeGlobalSoundProcess.ValidateAllocation(0, allocation);
            Assert.Equal(effect, next.OriginalEffectIds[voice]);
        }
        byte? rotation = next.Rotation;
        NativeGlobalSoundProcess explicitVoice = next.RecordEffect(690001, 0x1000001, out NativeGlobalSoundAllocation explicitAllocation);
        Assert.Equal((byte)1, explicitAllocation.Voice);
        Assert.Equal(rotation, explicitVoice.Rotation);
        NativeGlobalSoundProcess volumeOnly = explicitVoice.SelectVoice(0, out NativeGlobalSoundAllocation volumeAllocation);
        Assert.Equal((byte)29, volumeAllocation.Voice);
        Assert.Equal(explicitVoice.OriginalEffectIds.ToArray(), volumeOnly.OriginalEffectIds.ToArray());
        Assert.True(cold.IsColdProcess);
        Assert.False(volumeOnly.IsColdProcess);
        Assert.Throws<InvalidDataException>(() => (cold with { Backend = (NativeGlobalSoundBackend)1 }).Validate());
    }

    [Fact]
    public void CurrentShapeIsRequiredAndWarmNewGameKeepsProcessHistoryUnavailable()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetSeeD(session.SeeD!.WithSalaryUiEnabled(true));
        session.StartNewGame();
        Assert.False(session.SeeD!.SalaryUiEnabled);
        NativeSalaryPresentation presentation = session.SalaryPresentation!.StartPayment(5, 4, 5000, 4000);
        NativeGlobalSoundProcess sound = session.GlobalSoundProcess!.RecordEffect(91, 0, out _);
        session.SetSalaryPresentation(presentation);
        session.SetGlobalSoundProcess(sound);
        session.StartNewGame();
        Assert.Same(presentation, session.SalaryPresentation);
        Assert.Same(sound, session.GlobalSoundProcess);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeGlobalSoundProcess>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSalaryPresentation>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameState>("{}"));
    }
}
