using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79ReadWindowMeshTests
{
    [Fact]
    public void Staging_reader_keeps_original_group_headers_while_actual_source_memory_changes()
    {
        const uint address = 0x30000000;
        byte[] geometry = new byte[56];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 24);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 2);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(0, new byte[104]);
        context.WriteUInt32(0, address);
        context.WriteUInt32(20, 0x2030);
        NativeBattleEffectArena contextSnapshot = context.Capture();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectMemory memorySnapshot = NativeBattleStorageSnapshots.Capture(memory);
        NativeBattleCameraProjection projection = new(null, 100, 160, 108);
        NativeBattleEffect79Mesh104Step expected = NativeBattleEffect79Mesh104.DrawLoaded(memory, new(0, address, geometry.ToImmutableArray()), projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, geometry);
        using NativeBattleGfSourceReader reader = NativeBattleGfAnimationSource.Borrow(address, staging, geometry.Length).OpenReader();
        staging.Write(address + 24, [1, 0, 0, 0]);
        NativeBattleEffect79Mesh104Step actual = NativeBattleEffect79Mesh104.DrawLoaded(NativeBattleStorageSnapshots.Restore(memorySnapshot), address, reader, projection, NativeBattleStorageSnapshots.Restore(contextSnapshot), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address + 24, 4)));
    }
}
