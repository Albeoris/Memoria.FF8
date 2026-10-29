using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRemainingWaveSmoothPresentationTests
{
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleCameraProjection Projection => new(Identity, 100, 160, 108);

    [Fact]
    public void Raldo_captures_final_deformed_scratch_and_preserves_every_native_owner()
    {
        NativeBattleRaldo19BallPreparation prepared = Prepare();
        prepared = prepared with { BallC4 = Word(prepared.BallC4, 0xA4, 2048) };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 0xA8);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F40);
        NativeBattleRaldo19BallDraw plain = Draw(prepared, temporary, scratch);
        NativeBattleRaldo19BallDraw captured = Draw(prepared, temporary, scratch, VisualIdentity(19));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive packet = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
        NativeBattleVector final = Vector(captured.VertexScratch, 0);
        Assert.Equal(new Vector3(final.X, final.Y, final.Z + 2000), geometry.A);
        Assert.NotEqual(new Vector3(-400, -400, 2000), geometry.A);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raldo_biased_D8_capture_preserves_packet_bytes_signed_bias_and_temporary_writes(bool extended)
    {
        var fixture = RenderingFixture(2000, extended);
        NativeBattleRaldo19DebrisHead head = NativeBattleRaldo19DebrisRendering.PrepareHead(fixture.Frame, fixture.Task, fixture.Camera, fixture.Temporary);
        NativeBattleCameraProjection projection = new(Identity, 100, 160, 108);
        NativeBattleRaldo19DebrisDraw plain = NativeBattleRaldo19DebrisRendering.Draw(head, fixture.Resources, projection, 0, 17);
        NativeBattleRaldo19DebrisDraw captured = NativeBattleRaldo19DebrisRendering.Draw(head, fixture.Resources, projection, 0, 17, NativeBattlePilotVisualLifetimes.Identity(19, fixture.Task));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive[] quads = captured.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 10).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new[] { 138, 134 }, quads.Select(quad => quad.Bucket));
        Assert.All(quads, quad => Assert.NotNull(quad.PilotGeometry));
        Assert.NotEqual(quads[0].PilotGeometry!.Identity, quads[1].PilotGeometry!.Identity);
        Assert.All(captured.Batch.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 2), attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Fact]
    public void Aqua_sprite_capture_retains_authored_frame_material_and_native_bucket()
    {
        NativeBattleModulePoolTask task = AquaSpriteTask(2000);
        NativeBattleCameraProjection projection = new(Identity, 256, 160, 108);
        NativeBattleAquaSpriteComposition plain = NativeBattleAquaRendering.ComposeSprite(task, AquaResources(), Identity, projection, 0);
        NativeBattleAquaSpriteComposition captured = NativeBattleAquaRendering.ComposeSprite(task, AquaResources(), Identity, projection, 0, VisualIdentity(72));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive packet = Assert.Single(captured.Batch.Attempts).Primitive!;
        Assert.Equal(138, packet.Bucket);
        Assert.NotNull(packet.PilotGeometry);
        Assert.Equal((ushort)72, packet.PilotGeometry.Identity.Effect);
    }

    [Fact]
    public void Aqua72_wave_captures_mutated_selected_source_without_opting_in_enemy55()
    {
        NativeBattleGfGeometryBatch enemy = DrawWave(55);
        NativeBattleGfGeometryBatch blue = DrawWave(72);
        Assert.Equal(NativeBattleTestSnapshots.Json(enemy), NativeBattleTestSnapshots.Json(blue));
        Assert.Null(Assert.Single(enemy.Attempts).Primitive!.PilotGeometry);
        NativeBattlePilotVisualGeometry geometry = Assert.Single(blue.Attempts).Primitive!.PilotGeometry!;
        Assert.NotNull(geometry);
        Assert.Equal(new Vector3(-64, -64, 2000), geometry.A);
        Assert.Equal(new Vector3(64, 64, 2000), geometry.D);

        NativeBattleGfGeometryBatch DrawWave(ushort effect)
        {
            byte[] mesh = new byte[10600];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 2504);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 312);
            for (int corner = 0; corner < 4; corner++)
            {
                int offset = 8 + corner * 8;
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset), (short)((corner & 1) == 0 ? -64 : 64));
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset + 2), (short)(corner < 2 ? -64 : 64));
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset + 4), 2000);
            }
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(2516), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(2520), 0x2E808080);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(2524 + corner * 2), (ushort)(corner * 2));
            }
            NativeBattleAquaState retained = NativeBattleAquaState.FromColdProcess(effect) with { WaveMesh = mesh.ToImmutableArray() };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
            memory = effect == 55 ? memory with { Aqua55 = retained } : memory with { Aqua72 = retained };
            NativeBattleAquaFrame frame = new(memory, new(effect, effect, new(effect, 0, 0), null, 0), NativeCrtRandomState.FromColdProcess());
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 340, 1);
            (pool, int slot) = pool.Append(0x3000);
            NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(pool.Slots[slot], data => data with { Payload = new byte[328].ToImmutableArray() }).WriteDword(0x4C, frame.Profile.MeshAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x50, 4096);
            List<NativeBattleAquaOperation> operations = [];
            NativeBattleAquaInputs inputs = new(NativeBattleActionPacketsState.Fresh(), Identity, Projection, 0, Constants, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (current, operation) =>
            {
                operations.Add(operation);
                return current;
            });
            NativeBattleAquaRendering.DrawWave(frame, task, AquaResources() with { Effect = effect }, inputs);
            return Assert.Single(operations.OfType<NativeBattleAquaPackets>()).Batch;
        }
    }

    private static NativeBattlePilotVisualIdentity VisualIdentity(ushort effect)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 64, 1);
        (pool, int slot) = pool.Append(0x3000);
        return NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[slot]);
    }

    private static NativeBattleRaldo19BallPreparation Prepare()
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [0x11, 0x22, 0x33] };
        return NativeBattleRaldo19BallRendering.Prepare(Ball().MarkUnknown(0x52, 2), Identity with { Z = 2000 }, 0x1234, Identity, 0x5678, new(NativeBattleGeometryRegisters.Unknown(), colors));
    }

    private static NativeBattleRaldo19BallDraw Draw(NativeBattleRaldo19BallPreparation prepared, NativeBattleEffectArena temporary, NativeBattleEffectArena scratch, NativeBattlePilotVisualIdentity? identity = null)
    {
        return NativeBattleRaldo19BallRendering.DrawMeshes(prepared.BallC4, temporary, scratch, [Mesh()], [Identity, Identity with { X = 100, Y = 200, Z = 300 }], _ => 0x4455, 1, 0x00808080, Projection, 0, 0, prepared.Registers, NativeBattleScriptedLightingMemory.Unknown(), Constants, identity);
    }

    private static NativeBattleRawMesh Mesh()
    {
        NativeBattleVector[] points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        byte[] packed = new byte[30];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 4), points[index].Z);
        }
        byte[] polygon = new byte[20];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(8), 0x12340302);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(12), 0x56780605);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(16), 0x0A090807);
        return new(points.Select(point => new NativeBattleRawVertex(point, 0)).ToImmutableArray(), [new(true, polygon.ToImmutableArray())]) { PackedVertexGroups = [new(0, 4, packed.ToImmutableArray()), new(1, 0, new byte[6].ToImmutableArray())] };
    }

    private static NativeBattleEffectArena Ball()
    {
        NativeBattleEffectArena ball = NativeBattleEffectArena.Clear().Write(0, new byte[0xC4]);
        foreach (int offset in new[] { 0, 8, 16 })
        {
            ball = Word(ball, offset, 4096);
        }
        ball = Dword(ball, 0x64, NativeBattleRaldo19BallRendering.DescriptorAddress);
        ball = Dword(ball, 0x68, 0x1D972C0 + 3 * 0x9C);
        ball = Dword(ball, 0x6C, 0x1D8E054);
        ball = Word(ball, 0xAC, 3);
        foreach (int offset in new[] { 0xA6, 0xA8, 0xAA })
        {
            ball = Word(ball, offset, 400);
        }
        return ball;
    }
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(Short(arena, offset), Short(arena, offset + 2), Short(arena, offset + 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static (NativeBattleRaldo19Frame Frame, NativeBattleModulePoolTask Task, NativeBattlePackedMatrix32 Camera, NativeBattleEffectArena Temporary, NativeBattleRaldo19Resources Resources) RenderingFixture(short depth, bool extended)
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        (NativeBattleRaldo19Frame frame, uint parent) = Spawner(fixture.Frame, fixture.Inputs.Packets);
        frame = NativeBattleRaldo19Debris.Advance(frame, parent, fixture.Inputs.Packets, (current, _) => current).Frame;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        NativeBattleModulePoolTask task = Assert.Single(frame.Effect.Pools[4].Slots, item => item.Callback == 0x8AAA30);
        task = task.WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, unchecked((ushort)depth)).WriteDword(0x4C, 0x162A0C0).WriteWord(0x50, 0).WriteWord(0x56, 0xFFF0);
        byte[] sprite = new byte[0x144];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 12);
        for (int frameIndex = 0; frameIndex <= 12; frameIndex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + 2 * frameIndex), 36);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(36), extended ? 0x80000002u : 2);
        for (int quad = 0; quad < 2; quad++)
        {
            Span<byte> row = sprite.AsSpan(40 + 20 * quad, 20);
            row[0] = row[2] = 2;
            row[1] = 100;
            row[3] = 0x2C;
            row[4] = 10;
            row[5] = 20;
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(14), quad == 0 ? (ushort)0x123 : (ushort)0xD23);
            if (quad == 1)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(12), 1024);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(16), 4096);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(18), 2048);
            }
        }
        return (frame, task, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0x1234), NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 216).ToArray()), fixture.Resources with { DebrisLow = sprite.ToImmutableArray() });
    }

    private static (NativeBattleRaldo19Frame Frame, uint Address) Spawner(NativeBattleRaldo19Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint parent = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend created = hierarchy.Append8DC540(4, 0x8A9F10, 184, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(frame.Effect.Parameters!, packets, pointer, group, target));
        NativeBattleModulePoolTask task = created.Hierarchy.Read(created.Address).WriteDword(0x1C, 0x11112222).WriteDword(0x20, 0x33334444).WriteWord(0xB6, 1);
        return (frame with { Effect = frame.Effect with { Pools = created.Hierarchy.Write(created.Address, task).Pools } }, created.Address);
    }

    private static NativeBattleModulePoolTask AquaTask() => new(1, 0, null, 1, new byte[328].ToImmutableArray());
    private static NativeBattleModulePoolTask AquaSpriteTask(short depth) => AquaTask().WriteDword(0x4C, 0x15A38DC).WriteWord(0x20, unchecked((ushort)depth)).WriteWord(0x56, 0xFFF0);

    private static NativeBattleAquaResources AquaResources()
    {
        byte[] bytes = new byte[40];
        bytes[4] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1);
        bytes[20] = 8;
        bytes[21] = 64;
        bytes[22] = 8;
        bytes[23] = 0x2E;
        bytes[24] = 20;
        bytes[25] = 30;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 128);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), -4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(30), -4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 0x400);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 4096);
        return new(55, new("aqua-camera", []), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x15A38DC, bytes.ToImmutableArray()), [], [], 0, null);
    }
}
