using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeEditionProfileTests
{
    [Theory]
    [InlineData(NativeFieldRuntimeProfile.FF8EnExecutableHash)]
    [InlineData(NativeFieldRuntimeProfile.RemasteredEnImageHash)]
    public void SupportedImagesRetainTheirOwnFieldAllocationIdentity(string hash)
    {
        NativeFieldRuntimeProfile profile = Assert.IsType<NativeFieldRuntimeProfile>(NativeFieldRuntimeProfile.ForImage(hash));
        Assert.True(profile.IsVerified);
        Assert.Equal(hash, profile.ExecutableHash);
        Assert.Equal(7929, profile.AllocatedActorSpeed);
        Assert.Equal(28, profile.AllocatedLadderSpeed);
        NativeFieldInitializationProof proof = new(1, hash, 232, new string('A', 64), 20, NativeFieldInitializationStage.Completed);
        Assert.True(proof.IsFrom(profile, 232, new string('A', 64)));
        NativeFieldRuntimeProfile other = hash == NativeFieldRuntimeProfile.FF8EnExecutableHash ? NativeFieldRuntimeProfile.RemasteredEn : NativeFieldRuntimeProfile.FF8En;
        Assert.False(proof.IsFrom(other, 232, new string('A', 64)));
        Assert.False(proof.IsFrom(profile, 229, new string('A', 64)));
        Assert.False(proof.IsFrom(profile, 232, new string('B', 64)));
        Assert.False(proof.IsFrom(profile with { FieldClock = 19 }, 232, new string('A', 64)));
    }

    [Fact]
    public void UnknownImagesAndProfileVersionsRemainUnsupported()
    {
        Assert.Null(NativeFieldRuntimeProfile.ForImage(new string('0', 64)));
        Assert.Null(NativeFieldRuntimeProfile.ForImage(null));
        Assert.False((NativeFieldRuntimeProfile.RemasteredEn with { Version = 2 }).IsVerified);
        Assert.False((NativeFieldRuntimeProfile.RemasteredEn with { FieldClock = 19 }).IsVerified);
    }
}
