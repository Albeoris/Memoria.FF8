using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup19PresentationOwnershipTests
{
    [Fact]
    public void Actual_operation_metadata_is_not_serialized_into_native_receipts_or_snapshots()
    {
        var identity = new NativeBattlePilotVisualIdentity(300, 0x25077F0, 1, 123, 2) { ResourceGeneration = 0x103FA04 };
        NativeBattleMicroMissiles299DrawSprite sprite = new(0x1D99A00, 0x1040000, 9);
        NativeBattleMicroMissiles299DrawParticle particle = new(0x20E0000);
        NativeBattleBadBreath300DrawStrip strip = new(0x1D99A00, 0x103FA04, 0x20E0314);
        NativeBattleBadBreath300Clip clip = new(9);
        NativeBattleBadBreath300DrawParticle cloud = new(0x20E0400);
        NativeBattleSnipeLaser302DrawMesh7043B0 mesh = new(0x1D99A00, 0x1030000, 0x20E0000);
        NativeBattleSnipeLaser302DrawStatic88 copy = new(0x1D99A00, 0x1030010);
        NativeBattleSnipeLaser302DrawSprite180 spark = new(0x1D99A00, 0x1031000);
        Check(sprite, sprite with { PilotIdentity = identity with { Effect = 299 } });
        Check(particle, particle with { PilotIdentity = identity with { Effect = 299 } });
        Check(strip, strip with { PilotIdentity = identity });
        Check(clip, clip with { PilotIdentity = identity });
        Check(cloud, cloud with { PilotIdentity = identity });
        Check(mesh, mesh with { PilotIdentity = identity with { Effect = 302 } });
        Check(copy, copy with { PilotIdentity = identity with { Effect = 302 } });
        Check(spark, spark with { PilotIdentity = identity with { Effect = 302 } });
    }

    [Theory]
    [InlineData((ushort)271)]
    [InlineData((ushort)272)]
    [InlineData((ushort)297)]
    [InlineData((ushort)299)]
    [InlineData((ushort)300)]
    [InlineData((ushort)302)]
    public void Physical_task_reuse_and_source_or_copy_boundaries_cannot_reuse_presentation_identity(ushort effect)
    {
        var pool = NativeBattleEffectTaskPool.Initialize(0x2500000, 0x20E0000, 64, 1);
        (pool, int slot) = pool.Append(0x670000);
        var first = NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot], 12) with { ResourceGeneration = 0x1040000 };
        string native = NativeBattleTestSnapshots.Json(pool);
        Assert.Equal(first, NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot], 12) with { ResourceGeneration = 0x1040000 });
        Assert.NotEqual(first, first with { Primitive = 13 });
        Assert.NotEqual(first, first with { ResourceGeneration = 0x1041000 });
        Assert.Equal(native, NativeBattleTestSnapshots.Json(pool));
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        (pool, int recycled) = pool.Append(0x670000);
        Assert.Equal(slot, recycled);
        var next = NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[recycled], 12) with { ResourceGeneration = 0x1040000 };
        Assert.Equal(first.Bank, next.Bank);
        Assert.Equal(first.Slot, next.Slot);
        Assert.NotEqual(first.Generation, next.Generation);
    }

    private static void Check<T>(T native, T captured)
    {
        string expected = JsonSerializer.Serialize(native);
        Assert.Equal(expected, JsonSerializer.Serialize(captured));
        Assert.DoesNotContain("PilotIdentity", expected);
    }
}
