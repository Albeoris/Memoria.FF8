using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectHeapLoadTests
{
    [Fact]
    public void OriginalMag198TimRemainsHeapOwnedAcrossRestoreAndUploadsOnlyWhenExplicitlyQueued()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(directory, "Data", "lang-en", "magic.fs"), FlArchiveRepresentation.Files);
        using Stream stream = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("mag198.tim", StringComparison.OrdinalIgnoreCase)).OpenForRead();
        using MemoryStream content = new();
        stream.CopyTo(content);
        ImmutableArray<byte> bytes = content.ToArray().ToImmutableArray();
        Assert.NotEmpty(bytes);
        NativeBattleEffectMemory initial = NativeBattleEffectMemory.FromColdProcess().BeginLoad(198).LoadIntoArena([0xDA, 0x71]);
        NativeBattleEffectHeapLoad loaded = initial.LoadHeap5718E0(new("mag198.tim", bytes));
        Assert.Equal(0, loaded.AllocationIndex);
        Assert.Equal(1, loaded.Memory.AllocationCount);
        Assert.Equal(2, loaded.Memory.Cursor);
        Assert.Equal(new byte[] { 0xDA, 0x71, 0 }, loaded.Memory.Arena!.Read(0, 3));
        Assert.Equal(bytes.ToArray(), loaded.Memory.Allocations[0]!.Data.ToArray());
        Assert.Equal(0, loaded.Memory.Textures!.Count);
        Assert.Null(loaded.Memory.Textures.Animation);
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(loaded.Memory))!;
        (NativeBattleEffectTextureState queued, _) = restored.Textures!.EnqueueTim505E30(restored.Allocations[loaded.AllocationIndex]!);
        NativeBattleEffectTextureDrain uploaded = queued.Drain505D20(restored.Arena);
        Assert.NotEmpty(uploaded.Uploads);
        Assert.All(uploaded.Uploads, upload => Assert.Equal(upload.Width * upload.Height * 2, upload.Bytes.Length));
        Assert.Equal(0, uploaded.State.Count);
        (NativeBattleEffectTextureState continuousQueue, _) = loaded.Memory.Textures.EnqueueTim505E30(loaded.Memory.Allocations[0]!);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(continuousQueue.Drain505D20(loaded.Memory.Arena)), NativeBattleSnapshotJson.Serialize(uploaded));
        NativeBattleEffectMemory released = restored.BeginLoad(199);
        Assert.Equal(0, released.AllocationCount);
        Assert.Null(released.Allocations[0]);
        Assert.Equal(0, released.Cursor);
    }
}
