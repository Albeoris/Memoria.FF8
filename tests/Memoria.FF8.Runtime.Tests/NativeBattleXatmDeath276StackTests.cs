using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleXatmDeath276StackTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Arc_pointer_includes_both_pending_queue_arguments_and_retained_pose_arguments(byte phase)
    {
        NativeBattleExecutionStack profile = Profile();
        Assert.Equal(0x1A501Cu, NativeBattleXatmDeath276Stack.ResolveDirectionResidue(profile, phase));
        Assert.Equal(profile.FrameCallbackEsp - 0xC0u, NativeBattleXatmDeath276Stack.ResolveDirectionResidue(profile, phase));
        Assert.Equal(0x1B501Cu, NativeBattleXatmDeath276Stack.ResolveDirectionResidue(profile with { FrameCallbackEsp = profile.FrameCallbackEsp + 0x10000 }, phase));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(255)]
    public void Unproved_outer_stack_variants_are_rejected(byte phase)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleXatmDeath276Stack.ResolveDirectionResidue(Profile(), phase));
    }

    [Fact]
    public void Selected_address_survives_independent_json_and_cold_process_does_not_invent_one()
    {
        NativeBattleExecutionStack profile = Profile();
        NativeBattleExecutionStack restored = JsonSerializer.Deserialize<NativeBattleExecutionStack>(JsonSerializer.Serialize(profile))!;
        Assert.Equal(NativeBattleXatmDeath276Stack.ResolveDirectionResidue(profile, 4), NativeBattleXatmDeath276Stack.ResolveDirectionResidue(restored, 4));
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().ExecutionStack);
    }

    private static NativeBattleExecutionStack Profile() => new(0x1A50DC, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, "B6ADB6030051CE54CBA41C8098904FC6D73A430EA7322A93DBC17F55976E8B41", 5592, 8028);
}
