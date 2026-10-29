using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup11AliasPresentationTests
{
    [Fact]
    public void Aqua55_sprite_draw_uses_actual_enemy_task_identity_without_changing_native_packet_bytes()
    {
        NativeBattleModulePoolTask task = NativeBattleAquaRenderingTests.SpriteTask(2000);
        NativeBattleAquaResources resources = NativeBattleAquaRenderingTests.Resources();
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleGfGeometryBatch plain = NativeBattleAquaRendering.ComposeSprite(task, resources, view, projection, 0).Batch;
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleAquaFrame frame = Frame55();
        NativeBattleAquaFrame drawn = NativeBattleAquaRendering.DrawSprite(frame, task, resources, Inputs(view, projection, operations));
        NativeBattleGfGeometryBatch captured = Assert.Single(operations.OfType<NativeBattleAquaPackets>()).Batch;
        AssertNativeParity(plain, captured);
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal((ushort)55, geometry.Identity.Effect);
        Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(55, task).Generation, geometry.Identity.Generation);
        Assert.Equal(0x15A38DC, geometry.Identity.ResourceGeneration);
        Assert.Equal(4, geometry.CornerCount);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(drawn));
        Assert.Equal(new NativeBattleAquaTemporaryBegin(216), operations.First());
        Assert.Equal(new NativeBattleAquaTemporaryEnd(216), operations.Last());
    }

    [Fact]
    public void Aqua55_wave_captures_the_current_mutable_mesh_and_preserves_window_packets()
    {
        byte[] mesh = new byte[10600];
        I32(mesh, 0, 2504);
        I32(mesh, 4, 312);
        for (int index = 0; index < 4; index++)
        {
            W(mesh, 8 + index * 8, (short)((index & 1) == 0 ? -64 : 64));
            W(mesh, 10 + index * 8, (short)(index < 2 ? -64 : 64));
            W(mesh, 12 + index * 8, 2000);
        }
        I32(mesh, 2516, 1);
        I32(mesh, 2520, 0x2E808080);
        for (int corner = 0; corner < 4; corner++)
        {
            W(mesh, 2524 + corner * 2, (short)(corner * 2));
        }
        NativeBattleAquaFrame frame = Frame55();
        frame = frame with { Memory = frame.Memory with { Aqua55 = frame.State with { WaveMesh = mesh.ToImmutableArray() } } };
        NativeBattleModulePoolTask task = NativeBattleAquaRenderingTests.Task().WriteDword(0x4C, frame.Profile.MeshAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x50, 4096).WriteWord(0x5E, 8);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleAquaRendering.DrawWave(frame, task, NativeBattleAquaRenderingTests.Resources(), Inputs(view, projection, operations));
        NativeBattleGfGeometryBatch batch = Assert.Single(operations.OfType<NativeBattleAquaPackets>()).Batch;
        NativeBattlePilotVisualGeometry geometry = Assert.Single(batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!.PilotGeometry!;
        Assert.Equal((ushort)55, geometry.Identity.Effect);
        Assert.Equal(new Vector3(-64, -64, 2000), geometry.A);
        Assert.Equal(new Vector3(64, 64, 2000), geometry.D);
        Assert.Equal(2, batch.Attempts.Count(attempt => attempt.Primitive is { } primitive && primitive.PilotGeometry is null));
        NativeBattleAquaTemporaryPatch flags = Assert.Single(operations.OfType<NativeBattleAquaTemporaryPatch>(), patch => patch.Offset == 0x14);
        Assert.Equal(0xF0u, BinaryPrimitives.ReadUInt32LittleEndian(flags.Bytes.AsSpan()));
    }

    [Fact]
    public void Absorb56_flash_stays_discrete_and_releases_on_original_script_terminal()
    {
        var fixture = NativeBattleDrain39NodeTests.Fixture(56);
        NativeBattleDrainFamilyProfile profile = NativeBattleDrainFamilyProfile.ForEffect(56);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend added = hierarchy.Append8DC540(2, profile.FlashCallback, 60, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        hierarchy = added.Hierarchy.Write(added.Address, added.Hierarchy.Read(added.Address).WriteDword(0x30, profile.FirstFlashAddress));
        NativeBattleDrain39Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleDrain39Resources resources = NativeBattleDrain39NodeTests.Resources(56) with { FirstFlash = [0x007F7F7F, 0x003F3F3F, 0xFF000000] };
        List<NativeBattleModulePacketBatch> batches = [];
        string memoryBefore = NativeBattleTestSnapshots.Json(frame.Memory);
        for (int tick = 0; tick < 3; tick++)
        {
            NativeBattleDrain39LeafStep step = NativeBattleDrain39Simple.Advance(frame, added.Address, resources, (current, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleDrain39PacketOperation>(operation).Batch);
                return current;
            });
            frame = step.Frame;
            Assert.Equal(tick == 2 ? 2 : 0, step.Result);
        }
        Assert.Equal(2, batches.Count);
        for (int tick = 0; tick < 2; tick++)
        {
            NativeBattleModulePacketBatch batch = batches[tick];
            Assert.Equal(36, batch.AllocationBytes);
            Assert.Equal(tick == 0 ? 0x2A7F7F7Fu : 0x2A3F3F3Fu, batch.Primitives[0].Primitive.Words[1]);
            NativeBattlePilotVisualGeometry geometry = batch.Primitives[0].Primitive.PilotGeometry!;
            Assert.Equal((ushort)56, geometry.Identity.Effect);
            Assert.Equal(profile.Pools[2].Descriptor, geometry.Identity.Bank);
            Assert.False(geometry.Continuous);
            Assert.True(geometry.ScreenSpace);
            Assert.Equal(8, geometry.ScreenScale);
            Assert.Null(batch.Primitives[1].Primitive.PilotGeometry);
            Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[1].Primitive.Words.ToArray());
        }
        Assert.Equal(batches[0].Primitives[0].Primitive.PilotGeometry!.Identity, batches[1].Primitives[0].Primitive.PilotGeometry!.Identity);
        Assert.Equal(memoryBefore, NativeBattleTestSnapshots.Json(frame.Memory));
    }

    [Fact]
    public void Remedy67_uses_its_own_context_node_generation_and_original_Esuna_uv_wrap()
    {
        var fixture = NativeBattleRestorationNodesTests.Fixture(67);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.Remedy;
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Task).WriteWord(0x34 + 0x2A, 255);
        NativeBattleEsunaMeshPreparation prepared = NativeBattleRestorationMeshPackets.Prepare88E090(task, 1, 0, 0, null);
        Assert.Equal((ushort)1, prepared.Task.ReadWord(0x34 + 0x2A));
        Assert.Equal((ushort)1, prepared.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 128), prepared.Context.WindowAfter);
        using NativeBattleRestorationNodeMemory memory = new(fixture.Frame, fixture.Task);
        int row = fixture.Frame.State.Geometry!.Value;
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(67, hierarchy.Read(fixture.Task));
        memory.AllocatedPilotGeometry(row);
        NativeBattlePilotVisualIdentity first = memory.PilotGeometryIdentity(owner, row, 0);
        NativeBattlePilotVisualIdentity second = memory.PilotGeometryIdentity(owner, row, 1);
        memory.AllocatedPilotGeometry(row);
        NativeBattlePilotVisualIdentity reborn = memory.PilotGeometryIdentity(owner, row, 0);
        Assert.Equal((ushort)67, first.Effect);
        Assert.Equal(profile.Descriptors[2], first.Bank);
        Assert.Equal(first.Generation, reborn.Generation);
        Assert.NotEqual(first.ResourceGeneration, reborn.ResourceGeneration);
        Assert.NotEqual(first.Primitive, second.Primitive);
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Frame), NativeBattleTestSnapshots.Json(memory.Flush()));
    }

    private static NativeBattleAquaFrame Frame55() => new(NativeBattleEffectMemory.FromColdProcess() with { Aqua55 = NativeBattleAquaState.FromColdProcess(55), SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) }, new(55, 54, new(55, 0x808820, 0x808800), null, 0), NativeCrtRandomState.FromColdProcess());
    private static NativeBattleAquaInputs Inputs(NativeBattleActionMatrix view, NativeBattleCameraProjection projection, List<NativeBattleAquaOperation> operations) => new(NativeBattleActionPacketsState.Fresh(), view, projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static void AssertNativeParity(NativeBattleGfGeometryBatch plain, NativeBattleGfGeometryBatch captured)
    {
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        Assert.Equal(plain.Attempts.Length, captured.Attempts.Length);
        for (int index = 0; index < plain.Attempts.Length; index++)
        {
            Assert.Equal(plain.Attempts[index].Data.ToArray(), captured.Attempts[index].Data.ToArray());
            Assert.Equal(plain.Attempts[index].WriteMask.ToArray(), captured.Attempts[index].WriteMask.ToArray());
            Assert.Equal(plain.Attempts[index].ByteOffsetRelative, captured.Attempts[index].ByteOffsetRelative);
            Assert.Equal(plain.Attempts[index].Primitive?.Bucket, captured.Attempts[index].Primitive?.Bucket);
        }
    }

    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
}
