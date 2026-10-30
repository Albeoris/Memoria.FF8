using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37RenderingTests
{
    [Theory]
    [InlineData(0x85BD50u, 0x15D9624u, 10)]
    [InlineData(0x85E930u, 0x15D92F4u, 5)]
    [InlineData(0x85F060u, 0x15D9504u, 5)]
    public void Sprite_adapter_keeps_the_actual_directory_pointer_frame_count_and_original_task_identity(uint callback, uint resource, ushort phase)
    {
        var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleModulePoolTask task = Task(callback).WriteDword(0x4C, resource).WriteWord(0x50, phase).WriteWord(0x54, 0).WriteWord(0x22, 0xABCD);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        Assert.NotEmpty(NativeBattleDrinkMagic37Rendering.SpritePreparation(task, camera));
        Assert.Equal(callback, task.Callback);
        NativeBattleDrawStock15SpriteComposition composition = NativeBattleDrinkMagic37Rendering.ComposeSprite(task, fixture.Resources, camera, projection, 0);
        Assert.Equal(resource, composition.Draw.Resource!.Address);
        Assert.Equal(phase, composition.Draw.Phase);
        Assert.Equal(unchecked((short)0xABCD), composition.Draw.Anchor!.W);
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        NativeBattleDrawStock15SpriteTemporaryResult emitted = NativeBattleDrinkMagic37Rendering.Emit180(staging, temporary, task, fixture.Resources, camera, projection, 0);
        Assert.Equal(resource, BinaryPrimitives.ReadUInt32LittleEndian(emitted.Staging.Read(temporary, 4)));
        ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(composition.Draw.Resource.Bytes.AsSpan().Slice(10 + phase * 2));
        Assert.Equal(resource + offset + 4, BinaryPrimitives.ReadUInt32LittleEndian(emitted.Staging.Read(temporary + 44, 4)));
        Assert.Equal(callback == 0x85BD50 ? (ushort)16 : (ushort)6, BinaryPrimitives.ReadUInt16LittleEndian(emitted.Staging.Read(temporary + 38, 2)));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, emitted.Staging.Read(temporary + 118, 2));
        Assert.NotEmpty(emitted.Batch.Primitives);
        Assert.Equal(callback, task.Callback);
        Assert.Equal(resource, task.ReadDword(0x4C));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_Draw_Stock_wrapper_is_identical_to_the_extracted_prepared_path(bool sparkle)
    {
        var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleDrawStock15ParticleResources resources = new(fixture.Resources.Trajectory, fixture.Resources.Sparkle);
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(Task(sparkle ? 0x85F060u : 0x85E930u), taskData => taskData with { Callback = sparkle ? 0x8B6440u : 0x8B5C70u });
        task = task.WriteDword(0x4C, sparkle ? 0x162B730u : 0x162B520u).WriteWord(0x50, 2).WriteWord(0x40, 125).WriteWord(0x42, 413).WriteWord(0x44, 200);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrawStock15SpriteComposition composition = NativeBattleDrawStock15Rendering.ComposeSprite(task, resources, camera, projection, 17);
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        NativeBattleDrawStock15SpriteTemporaryResult wrapper = NativeBattleDrawStock15SpriteRendering.Emit180(staging, temporary, task, resources, camera, projection, 17);
        NativeBattleDrawStock15SpriteTemporaryResult prepared = NativeBattleDrawStock15SpriteRendering.EmitPrepared180(staging, temporary, task, composition, projection, 17);
        Assert.Equal(NativeBattleTestSnapshots.Json(wrapper), NativeBattleTestSnapshots.Json(prepared));
    }

    [Fact]
    public void Ribbon_reads_the_identical_payload_without_rewriting_the_physical_callback_or_suffix()
    {
        NativeBattleModulePoolTask task = Task(0x85E930).WriteWord(0x128, 1).WriteWord(0x12A, 3).WriteWord(0x12C, 0xBEEF).WriteWord(0x12E, 0x9876);
        for (int lane = 0; lane < 9; lane++)
        {
            task = task.WriteWord(0x60 + lane * 2, (ushort)(lane is 0 or 4 or 8 ? 4096 : 0));
        }
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrawStock15RibbonStep actual = NativeBattleDrinkMagic37Rendering.Ribbon(task, camera, projection);
        NativeBattleDrawStock15RibbonStep expected = NativeBattleDrawStock15Rendering.Ribbon8B5F30(NativeBattleTaskFixture.EditTask(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task)), taskData => taskData with { Callback = 0x8B5C70 }), camera, projection);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(4, actual.Batch.Attempts.Length);
        Assert.Equal(0x85E930u, task.Callback);
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x12C));
        Assert.Equal((ushort)0x9876, task.ReadWord(0x12E));
    }

    [Fact]
    public void Rendering_errors_preserve_callback_and_family_guards()
    {
        NativeBattleModulePoolTask task = Task(0x85E930).WriteWord(0x128, ushort.MaxValue).WriteWord(0x12A, 1);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        Assert.Throws<NotSupportedException>(() => NativeBattleDrinkMagic37Rendering.Ribbon(task, camera, projection));
        Assert.Equal(0x85E930u, task.Callback);
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawStock15Rendering.Ribbon8B5F30(task, camera, projection));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrawStock15SpriteRendering.Preparation(task, camera));
        Assert.Equal(0x85E930u, task.Callback);
        NativeBattleModulePoolTask stock = Task(0x8B5C70);
        Assert.Throws<InvalidDataException>(() => NativeBattleDrinkMagic37Rendering.Ribbon(stock, camera, projection));
        Assert.Throws<InvalidDataException>(() => NativeBattleDrinkMagic37Rendering.SpritePreparation(stock, camera));
        Assert.Equal(0x8B5C70u, stock.Callback);
    }

    [Theory]
    [InlineData(0x85BD50u, 0x15D9624u, 10)]
    [InlineData(0x85E930u, 0x15D92F4u, 5)]
    [InlineData(0x85F060u, 0x15D9504u, 5)]
    public void Sprite_capture_preserves_detached_temporary_operations_packets_and_actual_authored_corners(uint callback, uint resource, ushort phase)
    {
        var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleModulePoolTask task = Task(callback).WriteDword(0x4C, resource).WriteWord(0x50, phase).WriteWord(0x54, 0);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging nativeStaging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        NativeBattleFileStaging visualStaging = NativeBattleTestSnapshots.Independent(nativeStaging);
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(37, task) with { ResourceGeneration = resource };
        NativeBattleDrawStock15SpriteTemporaryResult native = NativeBattleDrinkMagic37Rendering.Emit180(nativeStaging, temporary, task, fixture.Resources, camera, projection, 0);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleDrawStock15SpriteTemporaryResult visual = NativeBattleDrinkMagic37Rendering.Emit180(visualStaging, temporary, task, fixture.Resources, camera, projection, 0, pilotIdentity: identity);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(visual));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        NativeBattleAllocatedPrimitive[] polygons = visual.Batch.Primitives.Where(item => ((item.Primitive.Words[1] >> 24) & 0xE0) == 0x20).ToArray();
        Assert.NotEmpty(polygons);
        Assert.All(polygons, item =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(item.Primitive.PilotGeometry);
            Assert.Equal((ushort)37, geometry.Identity.Effect);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal((long)resource, geometry.Identity.ResourceGeneration);
            Assert.Equal((byte)4, geometry.CornerCount);
            Assert.NotNull(geometry.Sprite);
        });
        Assert.Equal(polygons.Length, polygons.Select(item => item.Primitive.PilotGeometry!.Identity).Distinct().Count());
    }

    [Fact]
    public void Ribbon_capture_uses_actual_history_points_depths_and_birth_without_changing_native_operations_or_masks()
    {
        NativeBattleModulePoolTask task = Task(0x85E930).WriteWord(0x128, 1).WriteWord(0x12A, 3);
        for (int lane = 0; lane < 9; lane++)
        {
            task = task.WriteWord(0x60 + lane * 2, (ushort)(lane is 0 or 4 or 8 ? 4096 : 0));
        }
        for (int row = 1; row <= 3; row++)
        {
            task = task.WriteWord(0x90 + row * 8, (ushort)(row * 30)).WriteWord(0x94 + row * 8, (ushort)(1000 + row * 10));
        }
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(37, task);
        NativeBattleDrawStock15RibbonStep native = NativeBattleDrinkMagic37Rendering.Ribbon(task, camera, projection);
        NativeBattleDrawStock15RibbonStep visual = NativeBattleDrinkMagic37Rendering.Ribbon(task, camera, projection, pilotIdentity: identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(visual));
        Assert.Equal(4, visual.Batch.Attempts.Length);
        Assert.All(visual.Batch.Attempts, attempt =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal((ushort)37, geometry.Identity.Effect);
            Assert.Equal((byte)3, geometry.CornerCount);
            Assert.NotNull(geometry.SourceDepths);
            Assert.Equal(4, geometry.DepthQuantum);
            Assert.Equal(4, geometry.DepthDivisor);
            Assert.Equal(0.25f, geometry.DepthScale);
        });
        Assert.Equal(4, visual.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
    }

    [Fact]
    public void Orb_capture_preserves_detached_mesh_context_registers_far_controls_and_native_partial_writes()
    {
        var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend allocated = hierarchy.Append8DC540(4, NativeBattleDrinkMagic37Profile.OrbCallback, 96, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = allocated.Hierarchy.Read(allocated.Address).WriteDword(0x4C, NativeBattleDrinkMagic37Profile.OrbAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x5E, 120);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrinkMagic37OrbPreparation nativePrepared = NativeBattleDrinkMagic37OrbRendering.Prepare(task, camera, NativeBattleEffectArena.Clear());
        NativeBattleDrinkMagic37OrbPreparation visualPrepared = NativeBattleTestSnapshots.Independent(nativePrepared);
        byte[] meshBytes = fixture.Resources.OrbInitial.ToArray();
        short[] coordinates = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(meshBytes.AsSpan(8 + vertex * 8), coordinates[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(meshBytes.AsSpan(10 + vertex * 8), coordinates[vertex * 2 + 1]);
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(meshBytes);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(meshBytes.AsSpan(cursor));
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(meshBytes.AsSpan(cursor), group == 3 ? 0x2C808080u : 0x3C808080u);
                for (int corner = 0; corner < 4; corner++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(meshBytes.AsSpan(cursor + 4 + corner * 2), (ushort)(corner * 2));
                }
                cursor += strides[group];
            }
        }
        NativeBattleEffectArena nativeMesh = NativeBattleEffectArena.Clear().Write(0, meshBytes);
        NativeBattleEffectArena visualMesh = NativeBattleTestSnapshots.Independent(nativeMesh);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(37, task) with { ResourceGeneration = NativeBattleDrinkMagic37Profile.OrbAddress };
        NativeBattleActorWaveMeshStep native = NativeBattleDrinkMagic37OrbRendering.EmitBatch(nativeMesh, nativePrepared, projection, registers, new(1, 2, 3, 4, 5), 0, 0, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleActorWaveMeshStep visual = NativeBattleDrinkMagic37OrbRendering.EmitBatch(visualMesh, visualPrepared, projection, registers, new(1, 2, 3, 4, 5), 0, 0, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270, identity);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(visual));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        NativeBattleGeometryPrimitive[] polygons = visual.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.NotEmpty(polygons);
        Assert.All(polygons, primitive =>
        {
            Assert.Equal((ushort)37, primitive.PilotGeometry!.Identity.Effect);
            Assert.Equal(identity.Generation, primitive.PilotGeometry.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, primitive.PilotGeometry.Identity.ResourceGeneration);
        });
    }

    private static NativeBattleModulePoolTask Task(uint callback)
    {
        var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(3, callback, 304, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        return appended.Hierarchy.Read(appended.Address);
    }

    private static NativeBattleActionMatrix Camera()
    {
        return new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
    }
}
