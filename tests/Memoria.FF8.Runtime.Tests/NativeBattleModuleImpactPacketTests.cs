using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleImpactPacketTests
{
    [Fact]
    public void First_phase_draws_mesh_then_sprite_and_later_phase_uses_entry_scales_for_offsets()
    {
        NativeBattleModuleImpactPacketStep first = Draw(0);
        Assert.Equal(2, first.Batches.Length);
        Assert.All(first.Batches, batch => Assert.Empty(batch.Attempts));
        Assert.Equal(100, first.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(4900, first.Memory.GeometryRegisters.Translation[2]);
        Assert.Equal(0u, first.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        NativeBattleModuleImpactPacketStep later = Draw(1);
        Assert.Single(later.Batches);
        Assert.Equal(150, later.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(5100, later.Memory.GeometryRegisters.Translation[2]);
        Assert.Equal(0, later.Memory.GeometryRegisters.Translation[1]);
        Assert.Null(later.Memory.ModuleSpriteBillboard21DFED0);
        Assert.Null(later.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(later.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(later.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Throws<NotSupportedException>(() => later.Staging.Read(NativeBattleTemporaryMemory.Address + 118, 2));
    }

    [Fact]
    public void Non_sprite_primary_header_selects_fallback_without_reading_its_frame_directory()
    {
        NativeBattleModuleResource primary = Sprite(31) with { Bytes = ImmutableArray.Create<byte>(0, 0, 0, 1) };
        NativeBattleModuleImpactPacketStep step = Draw(1, resources: [primary, Sprite(0)]);
        Assert.Equal(Sprite(0).Address, BitConverter.ToUInt32(step.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal((ushort)8, BitConverter.ToUInt16(step.Staging.Read(NativeBattleTemporaryMemory.Address + 36, 2)));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(step.Staging.Read(NativeBattleTemporaryMemory.Address + 4, 2)));
        Assert.Throws<NotSupportedException>(() => Draw(1, resources: [primary]));
    }

    [Fact]
    public void JSON_restores_released_temporary_and_geometry_for_the_next_callback()
    {
        NativeBattleModuleImpactPacketStep first = Draw(0);
        NativeBattleModuleImpactPacketStep restored = JsonSerializer.Deserialize<NativeBattleModuleImpactPacketStep>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(1, first.Memory, first.Staging)), JsonSerializer.Serialize(Draw(1, restored.Memory, restored.Staging)));
        Assert.Throws<NotSupportedException>(() => Draw(1, staging: NativeBattleFileStaging.Unknown()));
    }

    private static NativeBattleModuleImpactPacketStep Draw(ushort phase, NativeBattleEffectMemory? memory = null, NativeBattleFileStaging? staging = null, ImmutableArray<NativeBattleModuleResource> resources = default)
    {
        memory ??= NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        staging ??= NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Impact570860, 0) { Phase = phase, Anchor = new(100, 300, 1000, 7), Rotation = 0, Orientation = 0, Scale = 1024, ImpactScaleZ = 2048 };
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4000 };
        byte[] mesh = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 8);
        return NativeBattleModuleImpactPackets.Draw570860(memory, staging, draw, NativeBattlePackedMatrix32.FromMatrix(camera, 0), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, phase == 0 ? new(5, 0x10000000, mesh.ToImmutableArray()) : null, resources.IsDefault ? [Sprite(31), Sprite(0)] : resources, 0);
    }

    private static NativeBattleModuleResource Sprite(byte id)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 4);
        for (int phase = 0; phase < 4; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), 20);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), ushort.MaxValue);
        return new(id, (uint)(0x11000000 + id * 256), 0, 0, bytes.ToImmutableArray());
    }
}
