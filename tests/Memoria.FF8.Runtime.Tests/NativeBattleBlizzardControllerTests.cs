using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzardControllerTests
{
    [Theory]
    [InlineData(0, 1234, -567, 1234, -567)]
    [InlineData(1024, 1234, -567, 567, 1234)]
    public void FaceOrbitCallsNativeCosineThenSineAndStoresEachSignedWord(short angle, short x, short z, short expectedX, short expectedZ)
    {
        Assert.Equal((expectedX, expectedZ), NativeBattleBlizzardFaceMotion.Orbit61BAC7(angle, x, z));
    }

    [Fact]
    public void MainWordCounterWrapsEvenWhenPausedAndSelectsItsNativeAlternateBuffer()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool main = fixture.Frame.Effect.Pools[0];
        main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, ushort.MaxValue)) };
        NativeBattleBlizzardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [main, fixture.Frame.Effect.Pools[1]] } };
        frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets) with { GraphicsFlags = 0x201 });
        Assert.Equal(0xC000, frame.Effect.DrawArenaOffset);
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Null(frame.Memory.BlizzardFaces);
    }

    [Fact]
    public void RepeatedHeaderIsAppendedAt28AndInitializedInThatSamePhysicalWalk()
    {
        var fixture = Fixture();
        NativeBattleActionPacketsState packets = fixture.Packets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 4, 144, null));
        (packets, _) = packets.AppendContact(new(new(4, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 2);
        NativeBattleBlizzardFrame frame = fixture.Frame;
        for (int tick = 0; tick <= 28; tick++)
        {
            frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(packets));
        }
        NativeBattleModulePoolTask repeated = Assert.Single(frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x61AC70 && task.ReadWord(14) == 1));
        Assert.Equal((ushort)1, repeated.ReadWord(12));
        Assert.Equal((ushort)4, repeated.ReadWord(32));
        Assert.Equal(2560, frame.Memory.BlizzardFaces!.Read(1).Length);
    }
    [Fact]
    public void EntryAndCompleteSingleHeaderWalkUseOriginalPools602DrawsAndRealSurvivors()
    {
        var fixture = Fixture();
        NativeBattleBlizzardFrame frame = fixture.Frame;
        Assert.Equal(0x24BD778u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)16, frame.Effect.Pools[0].Stride);
        Assert.Equal((ushort)36, frame.Effect.Pools[1].Stride);
        Assert.Equal(150, frame.Effect.Pools[1].Slots.Length);
        List<(int Tick, NativeBattleBlizzardOperation Operation)> operations = [];
        int tick = 0;
        for (; tick < 80 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets, (current, operation) =>
            {
                operations.Add((tick, operation));
                return current;
            }));
            if (tick == 20)
            {
                frame = JsonSerializer.Deserialize<NativeBattleBlizzardFrame>(JsonSerializer.Serialize(frame))!;
            }
        }
        Assert.Equal(55, tick);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)55, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(37, Assert.Single(operations, operation => operation.Operation is NativeBattleBlizzardContact).Tick);
        Assert.Equal(1, Assert.Single(operations, operation => operation.Operation is NativeBattleBlizzardSound).Tick);
        Assert.Equal(38, Assert.Single(operations, operation => operation.Operation is NativeBattleBlizzardCameraImpulse).Tick);
        Assert.Contains(operations, operation => operation.Tick == 1 && operation.Operation is NativeBattleBlizzardDraw { Task.Callback: 0x61B740 });
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 602; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.NotNull(frame.Memory.BlizzardFaces);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void PhaseZeroWritesAll80RecordsWithoutReadingOldBankAndPreservesOtherSequences()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        var first = NativeBattleBlizzardFaceMotion.Initialize61B190(null, 0, mesh, new(1));
        byte[] original = first.Faces.Read(0);
        Assert.Equal(2560, original.Length);
        Assert.Throws<NotSupportedException>(() => first.Faces.Read(1));
        var second = NativeBattleBlizzardFaceMotion.Initialize61B190(first.Faces, 1, mesh, first.Random);
        Assert.Equal(original, second.Faces.Read(0));
        Assert.NotEqual(original, second.Faces.Read(1));
        for (int index = 0; index < 80; index++)
        {
            ReadOnlySpan<byte> row = original.AsSpan(index * 32, 32);
            Assert.Equal(20, BinaryPrimitives.ReadInt16LittleEndian(row));
            Assert.Equal(-600, BinaryPrimitives.ReadInt16LittleEndian(row[2..]));
            Assert.Equal(100, BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
            Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(row[14..]));
            Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(row[22..]), (short)12, (short)19);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzardFaceMotion.Initialize61B190(first.Faces, 3, mesh, first.Random));
    }

    [Theory]
    [InlineData(0x1u)]
    [InlineData(0x200u)]
    public void PauseBlocksControllerButStillDrawsLeavesBeforeTheirMotion(uint flags)
    {
        var fixture = Fixture();
        NativeBattleBlizzardFrame frame = NativeBattleBlizzardController.Advance61BF70(fixture.Frame, Inputs(fixture.Packets));
        frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets));
        NativeBattleOriginalEffectState before = frame.Effect;
        List<NativeBattleBlizzardOperation> operations = [];
        NativeBattleBlizzardFrame frozen = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        }) with { GraphicsFlags = flags });
        Assert.Single(operations.OfType<NativeBattleBlizzardDraw>());
        Assert.Equal(frame.Random, frozen.Random);
        Assert.Equal(before.Pools[1].Slots.Select(slot => slot.Payload), frozen.Effect.Pools[1].Slots.Select(slot => slot.Payload));
        Assert.Equal(unchecked((ushort)(before.Pools[0].Slots[0].ReadWord(12) + 1)), frozen.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void PositiveDelayHasNoDrawAndOnlyUnpausedDelayChanges()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24BD788, 0x24BD798, 36, 150);
        (pool, int slot) = pool.Append(0x61B660);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(14, 1)) };
        NativeBattleBlizzardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [fixture.Frame.Effect.Pools[0], pool] } };
        frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets, (_, _) => throw new InvalidOperationException("Positive delay must not draw.")));
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[slot].ReadWord(14));
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[slot].ReadWord(12));
    }

    [Fact]
    public void CurrentFaceMotionWritesAnglesAndAgeBeforeProjectionWhilePausePreservesItsWholeSlice()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleBlizzardFaces faces = NativeBattleBlizzardFaceMotion.Initialize61B190(null, 0, mesh, new(1)).Faces;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { BlizzardFaces = faces };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x61B740, new byte[24].ToImmutableArray());
        task = task.WriteWord(12, 20).WriteWord(28, 4096);
        NativeBattleModuleResource sprite = new(0, 0, 0, 0, [0, 0, 0, 0]);
        NativeBattleBlizzardResources resources = new(new("fixture", []), sprite, sprite, mesh, mesh, mesh, 0);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 5000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = new(0, 0, 1, 4503601774854144d);
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleEffectMemory after = NativeBattleBlizzardFaceMotion.Draw61B9A0(memory, task, resources, 0, view, projection, colors, batches.Add);
        Assert.Equal(80, batches.Count);
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(after.BlizzardFaces!.Read(0).AsSpan(14)));
        Assert.NotEqual(faces.Read(0), after.BlizzardFaces.Read(0));
        NativeBattleEffectMemory paused = NativeBattleBlizzardFaceMotion.Draw61B9A0(memory, task, resources, 0x201, view, projection, colors, _ =>
        {
        });
        Assert.Equal(faces.Read(0), paused.BlizzardFaces!.Read(0));
    }

    [Theory]
    [InlineData(0x61B3A0u, 14)]
    [InlineData(0x61B4F0u, 16)]
    [InlineData(0x61B5B0u, 16)]
    [InlineData(0x61B660u, 8)]
    [InlineData(0x61B740u, 48)]
    public void EveryLeafDrawsItsLastPhaseThenReleasesAtItsOwnSignedWordLifetime(uint callback, int lifetime)
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24BD788, 0x24BD798, 36, 150);
        (pool, int slot) = pool.Append(callback);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, (ushort)(lifetime - 1))) };
        NativeBattleBlizzardFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [fixture.Frame.Effect.Pools[0], pool] } };
        int draws = 0;
        frame = NativeBattleBlizzardController.Advance61BF70(frame, Inputs(fixture.Packets, (current, operation) =>
        {
            Assert.IsType<NativeBattleBlizzardDraw>(operation);
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)lifetime, frame.Effect.Pools[1].Slots[slot].ReadWord(12));
    }

    internal static NativeBattleStaticMeshResource Mesh()
    {
        const int vertexEnd = 32;
        byte[] bytes = new byte[vertexEnd + 32 + 80 * 20];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, vertexEnd);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + 8 * index), (short)(10 + index * 10));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + 8 * index), -600);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + 8 * index), 100);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(vertexEnd + 8), 80);
        for (int index = 0; index < 80; index++)
        {
            int face = vertexEnd + 12 + index * 20;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
        }
        return new(0, 0xDD8590, bytes.ToImmutableArray());
    }

    private static NativeBattleBlizzardInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleBlizzardFrame, NativeBattleBlizzardOperation, NativeBattleBlizzardFrame>? execute = null) => new(0, packets, _ => new(100, -200, 300), _ => new(new(0, 0, 0), new(100, -200, 300), 0, -50, 4096), execute ?? ((frame, _) => frame));

    private static (NativeBattleBlizzardFrame Frame, NativeBattleActionPacketsState Packets) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 4, 144, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleModuleResource sprite = new(0, 0xDD82F0, 0, 0, [0, 0, 0, 0]);
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleBlizzardResources resources = new(new("fixture", []), sprite, sprite, mesh, mesh, mesh, 300012);
        NativeBattleOriginalEffectState effect = new(144, 143, new(144, 0x61ABA0, 0x61AB80) { BlizzardResources = resources }, new(0, 0, 8, 0, 0, 0), null);
        effect = NativeBattleBlizzardController.Initialize61ABA0(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1)), packets);
    }
}
