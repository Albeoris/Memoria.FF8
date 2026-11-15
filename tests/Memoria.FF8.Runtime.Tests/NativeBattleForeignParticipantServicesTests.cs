using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignParticipantServicesTests
{
    [Fact]
    public void Host_bone_observer_publishes_actual_point_and_geometry_registers_without_native_resource_rows()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "source", Name = "Source", IsPlayer = true }, new() { Id = "target", Name = "Target" }] }, BattleAtbMode.Normal);
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattlePosedActor pose = new(NativeBattleForeignPlacement.World(transform, 0, 0, 0), [new(10, 20, 30), new(10, 20, 80)], (selector, fraction) =>
        {
            Assert.Equal(0, selector);
            Assert.Equal(1024, fraction);
            return new(10, 20, 30);
        });
        BattleModelOwner owner = new BattleForeignEnemyModel(new("host"));
        NativeBattleForeignActor actor = new(owner, transform, default!, (_, _) => throw new InvalidOperationException()) { Pose = pose };
        typeof(BattleSession).GetField("presentationForeignActors", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new Dictionary<BattleModelOwner, NativeBattleForeignActor> { [owner] = actor });
        BattleActorModels models = new([new(owner, new NativeBattleGraphicsModel(2) { Transform = transform, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0 })]);
        NativeBattleGraphicsState graphics = new(models, NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        MethodInfo observe = typeof(BattleSession).GetMethod("ObserveNativeGraphicsBoneAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = ((NativeBattleEffectMemory Memory, NativeBattleVector Point))observe.Invoke(battle, [NativeBattleEffectMemory.FromColdProcess(), graphics, owner, (byte)0, (short)1024])!;
        Assert.Equal(new NativeBattleVector(110, 220, 330), result.Point);
        Assert.Equal(new int?[] { 110, 220, 330 }, result.Memory.ScriptedColorRegisters!.Mac.ToArray());
        Assert.Null(battle.NativeModelCpuResources);
        Assert.Null(battle.NativeFileStaging);
    }

    [Fact]
    public void Actor_draw_keeps_real_host_material_and_applies_the_selected_native_scaled_basis()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleActionMatrix view = NativeBattleForeignModelGeometry.Identity with { Z = 8192 };
        NativeBattleCameraProjection projection = new(view, 128, 160, 108);
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleForeignModelGeometry.Identity, 4096, 2048, 4096);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, scaled);
        ImmutableArray<NativeBattleGeometryPrimitive> drawn = NativeBattleForeignModelDrawing.Draw(geometry, basis, projection, 0x808080, new(0, 0x808080, 1f / 4096, 4503601774854144d), new(0, 1, 0, 0, 0));
        NativeBattleGeometryPrimitive primitive = Assert.Single(drawn);
        Assert.Same(geometry.Faces[0].Surface, primitive.PilotGeometry!.ForeignSurface);
        Assert.Equal(new Vector3(-256, -128, 0), primitive.PilotGeometry.A);
        Assert.Equal(new Vector3(256, -128, 0), primitive.PilotGeometry.B);
        Assert.Equal(new Vector3(-256, 128, 0), primitive.PilotGeometry.C);
        Assert.Equal(new NativeBattleVector(-256, -256, 0), geometry.Faces[0].A);
        NativeBattleRawModelBodyDraw body = NativeBattleForeignModelDrawing.DrawBody(geometry, basis, projection, 0x808080, new(0, 0x808080, 1f / 4096, 4503601774854144d), new(0, 1, 0, 0, 0));
        NativeBattleGfGeometryAttempt allocation = Assert.Single(body.Packets.Attempts);
        Assert.Equal(24, body.ProjectedBytesWritten);
        Assert.Equal(allocation.Data.Length, body.Packets.AllocationBytes);
        Assert.Same(geometry.Faces[0].Surface, allocation.Primitive!.PilotGeometry!.ForeignSurface);
        Assert.Equal(new Vector3(-256, -128, 0), allocation.Primitive.PilotGeometry.A);
        Assert.Equal(new Vector3(256, -128, 0), allocation.Primitive.PilotGeometry.B);
        Assert.Equal(new Vector3(-256, 128, 0), allocation.Primitive.PilotGeometry.C);
    }

    [Fact]
    public void Vertex_bounds_consume_current_host_triangles_and_the_same_selected_transform()
    {
        NativeBattleForeignModelGeometry geometry = Geometry();
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleForeignModelGeometry.Identity, 4096, 2048, 4096) with { Y = 50 };
        var bounds = NativeBattleForeignModelDrawing.VerticalBounds(geometry, point => NativeBattleGfMatrices.Transform460840(basis, point).Y);
        Assert.Equal((short)178, bounds.Maximum);
        Assert.Equal((short)-78, bounds.Minimum);
        Assert.Throws<NotSupportedException>(() => NativeBattleForeignModelDrawing.VerticalBounds(geometry with { Faces = [] }, point => point.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cinematic_anchor_consumes_real_native_or_host_pose_without_inventing_resource_bytes(bool foreign)
    {
        NativeBattleModelDatSource source = NativeBattleModelResourceCapacityTests.Source(false, 8);
        ImmutableArray<byte> skeleton = source.Bytes.Slice(source.Section(0), source.Section(1) - source.Section(0));
        NativeBattleActionMatrix identity = NativeBattleForeignModelGeometry.Identity;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x1A77238, new byte[0x180].ToImmutableArray(), new bool[0x180].ToImmutableArray()), new(7));
        NativeBattleCinematicBonePose pose = new(frame, skeleton, [identity with { X = 10, Y = 20, Z = 30 }], identity);
        if (foreign)
        {
            pose = new(frame, [], [], identity)
            {
                Pose = new(identity, [new(10, 20, 30), new(10, 20, 80)], (selector, fraction) =>
                {
                    Assert.Equal(0, selector);
                    Assert.Equal(4096, fraction);
                    return new(10, 20, 30);
                })
            };
        }
        NativeBattleWaterActorAnchorResult result = pose.Evaluate(NativeBattleActorAnchorRegisterFixture.Services(), frame, 0, 4096, identity with { X = 100, Y = 200, Z = 300 });
        Assert.Equal(1, pose.BoneCount);
        Assert.Equal(new NativeBattleFullMacVertex(110, 220, 330, 0), result.Anchor);
        Assert.Equal(330u, result.Frame.ReadTemporaryDword(0xF8));
        if (foreign)
        {
            Assert.Empty(pose.Skeleton);
            Assert.Empty(pose.Bones);
        }
        else
        {
            Assert.Equal(skeleton, pose.Skeleton);
        }
    }

    private static NativeBattleForeignModelGeometry Geometry()
    {
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        return new(NativeBattleForeignModelGeometry.Identity, new(default, default, 0, new(new(-256, -256, 0), new(256, 256, 0))), [new(new(-256, -256, 0), new(256, -256, 0), new(-256, 256, 0), surface)]);
    }

    private sealed record Material : INativeBattleForeignMaterial;
}
