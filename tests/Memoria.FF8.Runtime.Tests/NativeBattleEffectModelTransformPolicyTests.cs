using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectModelTransformPolicyTests
{
    [Fact]
    public void Native_default_reads_current_caller_bytes_and_rejects_unknown_scratch()
    {
        NativeBattleStackBytes unknown = NativeBattleStackBytes.Unknown("frame", 0, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectModelTransforms.ActorRelative(default, () => (unknown, 0)));
        NativeBattleStackBytes zero = unknown.StoreUInt32(0, 0, 0x1000, "native caller");
        Assert.False(NativeBattleEffectModelTransforms.ActorRelative(default, () => (zero, 0)));
        NativeBattleStackBytes nonzero = unknown.StoreUInt32(0, 0x6CF68C, 0x6CF687, "target initializer");
        Assert.True(NativeBattleEffectModelTransforms.ActorRelative(default, () => (nonzero, 0)));
    }

    [Fact]
    public void Explicit_host_profile_has_no_native_stack_owner_and_roundtrips_independently()
    {
        NativeBattleEffectModelTransformPolicy policy = JsonSerializer.Deserialize<NativeBattleEffectModelTransformPolicy>(JsonSerializer.Serialize(NativeBattleEffectModelTransformPolicy.HostActorRelative));
        Assert.True(NativeBattleEffectModelTransforms.ActorRelative(policy, () => throw new InvalidOperationException("The host has no native execution stack.")));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectModelTransforms.ActorRelative((NativeBattleEffectModelTransformPolicy)99, () => throw new InvalidOperationException()));
    }
}
