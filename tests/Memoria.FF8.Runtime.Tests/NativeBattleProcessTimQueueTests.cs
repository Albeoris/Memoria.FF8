using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleProcessTimQueueTests
{
    [Fact]
    public void Physical_owner_is_resolved_at_drain_after_mutation_and_whole_memory_JSON_restore()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Palette = new(Tim(0x1234).Data) };
        memory = NativeBattleProcessTim.Enqueue505E30(memory, 0x16146E8);
        memory = memory with { Bio22Palette = new(Tim(0xBEEF).Data) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleEffectTextureDrain drain = restored.Textures!.Drain505D20(resolveProcessTim: address => NativeBattleProcessTim.Resolve(restored, address));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(drain.Uploads[0].Bytes.AsSpan()));
        Assert.Null(restored.Textures.Slots[0]!.Source);
        Assert.Throws<NotSupportedException>(() => NativeBattleProcessTim.Resolve(restored with { Bio22Palette = null }, 0x16146E8));
    }

    [Fact]
    public void Pending_process_pointer_resolves_latest_palette_after_independent_JSON_and_releases_pending_count()
    {
        var queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueProcessTim505E30(0x16146E8, Tim(0x1234));
        Assert.Equal(0x1614908u, queued.NextAddress);
        Assert.Null(queued.State.Slots[0]!.Source);
        Assert.Equal(0x16146E8u, queued.State.Slots[0]!.ProcessAddress);
        NativeBattleEffectTextureState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(queued.State))!;
        Assert.Throws<NotSupportedException>(() => restored.Drain505D20());
        int reads = 0;
        NativeBattleEffectTextureDrain drain = restored.Drain505D20(resolveProcessTim: address =>
        {
            Assert.Equal(0x16146E8u, address);
            reads++;
            return Tim(0xABCD);
        });
        Assert.Equal(1, reads);
        Assert.Equal(0, drain.State.Count);
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(drain.Uploads[0].Bytes.AsSpan()));
        Assert.Equal(512, drain.Uploads[0].Bytes.Length);
        Assert.Empty(drain.Uploads[1].Bytes);
        Assert.Null(drain.State.Slots[0]!.Source);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Every_reused_slot_replaces_process_pointer_with_its_actual_new_source_arm(int kind)
    {
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueProcessTim505E30(0x16146E8, Tim(1)).State;
        NativeBattleEffectTextureState state = queued.Initialize500400();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x100, Tim(2).Data.AsSpan());
        state = kind switch
        {
            0 => state.EnqueueTim505E30(Tim(2)).State,
            1 => state.EnqueueArenaTim505E30(arena, 0x100).State,
            2 => state.EnqueueCopy505EB0(0, 0, 1, 1, 1, 0),
            3 => state.EnqueueRectangle505DF0(new(0, 0, 1, 1), new("generated-color", [1, 2])),
            _ => state.EnqueueArenaRectangle505DF0(new(0, 0, 1, 1), arena, 0x100)
        };
        Assert.Null(state.Slots[0]!.ProcessAddress);
        state.Validate();
        state.Drain505D20(arena);
    }

    [Fact]
    public void Ambiguous_or_unbound_process_pointer_is_rejected()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().EnqueueProcessTim505E30(0x16146E8, Tim(1)).State;
        NativeBattleEffectTimUpload slot = state.Slots[0]!;
        Assert.Throws<InvalidDataException>(() => (state with { Slots = state.Slots.SetItem(0, slot with { Source = Tim(2) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Slots = state.Slots.SetItem(0, slot with { ProcessAddress = 1 }) }).Validate());
        Assert.Throws<NotSupportedException>(() => state.EnqueueProcessTim505E30(1, Tim(1)));
    }

    private static NativeBattleEffectHeapAllocation Tim(ushort color)
    {
        byte[] bytes = new byte[0x220];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x20C);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 241);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), color);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x214), 12);
        return new("generated-palette-tim", bytes.ToImmutableArray());
    }
}
