using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModuleSoundTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 31)]
    [InlineData(3, 127)]
    public void Native501740_uses_the_committed_projection_pan(ushort sound, byte pan)
    {
        Assert.Equal((uint)pan, FieldAudioPresentation.ResolveNativeModuleSoundPan(new(sound, 0, new(1, 2, 3, 4)) { Pan = pan }));
    }

    [Fact]
    public void Missing_native_pan_is_rejected_instead_of_being_centered()
    {
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(2, 0, new(1, 2, 3, 4))));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(4, 0, new(1, 2, 3, 4)) { Pan = 20 }));
        Assert.Equal(64u, FieldAudioPresentation.ResolveNativeModuleSoundPan(new(0x72, 0x8000, new(1, 2, 3, 4))));
    }
}
