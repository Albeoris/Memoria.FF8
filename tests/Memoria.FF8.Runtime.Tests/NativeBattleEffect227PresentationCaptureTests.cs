using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect227RecordsTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227PresentationCaptureTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0x200u)]
    public void Captured227_selected_ACT_keeps_actual_nodes_W_native_registers_and_pointer_only_policy(uint header)
    {
        NativeBattleGfAnimationSource source = Source();
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, source.Data.AsSpan(16..).ToArray().ToImmutableArray());
        NativeBattleEffect227Resources resources = Resources() with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        byte[] nodeBytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(nodeBytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(12), 2000);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, nodeBytes.ToImmutableArray());
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(12, 4);
        NativeBattleEffect227Frame Run(bool capture, Harness harness) => NativeBattleEffect227Programme.DrawNode(Frame() with { CapturePresentation = capture, PresentationIdentity = capture ? new(227, 1, 2, 3, 4) : null }, source, node, caller, resources, harness.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108) });
        Harness nativeCalls = new();
        Harness capturedCalls = new();
        NativeBattleEffect227Frame native = Run(false, nativeCalls);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleEffect227Frame captured = Run(true, capturedCalls);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeCalls.Operations), NativeBattleTestSnapshots.Json(capturedCalls.Operations));
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(capturedCalls.Operations)).Batch;
        NativeBattlePilotVisualGeometry visual = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal((ushort)227, visual.Identity.Effect);
        Assert.Equal(source.Address + 16, visual.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(-400, -400, 2000), visual.A);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect227Programme.DrawNode(Frame(), source, node with { Data = node.Data.SetItem(42, 1) }, caller, resources, new Harness().Inputs()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    public void Captured227_puff_rows_preserve_native_padding_and_use_actual_physical_row_resource(int slot)
    {
        const uint first = 0x20DFAB8 + 0x61C;
        uint address = first + (uint)(slot * 16);
        NativeBattleEffect227Frame Fixture(bool capture)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x112D2E0u)).Write(8, BitConverter.GetBytes((short)2000)).Write(10, BitConverter.GetBytes((short)512)).Write(15, [1]);
            return WriteBytes(Frame() with { CapturePresentation = capture }, address, row, 16);
        }
        Harness nativeCalls = new();
        Harness capturedCalls = new();
        NativeBattleEffect227Inputs Inputs(Harness harness) => harness.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108) };
        NativeBattleEffect227Frame native = NativeBattleEffect227Records.Draw(Fixture(false), Resources(), Inputs(nativeCalls));
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleEffect227Frame captured = NativeBattleEffect227Records.Draw(Fixture(true), Resources(), Inputs(capturedCalls));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeCalls.Operations), NativeBattleTestSnapshots.Json(capturedCalls.Operations));
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(capturedCalls.Operations)).Batch;
        NativeBattlePilotVisualGeometry visual = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal((ushort)227, visual.Identity.Effect);
        Assert.Equal(slot, visual.Identity.Slot);
        Assert.Equal(0x112D2E0L, visual.Identity.ResourceGeneration);
    }

    [Fact]
    public void Captured227_record_reuse_changes_presentation_birth_only_and_keeps_all_four_CRT_draws()
    {
        const uint first = 0x20DFAB8 + 0x61C;
        NativeBattleEffect227Frame native = Frame();
        NativeBattleEffect227Frame captured = Frame() with { CapturePresentation = true };
        long old = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            native = NativeBattleEffect227Records.Birth(native, 0x1131660, 1, Resources(), new Harness().Inputs());
            captured = NativeBattleEffect227Records.Birth(captured, 0x1131660, 1, Resources(), new Harness().Inputs());
            Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
            NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(227, captured.State.Storage, first, 0);
            Assert.True(identity.Generation > old);
            old = identity.Generation;
            native = WriteBytes(native with { State = native.State.WriteDword(0x2508704, first) }, first + 15, NativeBattleEffectArena.Clear().Write(0, [0]), 1);
            captured = WriteBytes(captured with { State = captured.State.WriteDword(0x2508704, first) }, first + 15, NativeBattleEffectArena.Clear().Write(0, [0]), 1);
        }
    }

    [Theory]
    [InlineData((ushort)4)]
    [InlineData((ushort)15)]
    public void Captured227_lower_ACT_keeps_actual_source_W_terminal_motion_and_all_native_registers(ushort age)
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        NativeBattleVector[] vertices = [new(-40, -40, 2000), new(40, -40, 2000), new(-40, 40, 2000)];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vertices[index].Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 0x30808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(58), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x808080);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x808080);
        NativeBattleEffect227Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x112DAA8, new(0, 0x112DAA8, bytes.ToImmutableArray())) };
        NativeBattleEffect227Frame Fixture(bool capture)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508718, 0x20DFAB8, 24, 16);
            (pool, int slot) = pool.Append(0x694360);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[12]).WriteWord(12, age));
            NativeBattleEffect227Frame frame = Frame();
            frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
            frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, frame.Effect.Pools), CapturePresentation = capture };
            return frame with { PresentationIdentity = capture ? NativeBattlePilotVisualLifetimes.Identity(227, Read(frame, 0x20DFAB8)) : null };
        }
        Harness nativeCalls = new();
        Harness capturedCalls = new();
        NativeBattleEffect227Inputs Inputs(Harness harness) => harness.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108) };
        NativeBattleEffect227Step native = NativeBattleEffect227Lower.Advance(Fixture(false), 0x20DFAB8, resources, Inputs(nativeCalls));
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleEffect227Step captured = NativeBattleEffect227Lower.Advance(Fixture(true), 0x20DFAB8, resources, Inputs(capturedCalls));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeCalls.Operations), NativeBattleTestSnapshots.Json(capturedCalls.Operations));
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(capturedCalls.Operations)).Batch;
        NativeBattlePilotVisualGeometry visual = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal((ushort)227, visual.Identity.Effect);
        Assert.Equal(0x112DAA8L, visual.Identity.ResourceGeneration);
        Assert.True(visual.Continuous);
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 1);
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = 28 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vertices[index].Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x30808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(78), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x808080);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(88), 0x808080);
        return new(0x112DCF8, bytes.ToImmutableArray());
    }
}
