using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorBankStorageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Actor_fixups_stay_private_until_context_success_and_preserve_unrelated_unknown_bytes(int directory)
    {
        const uint address = 0x1800000;
        NativeBattleNodeSource source = Source(directory);
        source.Arena.Write(0x350, [0xA5]);
        source.Arena.MarkUnknown(0x350, 1);
        source.Arena.WriteResourcePointer(0x354, new(null, null) { CpuRow = 1 });
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(source, address, directory);
            Assert.Equal(0u, source.ReadUInt32(directory));
            throw new InvalidOperationException("Task-context callback failed.");
        }));
        Assert.Equal(0u, source.ReadUInt32(directory));
        Assert.Equal(0x180u, source.ReadUInt32(directory + 4));
        using (NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(source, address, directory))
        {
            Assert.Same(source, initialization.Publish());
        }
        Assert.Equal(1u, source.ReadUInt32(directory));
        Assert.Equal(address + (uint)directory + 0x180u, source.ReadUInt32(directory + 4));
        Assert.Equal(address + (uint)directory + 0x10u, source.ReadUInt32(directory + 0x180 + 0xC8));
        byte[] raw = new byte[1];
        bool[] known = new bool[1];
        source.ReadWithKnownness(0x350, raw, known);
        Assert.Equal((byte)0xA5, raw[0]);
        Assert.False(known[0]);
        Assert.Equal(1, source.Arena.ReadResourcePointer(0x354)!.CpuRow);
    }

    [Fact]
    public void Locally_published_actor_fixups_do_not_accept_an_unpublished_outer_frame()
    {
        NativeBattleNodeSource source = Source(0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Death14 = NativeBattleDeathState.FromColdProcess() with { AuraActors = source } };
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new(memory, []);
            using NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(source, 0x1800000);
            initialization.Publish();
            Assert.Equal(1u, source.ReadUInt32(0));
            throw new InvalidOperationException("Unpublished controller suffix failed.");
        }));
        Assert.Equal(0u, source.ReadUInt32(0));
        Assert.Equal(0x180u, source.ReadUInt32(4));
    }

    [Fact]
    public void Death_and_Wendigo_actor_fields_keep_legacy_arrays_and_restore_independent_working_owners()
    {
        ActorHistory live = new(NativeBattleDeathState.FromColdProcess() with { AuraActors = Source(0), DarkActors = Source(0) }, NativeBattleWendigoState.FromColdProcess() with { ActorBank = Source(32) });
        ActorHistory captured = NativeBattleStorageSnapshots.Capture(live);
        string json = JsonSerializer.Serialize(captured);
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("Death").GetProperty("AuraActors").ValueKind);
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("Wendigo").GetProperty("ActorBank").ValueKind);
        }
        ActorHistory first = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<ActorHistory>(json)!);
        ActorHistory second = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<ActorHistory>(json)!);
        using (NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(first.Death.AuraActors!, 0x1800000))
        {
            initialization.Publish();
        }
        using (NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(first.Wendigo.ActorBank!, 0x1900000, 32))
        {
            initialization.Publish();
        }
        Assert.Equal(1u, first.Death.AuraActors!.ReadUInt32(0));
        Assert.Equal(1u, first.Wendigo.ActorBank!.ReadUInt32(32));
        Assert.Equal(0u, second.Death.AuraActors!.ReadUInt32(0));
        Assert.Equal(0u, second.Wendigo.ActorBank!.ReadUInt32(32));
        Assert.Equal(0u, live.Death.AuraActors!.ReadUInt32(0));
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    private static NativeBattleNodeSource Source(int directory)
    {
        byte[] data = new byte[0x400];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(directory + 4), 0x180);
        for (int field = 0xC8; field <= 0x144; field += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(directory + 0x180 + field), 0x10);
        }
        return new(data.ToImmutableArray());
    }

    private sealed record ActorHistory(NativeBattleDeathState Death, NativeBattleWendigoState Wendigo);
}
