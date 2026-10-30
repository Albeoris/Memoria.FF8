using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187OpeningGridTests
{
    [Fact]
    public void WrappedTextureEdgesCropCellsAndKeepUnusedUvWordsUntouched()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(255, false);
        Assert.Equal(1024, step.Batch.Attempts.Length);
        Assert.Equal(40960, step.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt first = step.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt next = step.Batch.Attempts[1];
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(12, 2)));
        Assert.Equal((ushort)0xFF00, BinaryPrimitives.ReadUInt16LittleEndian(next.Data.AsSpan(12, 2)));
        Assert.Equal((ushort)0x3FD4, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(14, 2)));
        Assert.Equal((ushort)0xB6, BinaryPrimitives.ReadUInt16LittleEndian(first.Data.AsSpan(22, 2)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.WriteMask[39]);
        Assert.Throws<NotSupportedException>(() => step.Context120.Read(0x5A, 2));
        Assert.False(step.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
    }

    [Fact]
    public void OpeningGridDoesNotIntroduceAnAreaRejection()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(0, false);
        Assert.All(step.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(0x2E404040u, BinaryPrimitives.ReadUInt32LittleEndian(step.Batch.Attempts[0].Data.AsSpan(4, 4)));
        Assert.Equal(step.Memory.GeometryRegisters!.Otz, step.Memory.GeometryRegisters.Mac0);
    }

    [Fact]
    public void NearRejectedCellsReuseAllocationCursorAndPublishOnlyPrefix()
    {
        NativeBattleEffect187OpeningGridStep step = Draw(0, true);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.Equal(1024, step.Batch.Attempts.Length);
        Assert.All(step.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
        Assert.Throws<NotSupportedException>(() => step.Context120.Read(0x4C, 4));
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(step.Context120.Read(0x50, 4)) & 0x60000);
    }

    [Theory]
    [InlineData(256, 32, 0)]
    [InlineData(256, 32, 255)]
    [InlineData(512, 16, 0)]
    [InlineData(512, 16, 255)]
    public void Captured_grid_preserves_detached_native_state_masks_UV_and_actual_world_plane_depths(int size, int count, int phase)
    {
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 120).Write(0, BitConverter.GetBytes(size)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(count)).Write(12, BitConverter.GetBytes(phase)).Write(16, BitConverter.GetBytes(phase)).Write(0x74, BitConverter.GetBytes(0u));
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var plain = NativeBattleEffect187OpeningGrid.Draw647B10(Memory(), Context(), projection);
        string expected = NativeBattleTestSnapshots.Json(plain);
        NativeBattlePilotVisualIdentity identity = new(187, 0x24FD990, 2, 13, 0) { ResourceGeneration = 0x00B63FD4 };
        var captured = NativeBattleEffect187OpeningGrid.Draw647B10(Memory(), Context(), projection, identity);
        Assert.Equal(expected, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(count * count, captured.Batch.Attempts.Length);
        var primitive = captured.Batch.Attempts[0].Primitive!;
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        int extent = phase == 255 ? size / 32 : size;
        Assert.Equal(new System.Numerics.Vector3(-4096, 0, 15904), visual.A);
        Assert.Equal(new System.Numerics.Vector3(-4096 + extent, 0, 15904 + extent), visual.D);
        Assert.Equal(new System.Numerics.Vector4(15904, 15904, 15904 + extent, 15904 + extent), visual.SourceDepths!.Value);
        Assert.Equal(0x2E000000u, primitive.Words[1]);
        Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.Equal(count * count, captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(captured.Batch.Attempts, attempt =>
        {
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.False(geometry.ScreenSpace);
            Assert.True(geometry.Continuous);
            Assert.True(geometry.UseSourceDepths);
        });
    }

    [Fact]
    public void Captured_grid_keeps_native_first_triple_rejection_and_no_fourth_or_packet_depth_store()
    {
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), new(new short[9].ToImmutableArray(), 0, 0, 0, 4096), true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 120).Write(0, BitConverter.GetBytes(256)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(32)).Write(12, new byte[4]).Write(16, new byte[4]).Write(0x74, new byte[4]);
        var plain = NativeBattleEffect187OpeningGrid.Draw647B10(Memory(), Context(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108));
        string expected = NativeBattleTestSnapshots.Json(plain);
        var captured = NativeBattleEffect187OpeningGrid.Draw647B10(Memory(), Context(), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), new(187, 0x24FD990, 2, 13, 0));
        Assert.Equal(expected, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(0, captured.Batch.AllocationBytes);
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Throws<NotSupportedException>(() => captured.Context120.Read(0x4C, 4));
    }

    [Theory]
    [InlineData(0x6479D0u, 32)]
    [InlineData(0x647E40u, 16)]
    public void Actual_opening_callback_forwards_physical_task_birth_material_and_all_accepted_cells(uint callback, int count)
    {
        NativeBattleEffect187Frame Frame()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24FD990, 0x24FD9A0, 36, 100);
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[24]).WriteWord(0x1C, 4096));
            return new(NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() }, new(187, 186, new(187, 0x6472E0, 0x575570), null, 0) { Pools = [pool] }, new(42), NativeBattleEffect187State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        }
        NativeBattleEffect187Inputs Inputs(bool capture, Action<NativeBattleGfGeometryBatch> receive) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => 0, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }, null), _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), null!, null!, null!, null!, null!, null!, null!, null!, null!, (frame, operation) =>
        {
            var emitted = Assert.IsType<NativeBattleEffect187DeliverBatch>(operation);
            receive(emitted.Batch);
            return frame;
        }) { CapturePresentation = capture };
        NativeBattleGfGeometryBatch? plainBatch = null;
        var plain = NativeBattleEffect187Controller.AdvanceOpening(Frame(), 0x24FD9A0, Inputs(false, batch => plainBatch = batch));
        string expected = NativeBattleTestSnapshots.Json(plain);
        string packets = NativeBattleTestSnapshots.Json(plainBatch!);
        NativeBattleEffect187Frame source = Frame();
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(187, source.Effect.Pools[0].Slots[0]);
        NativeBattleGfGeometryBatch? capturedBatch = null;
        var captured = NativeBattleEffect187Controller.AdvanceOpening(source, 0x24FD9A0, Inputs(true, batch => capturedBatch = batch));
        Assert.Equal(expected, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(packets, NativeBattleTestSnapshots.Json(capturedBatch!));
        Assert.Equal(count * count, capturedBatch!.Attempts.Length);
        Assert.All(capturedBatch.Attempts, attempt =>
        {
            var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.Equal(owner.Generation, visual.Identity.Generation);
            Assert.Equal(owner.Bank, visual.Identity.Bank);
            Assert.Equal(owner.Slot, visual.Identity.Slot);
            Assert.Equal(0x00B63FD4L, visual.Identity.ResourceGeneration);
            Assert.False(visual.ScreenSpace);
        });
        Assert.Equal(plain.Result, captured.Result);
        Assert.Equal(plain.Frame.Random, captured.Frame.Random);
    }

    private static NativeBattleEffect187OpeningGridStep Draw(int phase, bool near)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleActionMatrix matrix = near ? new(new short[9].ToImmutableArray(), 0, 0, 0, 4096) : NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 };
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 120).Write(0, BitConverter.GetBytes(256)).Write(4, BitConverter.GetBytes(32)).Write(8, BitConverter.GetBytes(32)).Write(12, BitConverter.GetBytes(phase)).Write(16, BitConverter.GetBytes(phase)).Write(0x74, BitConverter.GetBytes(0x404040u));
        return NativeBattleEffect187OpeningGrid.Draw647B10(memory, context, new(null, 100, 160, 108));
    }
}
