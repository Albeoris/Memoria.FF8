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
        NativeBattleModulePoolTask task = Task(sparkle ? 0x85F060u : 0x85E930u) with { Callback = sparkle ? 0x8B6440u : 0x8B5C70u };
        task = task.WriteDword(0x4C, sparkle ? 0x162B730u : 0x162B520u).WriteWord(0x50, 2).WriteWord(0x40, 125).WriteWord(0x42, 413).WriteWord(0x44, 200);
        NativeBattleActionMatrix camera = Camera();
        NativeBattleCameraProjection projection = new(camera, 216, 160, 108);
        NativeBattleDrawStock15SpriteComposition composition = NativeBattleDrawStock15Rendering.ComposeSprite(task, resources, camera, projection, 17);
        uint temporary = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(temporary, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        NativeBattleDrawStock15SpriteTemporaryResult wrapper = NativeBattleDrawStock15SpriteRendering.Emit180(staging, temporary, task, resources, camera, projection, 17);
        NativeBattleDrawStock15SpriteTemporaryResult prepared = NativeBattleDrawStock15SpriteRendering.EmitPrepared180(staging, temporary, task, composition, projection, 17);
        Assert.Equal(JsonSerializer.Serialize(wrapper), JsonSerializer.Serialize(prepared));
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
        NativeBattleDrawStock15RibbonStep expected = NativeBattleDrawStock15Rendering.Ribbon8B5F30(task with { Callback = 0x8B5C70 }, camera, projection);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(4, actual.Batch.Attempts.Length);
        Assert.Equal(0x85E930u, task.Callback);
        Assert.Equal((ushort)0xBEEF, task.ReadWord(0x12C));
        Assert.Equal((ushort)0x9876, task.ReadWord(0x12E));
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
