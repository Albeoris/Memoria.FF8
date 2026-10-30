using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleCinematicModelTests
{
    [Fact]
    public void Draw_projects_into_unknown_current_workspace_and_retains_actual_face_context_and_source_cursor()
    {
        NativeBattleWaterExecutionFrame frame = RenderFixture();
        uint work = Base + Model + 0x80;
        frame = frame with { Arena = frame.Arena.MarkUnknown(Model + 0x80, 32).MarkUnknown(0x12028, 4) };
        NativeBattleCinematicModelDraw result = NativeBattleCinematicModel.Render(frame, Base + Model, new(null, 216, 160, 108), new(0, 0, 1f / 4096, 6755401588539392d), pointer =>
        {
            Assert.Equal(0x1000u, pointer);
            return 51;
        });
        Assert.Equal(24, result.ProjectedBytesWritten);
        Assert.Equal(24, result.Frame.Arena.Read(Model + 0x80, 24).Length);
        Assert.Throws<NotSupportedException>(() => result.Frame.Arena.Read(Model + 0x98, 8));
        Assert.Equal(work, Dword(result.Frame, Model + 0x44));
        Assert.Equal(Base + 0x1202C, Dword(result.Frame, Model + 0x40));
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, result.Frame.Arena.Read(Model + 0x48, 8));
        Assert.Equal((uint?)(Base + 0x1203C), result.SourceCursor);
        Assert.Single(result.Packets.Attempts);
    }

    [Fact]
    public void Masked_object_does_not_read_its_directory_entry_or_output_context()
    {
        NativeBattleWaterExecutionFrame frame = RenderFixture();
        frame = frame with { Arena = frame.Arena.Write(Model + 0x60, BitConverter.GetBytes(0u)).MarkUnknown(0x12004, 56).MarkUnknown(Model + 0x40, 0x10) };
        NativeBattleCinematicModelDraw result = NativeBattleCinematicModel.Render(frame, Base + Model, new(null, 216, 160, 108), new(0, 0, 1f / 4096, 6755401588539392d), _ => throw new InvalidOperationException());
        Assert.Same(frame, result.Frame);
        Assert.Equal(0, result.ProjectedBytesWritten);
        Assert.Empty(result.Packets.Attempts);
        Assert.Null(result.SourceCursor);
    }

    [Fact]
    public void Unreached_colored_groups_and_work_aliases_fail_explicitly()
    {
        NativeBattleWaterExecutionFrame frame = RenderFixture();
        NativeBattleWaterExecutionFrame colored = frame with { Arena = frame.Arena.Write(0x12024, new byte[] { 1, 0 }) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicModel.Render(colored, Base + Model, new(null, 216, 160, 108), new(0, 0, 1f / 4096, 6755401588539392d), _ => 51));
        frame = frame with { Arena = frame.Arena.Write(Model + 0x44, BitConverter.GetBytes(Base + 0x12008)) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicModel.Render(frame, Base + Model, new(null, 216, 160, 108), new(0, 0, 1f / 4096, 6755401588539392d), _ => 51));
    }

    [Theory]
    [InlineData((ushort)208)]
    [InlineData((ushort)211)]
    public void Captured208_211_model_keeps_native_pose_scratch_and_actual_model_geometry_resource(ushort effect)
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 216, 160, 108);
        NativeBattleCinematicModelDraw native = NativeBattleCinematicModel.Render(RenderFixture(), Base + Model, projection, new(0, 0, 1f / 4096, 6755401588539392d), _ => 51);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleCinematicModelDraw captured = NativeBattleCinematicModel.Render(RenderFixture(), Base + Model, projection, new(0, 0, 1f / 4096, 6755401588539392d), _ => 51, new(effect, 1, 2, 3, 4) { ResourceGeneration = 123 });
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        NativeBattlePilotVisualGeometry visual = Assert.Single(captured.Packets.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(effect, visual.Identity.Effect);
        Assert.Equal(unchecked(123L * 397 ^ (Base + 0x12000u)), visual.Identity.ResourceGeneration);
    }

    private static NativeBattleWaterExecutionFrame RenderFixture()
    {
        NativeBattleWaterExecutionFrame frame = Prepared();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, Base + Model);
        frame = frame.WriteTemporaryDword(0x14C, 0x1000);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x4C, BitConverter.GetBytes(0xBADu)) };
        byte[] geometry = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(geometry, 1);
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(12), 3);
        short[] positions = [-10, -10, 1000, 10, -10, 1000, 0, 10, 1000];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(14 + index * 2), positions[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(44), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(48), 2);
        frame = frame with { Arena = frame.Arena.Write(0x12000, geometry) };
        return NativeBattleCinematicModel.PrepareDraw(frame, Services);
    }
}
