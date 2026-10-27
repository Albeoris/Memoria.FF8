using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEmbeddedTextureIdentityTests
{
    [Fact]
    public void Requested_embedded_members_follow_upload_events_and_continue_after_restore()
    {
        NativeBattleTextureFileIdentity identity = new("MAG184_E.DAT", NativeBattleTextureDomain.Magic);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadIntoArena(Container(), identity);
        NativeBattleEffectTextureState textures = memory.Textures!;
        textures = textures.EnqueueArenaTim505E30(memory.Arena!, 160).State;
        textures = textures.EnqueueArenaTim505E30(memory.Arena!, 64).State;
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(memory.Arena);
        NativeBattleTextureOrigin[] images = drain.Uploads.Where(upload => upload.Origin?.Role == NativeBattleTextureRole.Pixels).Select(upload => upload.Origin!).ToArray();
        Assert.Equal(new[] { 0, 1 }, images.Select(origin => origin.Member));
        Assert.All(images, origin => Assert.Equal("mag184_e.dat", origin.Owner));
        Assert.Equal(new int?[] { 160, 64 }, images.Select(origin => origin.SourceOffset));
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drain.State)))!);
        NativeBattleEffectArena arena = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory.Arena)))!);
        NativeBattleEffectTextureDrain next = restored.EnqueueArenaTim505E30(arena, 64).State.Drain505D20(arena);
        Assert.Equal(2, next.Uploads[1].Origin!.Member);
        NativeBattleEffectTextureDrain reopened = next.State.OpenTextureFile(identity).EnqueueArenaTim505E30(arena, 160).State.Drain505D20(arena);
        Assert.Equal(0, reopened.Uploads[1].Origin!.Member);
    }

    [Fact]
    public void Last_opened_alias_and_actual_source_owner_remain_distinct_when_loads_interleave()
    {
        byte[] bytes = Container();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0)
            .LoadIntoArena(bytes, new("mag184_e.dat", NativeBattleTextureDomain.Magic))
            .LoadIntoArena(bytes, new("MAG999_E.01", NativeBattleTextureDomain.Battle));
        NativeBattleEffectTextureState textures = memory.Textures!.EnqueueArenaTim505E30(memory.Arena!, 64).State;
        textures = textures.EnqueueArenaTim505E30(memory.Arena!, bytes.Length + 160).State;
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(memory.Arena);
        NativeBattleTextureOrigin first = drain.Uploads[1].Origin!;
        NativeBattleTextureOrigin second = drain.Uploads[3].Origin!;
        Assert.Equal("mag999_e.01", first.Owner);
        Assert.Equal(NativeBattleTextureDomain.Battle, first.Domain);
        Assert.Equal("mag184_e.dat", first.SourceOwner);
        Assert.Equal(64, first.SourceOffset);
        Assert.Equal(0, first.Member);
        Assert.Equal("mag999_e.01", second.SourceOwner);
        Assert.Equal(160, second.SourceOffset);
        Assert.Equal(1, second.Member);
    }

    [Fact]
    public void Heap_container_uses_its_bounded_requested_TIM_offset_without_scanning()
    {
        NativeBattleEffectHeapLoad loaded = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadHeap5718E0(new("mag184_e.dat", Container().ToImmutableArray()));
        NativeBattleEffectHeapAllocation source = loaded.Memory.Allocations[loaded.AllocationIndex]!;
        NativeBattleEffectTextureDrain drain = loaded.Memory.Textures!.EnqueueTim505E30(source, 160).State.Drain505D20();
        Assert.Equal("mag184_e.dat", drain.Uploads[1].Origin!.Owner);
        Assert.Equal(160, drain.Uploads[1].Origin!.SourceOffset);
        Assert.Equal(0, drain.Uploads[1].Origin!.Member);
    }

    [Fact]
    public void Unowned_TIM_uses_the_observed_alias_without_claiming_an_actual_source_owner()
    {
        NativeBattleEffectHeapLoad loaded = NativeBattleEffectMemory.FromColdProcess().LoadHeap5718E0(new("mag184_e.dat", Container().ToImmutableArray()));
        NativeBattleEffectTextureState textures = loaded.Memory.Textures!.EnqueueTim505E30(new("buffer", Tim().ToImmutableArray())).State;
        textures = textures.EnqueueTim505E30(loaded.Memory.Allocations[0]!, 64).State;
        NativeBattleEffectTextureDrain drain = textures.Drain505D20();
        Assert.Equal("mag184_e.dat", drain.Uploads[1].Origin!.Owner);
        Assert.Equal(0, drain.Uploads[1].Origin!.Member);
        Assert.Null(drain.Uploads[1].Origin!.SourceOwner);
        Assert.Null(drain.Uploads[1].Origin!.SourceOffset);
        Assert.Equal(1, drain.Uploads[3].Origin!.Member);
    }

    [Fact]
    public void Unrelated_container_relocation_keeps_the_native_alias_but_not_unproved_source_ownership()
    {
        NativeBattleEffectMemory loaded = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadIntoArena(Container(), new("mag184_e.dat", NativeBattleTextureDomain.Magic));
        NativeBattleEffectArena relocated = loaded.Arena!.Write(16, [0x78, 0x56, 0x34, 0x12]);
        NativeBattleEffectTextureState queued = loaded.Textures!.EnqueueArenaTim505E30(relocated, 64).State;
        Assert.Null(queued.Slots[0]!.TimIdentity);
        queued = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(queued)))!);
        relocated = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(relocated)))!);
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(relocated);
        Assert.Equal("mag184_e.dat", drain.Uploads[1].Origin!.Owner);
        Assert.Equal(NativeBattleTextureDomain.Magic, drain.Uploads[1].Origin!.Domain);
        Assert.Equal(0, drain.Uploads[1].Origin!.Member);
        Assert.Null(drain.Uploads[1].Origin!.SourceOwner);
        Assert.Null(drain.Uploads[1].Origin!.SourceOffset);
        NativeBattleEffectTextureDrain changed = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(relocated.Write(128, [7, 8]));
        Assert.Null(changed.Uploads[1].Origin);
        Assert.Equal(new byte[] { 7, 8 }, changed.Uploads[1].Bytes.Take(2));
        Assert.Equal(1, changed.State.TextureFileContext!.NextTimMember);
    }

    [Fact]
    public void Unknown_large_raw_upload_loses_ordinal_certainty_until_an_observed_file_open()
    {
        NativeBattleEffectHeapLoad loaded = NativeBattleEffectMemory.FromColdProcess().LoadHeap5718E0(new("mag184_e.dat", Container().ToImmutableArray()));
        NativeBattleEffectTextureState textures = loaded.Memory.Textures!.EnqueueRectangle505DF0(new(256, 256, 64, 64), new("buffer", new byte[8192].ToImmutableArray()));
        textures = textures.EnqueueTim505E30(loaded.Memory.Allocations[0]!, 64).State;
        NativeBattleEffectTextureDrain drain = textures.Drain505D20();
        Assert.Null(drain.Uploads.Last().Origin);
        Assert.Null(drain.State.TextureFileContext!.NextTimMember);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drain.State)))!);
        restored = restored.OpenTextureFile(new("mag184_e.dat", NativeBattleTextureDomain.Magic));
        Assert.Equal(0, restored.EnqueueTim505E30(loaded.Memory.Allocations[0]!, 160).State.Drain505D20().Uploads[1].Origin!.Member);
    }

    private static byte[] Container()
    {
        byte[] bytes = new byte[256];
        Tim().CopyTo(bytes, 64);
        Tim().CopyTo(bytes, 160);
        return bytes;
    }

    private static byte[] Tim()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(56), 64);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(60), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(62), 2);
        return bytes;
    }
}
