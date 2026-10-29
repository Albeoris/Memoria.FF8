using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleQuezacotlFragmentMeshAssetsTests
{
    [Fact]
    public void Actual525_current_executable_mesh_emits_original_twenty_F3_attempts_and_survives_independent_restore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectFileAsset file = game.ReadNativeQuezacotlResources().Files.Single(file => file.FileId == 525);
        Assert.Equal("MAG115_H.20", file.Name);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!;
        uint address = NativeBattleQuezacotlFragmentPackets.MeshAddress;
        int meshOffset = checked((int)(address - 0x12A9664));
        Assert.Equal(0x3254, meshOffset);
        int coldHeader = BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(memory.QuezacotlExecutableMemory!).Data.AsSpan().Slice(checked((int)(address - NativeBattleQuezacotlExecutableMemory.Address)), 4));
        Assert.NotEqual(0x68, coldHeader);
        memory = memory with { QuezacotlExecutableMemory = memory.QuezacotlExecutableMemory!.Write(0x12A9664, NativeBattleStorageSnapshots.Capture(file).Data.AsSpan()) };
        ReadOnlySpan<byte> mesh = NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(meshOffset, 376);
        Assert.Equal(0x68, BinaryPrimitives.ReadInt32LittleEndian(mesh));
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(mesh[4..]));
        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(mesh[0x68..]));
        Assert.All(Enumerable.Range(0, 7), group => Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(file).Data.AsSpan().Slice(meshOffset + 0x68 + 4 + 20 * 12 + group * 4, 4))));
        ImmutableArray<byte> task = new byte[44].ToImmutableArray();
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        List<ushort> depths = [];
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, task, projection, depths.Add);
        Assert.Equal(20, batch.Attempts.Length);
        Assert.Equal(60, depths.Count);
        Assert.True(batch.AllocationBytes > 0);
        Assert.Equal(batch.Attempts.Count(attempt => attempt.Primitive is not null) * 20, batch.AllocationBytes);
        for (int face = 0; face < 20; face++)
        {
            NativeBattleGfGeometryAttempt attempt = batch.Attempts[face];
            uint sourceColor = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Slice(0x6C + face * 12, 4));
            Assert.Equal(sourceColor, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(attempt).Data.AsSpan().Slice(4, 4)));
            Assert.Equal(0x04000000u, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(attempt).Data.AsSpan()));
            Assert.Equal(20, attempt.PacketLength);
            Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(memory))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(batch), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlFragmentPackets.Emit6C6660(restored, task, projection)));
        uint changedColor = BinaryPrimitives.ReadUInt32LittleEndian(mesh[0x6C..]) ^ 0x00010101u;
        byte[] color = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(color, changedColor);
        NativeBattleEffectMemory changedInput = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(memory));
        NativeBattleEffectMemory changed = changedInput with { QuezacotlExecutableMemory = changedInput.QuezacotlExecutableMemory!.Write(address + 0x6C, color) };
        NativeBattleGfGeometryBatch changedBatch = NativeBattleQuezacotlFragmentPackets.Emit6C6660(changed, task, projection);
        Assert.Equal(changedColor, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(changedBatch.Attempts[0]).Data.AsSpan().Slice(4, 4)));
        Assert.NotEqual(NativeBattleSnapshotJson.Serialize(batch), NativeBattleSnapshotJson.Serialize(changedBatch));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(batch), NativeBattleSnapshotJson.Serialize(NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, task, projection)));
    }
}
