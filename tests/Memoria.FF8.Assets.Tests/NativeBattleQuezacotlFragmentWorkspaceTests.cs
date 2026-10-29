using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlFragmentWorkspaceTests
{
    [Fact]
    public void Original525_current_model_initializes_the124_child_workspace_for_its_complete_sixteen_frame_resource()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 525);
        Assert.Equal("MAG115_H.20", file.Name);
        Assert.Equal(13260, NativeBattleStorageSnapshots.Capture(file).Data.Length);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116);
        //276 queues this original type5 resource to12A9664;303 uses those current bytes rather than a cold EXE template.
        memory = memory with { QuezacotlExecutableMemory = memory.QuezacotlExecutableMemory!.Write(0x12A9664, NativeBattleStorageSnapshots.Capture(file).Data.AsSpan()) };
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x12A9664);
        int table = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(4));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(table)));
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(source).Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[NativeBattleQuezacotlWorkspace.NinthLength].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, NativeBattleQuezacotlWorkspace.NinthLength);
        for (int frame = 0; frame < 16; frame++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, _) => current);
            Assert.Equal(4, advanced.EmittedNodes);
            workspace = advanced.Workspace;
        }
        Assert.Equal(16, workspace.Frame);
        Assert.Equal(0x12A9664u, workspace.ResourceAddress);
        Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidOperationException("Native source duration is complete.")).EmittedNodes);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(workspace), NativeBattleSnapshotJson.Serialize(NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))));
    }
}
