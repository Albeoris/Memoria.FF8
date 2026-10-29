using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlExecutableWorkspaceTests
{
    [Fact]
    public void Original129CD84_runs_its_sixteen_nodes_in_the_retained4BC_executable_workspace_and_preserves_the_concurrent_child()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset currentModel = resources.Files.Single(file => file.FileId == 521);
        Assert.Equal("MAG115_H.16", currentModel.Name);
        Assert.Equal(0x6880, NativeBattleStorageSnapshots.Capture(currentModel).Data.Length);
        Assert.Equal("BD70AE826A4CD3FC2F9DB46E0CDF3BFC09612A856AD61B6BB242CC4DA683999C", Convert.ToHexString(SHA256.HashData(NativeBattleStorageSnapshots.Capture(currentModel).Data.AsSpan())));
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116);
        //6C4C4B queues521/type5 to1298C68 before212; its bytes end exactly at workspace129F4E8.
        memory = memory with { QuezacotlExecutableMemory = memory.QuezacotlExecutableMemory!.Write(NativeBattleQuezacotlExecutableMemory.Address, NativeBattleStorageSnapshots.Capture(currentModel).Data.AsSpan()) };
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84);
        int table = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(4));
        Assert.Equal(0x18C0, table);
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(table)));
        Assert.Equal(26, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationSource childSource = NativeBattleQuezacotlWorkspace.Source(memory, 0x1299718);
        NativeBattleGfAnimationWorkspace childWorkspace = new(new byte[NativeBattleQuezacotlWorkspace.SeventhLength].ToImmutableArray());
        childWorkspace = childWorkspace.Initialize7016B0(childSource, NativeBattleQuezacotlWorkspace.SeventhLength);
        memory = memory with { Arena = memory.Arena!.Write(NativeBattleQuezacotlWorkspace.Offset, NativeBattleStorageSnapshots.Capture(childWorkspace).Data.AsSpan()) };
        byte[] original = NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data.ToArray();
        NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.EighthLength);
        for (int frame = 0; frame < 26; frame++)
        {
            memory = NativeBattleQuezacotlWorkspace.WriteExecutable(memory, workspace);
            source = NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84);
            NativeBattleGfAnimationAdvance advanced = NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Advance701970(source, 0, (current, _) => current);
            Assert.Equal(16, advanced.EmittedNodes);
            workspace = advanced.Workspace;
        }
        memory = NativeBattleQuezacotlWorkspace.WriteExecutable(memory, workspace);
        Assert.Equal(26, NativeBattleQuezacotlWorkspace.ReadExecutable(memory).Frame);
        Assert.Equal(0x129CD84u, workspace.ResourceAddress);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(childWorkspace).Data.ToArray(), memory.Arena!.Read(NativeBattleQuezacotlWorkspace.Offset, NativeBattleQuezacotlWorkspace.SeventhLength));
        int offset = checked((int)(NativeBattleQuezacotlWorkspace.EighthAddress - NativeBattleQuezacotlExecutableMemory.Address));
        Assert.Equal(original.AsSpan(0, offset).ToArray(), NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data.AsSpan().Slice(0, offset).ToArray());
        Assert.Equal(original.AsSpan(offset + NativeBattleQuezacotlWorkspace.EighthLength).ToArray(), NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data.AsSpan().Slice(offset + NativeBattleQuezacotlWorkspace.EighthLength).ToArray());
        NativeBattleGfAnimationAdvance completed = workspace.Advance701970(NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84), 0, (_, _) => throw new InvalidOperationException("The native resource duration has completed."));
        Assert.Equal(0, completed.EmittedNodes);
        Assert.Equal(0, completed.Remaining);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(memory), NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(memory))));
        Assert.Equal(240039u, resources.Frame277Sound);
        Assert.DoesNotContain("Frame277Sound", NativeBattleSnapshotJson.Serialize(resources with { Frame277Sound = null }));
    }

    [Fact]
    public void Original527_type16_completion_queues_its_three_mutable_arena_TIM_pointers()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 527);
        Assert.Equal(97896, NativeBattleStorageSnapshots.Capture(file).Data.Length);
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116), 64);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 527, 0x2108954, 0x10);
        NativeBattleEffectIoAdvance request = NativeBattleEffectIo.Advance534210(memory);
        Assert.Equal(new NativeBattleEffectFileRequest(527, 0x2108954, 0x6C3580), request.Request);
        memory = request.Memory with { Arena = request.Memory.Arena!.Write(0x28E9C, NativeBattleStorageSnapshots.Capture(file).Data.AsSpan()) };
        NativeBattleEffectIoCompletion completed = NativeBattleEffectIo.Complete6C3580(memory, (_, _) => throw new InvalidOperationException("Native type16 uses its current three arena TIM aliases."));
        Assert.Null(completed.Stream);
        Assert.Equal(3, completed.Memory.Textures!.Count);
        Assert.Equal(new int?[] { 0x2C6A4, 0x348C4, 0x38AE4 }, completed.Memory.Textures.Slots.Take(3).Select(slot => slot!.ArenaOffset));
        Assert.NotEmpty(completed.Memory.Textures.Drain505D20(completed.Memory.Arena).Uploads);
    }
}
