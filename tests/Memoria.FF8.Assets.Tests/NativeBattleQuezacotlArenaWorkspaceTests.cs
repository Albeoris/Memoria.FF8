using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlArenaWorkspaceTests
{
    [Fact]
    public void Original527_current_arena_resource_runs_all_ten_nodes_for36_frames_through_the_shared_geometry_core()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources resources = game.ReadNativeQuezacotlResources();
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 527);
        Assert.Equal("MAG115_H.22", file.Name);
        Assert.Equal(97896, file.Data.Length);
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(116), 64);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 527, NativeBattleQuezacotlWorkspace.TenthSourceAddress, 0x10);
        NativeBattleEffectIoAdvance requested = NativeBattleEffectIo.Advance534210(memory);
        Assert.Equal(new NativeBattleEffectFileRequest(527, NativeBattleQuezacotlWorkspace.TenthSourceAddress, 0x6C3580), requested.Request);
        memory = requested.Memory with { Arena = requested.Memory.Arena!.Write(0x28E9C, file.Data.AsSpan()) };
        memory = NativeBattleEffectIo.Complete6C3580(memory, (_, _) => throw new InvalidOperationException("The native type16 completion uses current arena TIM aliases.")).Memory;
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, NativeBattleQuezacotlWorkspace.TenthSourceAddress, file.Data.Length);
        Assert.Equal(file.Data.ToArray(), source.Data.ToArray());
        int table = Int32(source.Data, 4);
        Assert.Equal(0x2D78, table);
        Assert.Equal(10, Int32(source.Data, table));
        Assert.Equal(36, Int32(source.Data, table + 4));
        int[][] expectedGroups = [[0, 0, 16, 32, 0, 0, 0, 0], [0, 0, 16, 64, 0, 0, 0, 16], [0, 0, 0, 16, 0, 0, 0, 14], [20, 100, 0, 0, 0, 20, 0, 0]];
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int geometryIndex = 0; geometryIndex < expectedGroups.Length; geometryIndex++)
        {
            int geometry = Int32(source.Data, 8 + geometryIndex * 4);
            int cursor = geometry + Int32(source.Data, geometry);
            for (int group = 0; group < 8; group++)
            {
                int count = Int32(source.Data, cursor);
                Assert.Equal(expectedGroups[geometryIndex][group], count);
                cursor += 4 + count * lengths[group];
            }
            Assert.Equal(geometryIndex == 3 ? table : Int32(source.Data, 12 + geometryIndex * 4), cursor);
        }
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560) { NativeQuezacotlResources = resources }, null, null) { Pools = [child], Quezacotl = new(1, 3690, 3690) { Billboard17B0 = identity } };
        NativeBattleQuezacotlWorkspaceWrite initialized = NativeBattleQuezacotlWorkspace.Write(memory, effect, NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.TenthLength).Initialize7016B0(source, NativeBattleQuezacotlWorkspace.TenthLength));
        memory = initialized.Memory;
        effect = initialized.Effect;
        NativeBattleCameraProjection projection = new(identity with { Z = 16000 }, 288, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int attempts = 0;
        int accepted = 0;
        int depthWrites = 0;
        for (int frame = 0; frame < 36; frame++)
        {
            NativeBattleGfAnimationAdvance advanced = NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.TenthLength).Advance701970(source, 0, (current, node) =>
            {
                NativeBattleQuezacotlWorkspaceWrite nodeWrite = NativeBattleQuezacotlWorkspace.Write(memory, effect, current);
                memory = nodeWrite.Memory;
                effect = nodeWrite.Effect;
                NativeBattleQuezacotlGeometryStep geometry = NativeBattleQuezacotlGeometry.Emit6C6060(memory, effect, node, source, projection, colors, new(0, 0, 3690), depthBias: -1024, observeDepth: _ => depthWrites++);
                memory = geometry.Memory;
                attempts += geometry.Batch.Attempts.Length;
                accepted += geometry.Batch.Attempts.Count(attempt => attempt.Primitive is not null);
                return current;
            });
            Assert.Equal(10, advanced.EmittedNodes);
            NativeBattleGfAnimationWorkspace workspace = frame == 17 ? JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(advanced.Workspace))! : advanced.Workspace;
            NativeBattleQuezacotlWorkspaceWrite frameWrite = NativeBattleQuezacotlWorkspace.Write(memory, effect, workspace);
            memory = frameWrite.Memory;
            effect = frameWrite.Effect;
        }
        NativeBattleGfAnimationWorkspace completed = NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.TenthLength);
        Assert.Equal(36, completed.Frame);
        Assert.Equal(0x2108954u, completed.ResourceAddress);
        Assert.Equal(0, completed.Advance701970(source, 0, (_, _) => throw new InvalidOperationException("The native resource duration is complete.")).EmittedNodes);
        Assert.True(attempts > 0);
        Assert.True(depthWrites > 0);
        Assert.Equal(source.Data.ToArray(), NativeBattleQuezacotlWorkspace.Source(memory, source.Address, file.Data.Length).Data.ToArray());
        Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(completed))));
        Console.WriteLine($"Actual MAG115_H.22 final workspace: frames=36, nodes=10, attempts={attempts}, accepted={accepted}, depthWrites={depthWrites}");
    }

    private static int Int32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
