using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22GeometryPacketTests
{
    [Theory]
    [InlineData(false, (short)0)]
    [InlineData(false, (short)2048)]
    [InlineData(false, (short)4096)]
    [InlineData(true, (short)0)]
    [InlineData(true, (short)2048)]
    [InlineData(true, (short)4096)]
    public void Original_current_mesh_runs_both_exact_GT3_producers_and_independent_next_morph(bool secondMesh, short ratio)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleEffectHeapAllocation first = resources.MorphSources[secondMesh ? 5 : 0];
        NativeBattleEffectHeapAllocation second = resources.MorphSources[secondMesh ? 6 : 1];
        NativeBattleBio22Geometry current = NativeBattleBio22Geometry.FromColdProcess(secondMesh ? resources.SecondMeshBaseline : resources.MeshBaseline).Morph89B0D0(first.Data, second.Data, ratio);
        NativeBattleModulePoolTask task = Task();
        NativeBattleCameraProjection projection = new(NativeBattleGfMatrices.Euler56CD50(new(512, 0, 0)) with { Z = 2048 }, 256, 160, 112);
        NativeBattleGfGeometryBatch ordinary = NativeBattleBio22GeometryPackets.Emit89AB90(task, current.Bytes, projection, 341);
        NativeBattleGfGeometryBatch deformed = NativeBattleBio22GeometryPackets.Emit89AD30(task, current.Bytes, projection, 341);
        foreach (NativeBattleGfGeometryBatch batch in new[] { ordinary, deformed })
        {
            Assert.NotEmpty(batch.Attempts);
            Assert.Equal(batch.Attempts.Length * 40, batch.AllocationBytes);
            Assert.InRange(batch.Attempts.Length, 1, secondMesh ? 80 : 160);
            Assert.All(batch.Attempts, attempt =>
            {
                Assert.Equal(0x09000000u, attempt.Primitive!.Words[0]);
                Assert.All(attempt.WriteMask.Skip(38), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
            });
        }
        NativeBattleBio22Geometry restored = JsonSerializer.Deserialize<NativeBattleBio22Geometry>(JsonSerializer.Serialize(current))!;
        NativeBattleBio22Geometry next = current.Morph89B0D0(first.Data, second.Data, 3072);
        NativeBattleBio22Geometry coldNext = restored.Morph89B0D0(first.Data, second.Data, 3072);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleBio22GeometryPackets.Emit89AB90(task, next.Bytes, projection, 341)), JsonSerializer.Serialize(NativeBattleBio22GeometryPackets.Emit89AB90(task, coldNext.Bytes, projection, 341)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleBio22GeometryPackets.Emit89AD30(task, next.Bytes, projection, 341)), JsonSerializer.Serialize(NativeBattleBio22GeometryPackets.Emit89AD30(task, coldNext.Bytes, projection, 341)));
        Assert.Equal(current.Bytes.ToArray(), restored.Bytes.ToArray());
    }

    private static NativeBattleModulePoolTask Task()
    {
        byte[] payload = new byte[0x98 - 12];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0x20 - 12), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0x46 - 12), 512);
        foreach (int offset in new[] { 0x30, 0x34, 0x38 })
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset - 12), 4096);
        }
        return new(1, 0, null, 0x89AAE0, payload.ToImmutableArray());
    }
}
