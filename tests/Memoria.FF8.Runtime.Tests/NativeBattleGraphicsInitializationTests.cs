using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsInitializationTests
{
    [Fact]
    public void Reached_descriptor_initialization_sets_scalar_projection_and_offsets_at_its_own_boundary()
    {
        NativeBattleCameraState cold = NativeBattleCameraState.FromColdProcess();
        Assert.Equal((short)0, cold.FocalLength);
        NativeBattleCameraState prior = cold.Initialize500400();
        Assert.Equal((ushort)216, prior.Projection!.FocalLength);
        Assert.Equal((short)0, prior.FocalLength);
        NativeBattleCameraState actual = prior.Initialize500520();
        Assert.Equal((short)512, actual.FocalLength);
        Assert.Equal((ushort)512, actual.Projection!.FocalLength);
        Assert.Equal((short)160, actual.Projection.OffsetX);
        Assert.Equal((short)108, actual.Projection.OffsetY);
        Assert.Equal((short)0, actual.ScreenOffsetScratchX);
        Assert.Equal((short)0, actual.ScreenOffsetScratchY);
        Assert.Same(prior.CurrentPose, actual.CurrentPose);
        Assert.Same(prior.CapturedPose, actual.CapturedPose);
        Assert.Same(prior.Projection.View, actual.Projection.View);
        Assert.Equal(prior.Mode, actual.Mode);
    }

    [Fact]
    public void Empty_selected_track_captures_actual_scalar_and_roll_instead_of_stale_pose_fields()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Initialize500520() with { Roll = 73 };
        NativeBattleCameraBank bank = new(ImmutableArray.Create<byte>(1, 0, 4, 0, 1, 0, 255, 255));
        NativeBattleCameraState completed = camera.Start(bank, 0).Tick(4).Capture503300();
        Assert.Equal((short)512, completed.CapturedPose!.FocalLength);
        Assert.Equal((short)73, completed.CapturedPose.Roll);
        completed = completed with { BlendProgress = 4096 };
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(completed))!;
        NativeBattleCameraState next = restored.Tick(4).PublishProjection(4);
        Assert.Equal((short)512, next.CurrentPose!.FocalLength);
        Assert.Equal((ushort)512, next.Projection!.FocalLength);
        Assert.Equal((short)73, next.Roll);
        Assert.Equal((ushort)0, next.Mask);
    }

    [Fact]
    public void Geometry_reset_clears_actual_register_owners_and_preserves_nonregister_workspaces()
    {
        NativeBattleEffectMemory prior = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { AnchorScratch8 = Enumerable.Repeat<byte?>(0xA5, 8).ToImmutableArray() },
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { ModuleViewBackup = Enumerable.Repeat((byte)0xCC, 32).ToImmutableArray(), Dqa = 7, Dqb = 999, DepthScaleBits1877DA8 = 0x3F000000 },
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() with { PublicationState03A = 2, ModelRendererRegisters = new(1, 2, 3, 0x1234, 0x5678) }
        };
        NativeBattleEffectMemory actual = NativeBattleGraphicsInitialization.Initialize500520(prior);
        Assert.All(actual.GeometryRegisters!.Rotation20, value => Assert.Equal((byte?)0, value));
        Assert.All(actual.GeometryRegisters.Translation, value => Assert.Equal((int?)0, value));
        Assert.All(actual.GeometryRegisters.AnchorScratch8, value => Assert.Equal((byte?)0xA5, value));
        Assert.Equal(0u, actual.ScriptedColorRegisters!.Flags);
        Assert.All(actual.ScriptedColorRegisters.Ir, value => Assert.Equal((int?)0, value));
        Assert.All(actual.ScriptedLighting!.LightMatrix!.Known, value => Assert.True(value));
        Assert.All(actual.ScriptedLighting.LightMatrix.Bytes, value => Assert.Equal((byte)0, value));
        Assert.Equal(new NativeBattleScriptedBackColor(0, 0, 0), actual.ScriptedLighting.BackColor);
        Assert.Equal((short)0x155, actual.ScriptedGraphics!.Zsf3);
        Assert.Equal((short)0x100, actual.ScriptedGraphics.Zsf4);
        Assert.Equal((short)0, actual.ScriptedGraphics.Dqa);
        Assert.Equal(0, actual.ScriptedGraphics.Dqb);
        Assert.Same(prior.ScriptedGraphics!.Depths, actual.ScriptedGraphics.Depths);
        Assert.Equal(prior.ScriptedGraphics.ModuleViewBackup, actual.ScriptedGraphics.ModuleViewBackup);
        Assert.Equal(prior.ScriptedGraphics.DepthScaleBits1877DA8, actual.ScriptedGraphics.DepthScaleBits1877DA8);
        Assert.Equal((byte)0x11, actual.PrimitiveProcess!.PublicationState03A);
        Assert.Equal(new NativeBattleModelRendererRegisters(0, 0, 0, 0x1234, 0x5678), actual.PrimitiveProcess.ModelRendererRegisters);
        Assert.Equal(prior.PrimitiveProcess!.PacketBank, actual.PrimitiveProcess.PacketBank);
        Assert.Equal(prior.PrimitiveProcess.ByteCursor, actual.PrimitiveProcess.ByteCursor);
    }

    [Fact]
    public void Missing_nonregister_owner_is_rejected_instead_of_recreated()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsInitialization.Initialize500520(NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsInitialization.Initialize500520(NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = null }));
    }
}
