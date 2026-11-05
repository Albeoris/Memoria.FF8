using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonModuleMotionPacketTests
{
    private static readonly NativeBattlePilotVisualIdentity Identity = new(0, NativeBattleModuleState.DescriptorAddress, 4, 7, 0);
    private static readonly NativeBattleActionMatrix Camera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4000 };
    private static readonly NativeBattleCameraProjection Projection = new(Camera, 100, 160, 108);

    [Fact]
    public void Defensive_mesh_keeps_world_coordinates_and_binds_the_selected_resource()
    {
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Defensive, 0) { Anchor = new(100, 300, 1000, 0), Phase = 2, Scale = 4096, PilotIdentity = Identity };
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleModulePacketBatch plain = NativeBattleDefensivePackets.Emit570650(draw with { PilotIdentity = null }, new(0, mesh.Address, mesh.Bytes), Projection);
        NativeBattleModulePacketBatch batch = NativeBattleDefensivePackets.Emit570650(draw, new(0, mesh.Address, mesh.Bytes), Projection);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Primitives).Primitive;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(mesh.Address, geometry.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(-300, -100, 1000), geometry.A);
        Assert.Equal(Assert.Single(plain.Primitives).Primitive.Words, primitive.Words);
    }

    [Fact]
    public void Impact_mesh_and_sprite_keep_distinct_resource_bound_world_geometry()
    {
        NativeBattleModuleImpactPacketStep plain = Impact(0, null);
        NativeBattleModuleImpactPacketStep first = Impact(0, Identity);
        Assert.Equal(2, first.Batches.Length);
        NativeBattleGeometryPrimitive mesh = Assert.Single(first.Batches[0].Attempts).Primitive!;
        NativeBattleGeometryPrimitive sprite = Assert.Single(first.Batches[1].Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        NativeBattlePilotVisualGeometry meshGeometry = Assert.IsType<NativeBattlePilotVisualGeometry>(mesh.PilotGeometry);
        NativeBattlePilotVisualGeometry spriteGeometry = Assert.IsType<NativeBattlePilotVisualGeometry>(sprite.PilotGeometry);
        Assert.Equal(Mesh().Address, meshGeometry.Identity.ResourceGeneration);
        Assert.Equal(Sprite().Address, spriteGeometry.Identity.ResourceGeneration);
        Assert.NotEqual(meshGeometry.Identity, spriteGeometry.Identity);
        Assert.Equal(new Vector3(0, 200, 1000), meshGeometry.A);
        Assert.Equal(new Vector3(100, 0, 900), spriteGeometry.A);
        Assert.Equal(new Vector3(106, 0, 900), spriteGeometry.B);
        Assert.Equal(new Vector3(100, 0, 906), spriteGeometry.C);
        Assert.Equal(new Vector3(106, 0, 906), spriteGeometry.D);
        Assert.Equal(plain.Batches[0].Attempts[0].Primitive!.Words, mesh.Words);
        Assert.Equal(Assert.Single(plain.Batches[1].Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.Words, sprite.Words);
        NativeBattlePilotVisualGeometry later = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(Assert.Single(Impact(1, Identity).Batches).Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal(spriteGeometry.Identity, later.Identity);
        Assert.Equal(new Vector3(150, 0, 1100), later.A);
    }

    private static NativeBattleModuleImpactPacketStep Impact(ushort phase, NativeBattlePilotVisualIdentity? identity)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Impact570860, 0) { Anchor = new(100, 300, 1000, 0), Phase = phase, Scale = 1024, ImpactScaleZ = 2048, PilotIdentity = identity };
        return NativeBattleModuleImpactPackets.Draw570860(memory, staging, draw, NativeBattlePackedMatrix32.FromMatrix(Camera, 0), Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, Mesh(), [Sprite()], 0);
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] vertices = [-400, -400, 0, 0, 400, -400, 0, 0, -400, 400, 0, 0, 400, 400, 0, 0];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), vertices[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x20808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        return new(5, 0x10000000, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), ushort.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1);
        bytes[20] = 1;
        bytes[21] = 128;
        bytes[22] = 1;
        bytes[23] = 0x2E;
        return new(31, 0x11000000, 0, 0, bytes.ToImmutableArray());
    }
}
