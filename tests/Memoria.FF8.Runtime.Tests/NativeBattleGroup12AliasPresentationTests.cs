using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup12AliasPresentationTests
{
    [Fact]
    public void LevelDeath74_dark_body_keeps_actual_task_generation_and_consumed_source_corners()
    {
        var fixture = NativeBattleDeathDrawingTests.Fixture(4096, 74);
        NativeBattleDeathDraw Draw() => NativeBattleDeathDrawing.Dark(fixture.Frame, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, NativeBattleDeathDrawingTests.Shadow, new(true));
        NativeBattleDeathDraw first = Draw();
        NativeBattleDeathDraw second = Draw();
        NativeBattleGeometryPrimitive body = Assert.Single(first.Packets.Attempts.Where(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C)).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(body.PilotGeometry);
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(74, fixture.Task);
        Assert.Equal(owner.Effect, geometry.Identity.Effect);
        Assert.Equal(owner.Bank, geometry.Identity.Bank);
        Assert.Equal(owner.Slot, geometry.Identity.Slot);
        Assert.Equal(owner.Generation, geometry.Identity.Generation);
        Assert.Equal(new Vector3(-50, -100, 0), geometry.A);
        Assert.Equal(new Vector3(50, 50, 0), geometry.D);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Frame), NativeBattleTestSnapshots.Json(second.Frame));
        Assert.Equal(geometry.Identity, second.Packets.Attempts.Single(attempt => attempt.Primitive?.PilotGeometry?.Identity == geometry.Identity).Primitive!.PilotGeometry!.Identity);
    }

    [Fact]
    public void LevelDeath74_actor_mesh_capture_preserves_native_packet_bytes_masks_and_registers()
    {
        var fixture = NativeBattleDeathDrawingTests.Fixture(4096, 74);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 25, Z = 2000 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        const uint meshAddress = 0x1819000;
        NativeBattleDeathFrame frame = NativeBattleDeathNodeDrawing.PublishCamera(fixture.Frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        frame = NativeBattleDeathNodeDrawing.Begin(frame, meshAddress, 0, 2048, 0xC08040);
        ImmutableArray<byte> mesh = NativeBattleDeathNodeDrawingTests.Mesh();
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(74, fixture.Task, 9);
        NativeBattleDeathNodeMeshStep plain = NativeBattleDeathNodeDrawing.Emit(frame, mesh, meshAddress, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x4321);
        NativeBattleDeathNodeMeshStep captured = NativeBattleDeathNodeDrawing.Emit(frame, mesh, meshAddress, matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x4321, identity);
        NativeBattleGfGeometryAttempt expected = Assert.Single(plain.Batch.Attempts);
        NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
        Assert.Equal(expected.WriteMask.ToArray(), actual.WriteMask.ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Frame), NativeBattleTestSnapshots.Json(captured.Frame));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive!.PilotGeometry);
        Assert.Equal((ushort)74, geometry.Identity.Effect);
        Assert.Equal(new Vector3(-375, -400, 2000), geometry.A);
        Assert.Equal(new Vector3(425, 400, 2000), geometry.D);
    }

    [Fact]
    public void Actor81_draw_only_selects_wave_capture_and_retains_native_GT4_packet_and_owner()
    {
        var fixture = NativeBattleActorDirectorGenericTests.Fixture(81);
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(81);
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Address, 0, 0, 0);
        NativeBattleCuragaNodeAllocation allocated = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Address, emitter.ArenaOffset!.Value, 0);
        NativeBattleCuragaFrame frame = allocated.Frame;
        int node = allocated.ArenaOffset!.Value;
        byte[] bank = new byte[profile.ExecutableLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bank, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(8), profile.ExecutableAddress + 0x300);
        bank[0x316] = 4;
        NativeBattleDeathNodeDrawingTests.Mesh().AsSpan().CopyTo(bank.AsSpan(0x400));
        NativeBattleCuragaExecutableState executable = new(1, bank.ToImmutableArray()) { ResourceAddress = profile.ExecutableAddress };
        frame = frame with { Memory = profile.WithExecutable(frame.Memory, executable) };
        frame = frame with { Memory = frame.Memory with { ScriptedGraphics = frame.Memory.ScriptedGraphics!.InitializeGeom56BAA0(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() } };
        using (NativeBattleCuragaNodeMemory memory = new(frame, fixture.Address))
        {
            memory.ContextDword(0x224, profile.ExecutableAddress + 8);
            memory.Dword(node + 0x170, profile.ExecutableAddress + 0x400);
            memory.Byte(node + 0x1D8, 1);
            memory.Word(node + 0x1CE, 0);
            byte[] matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 0).Bytes.ToArray();
            for (int index = 0; index < matrix.Length; index++)
            {
                memory.Byte(node + 0xAC + index, matrix[index]);
            }
            frame = memory.Flush();
        }
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleCuragaNodeDrawStep drawn = NativeBattleCuragaNodeKinematics.DrawNodes880390And87ED10(frame, fixture.Address, projection, 0, NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.Single(drawn.Batch.Attempts).Primitive!;
        Assert.Equal(0x3C332211u, primitive.Words[1]);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(81, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address));
        Assert.Equal(owner.Effect, geometry.Identity.Effect);
        Assert.Equal(owner.Bank, geometry.Identity.Bank);
        Assert.Equal(owner.Slot, geometry.Identity.Slot);
        Assert.Equal(new Vector3(-400, -400, 2000), geometry.A);
        Assert.Equal(new Vector3(400, 400, 2000), geometry.D);
    }
}
