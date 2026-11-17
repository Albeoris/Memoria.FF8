using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignModelContextTests
{
    private sealed record Material : INativeBattleForeignMaterial;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Whole_host_body_context_preserves_actual_material_packets_without_native_mesh_mask_or_CPU_scratch(bool compactContext)
    {
        BattleDefinition definition = new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleGraphicsDefinition drawing = new(new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray()) { SummonColors = colors }, [], []) { PrimitiveCapabilities = new(true) { HardwareEnabled = true } };
        NativeBattleActionMatrix identity = NativeBattleForeignModelGeometry.Identity;
        NativeBattleActionMatrix world = identity with { Z = 8192 };
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleForeignModelFace face = new(new(-256, -256, 0), new(256, -256, 0), new(-256, 256, 0), surface);
        NativeBattleForeignModelGeometry geometry = new(world, new(default, default, 0, new(default, default)), [face]);
        NativeBattleModelTransform transform = new(default, default, 0, 0, 4096);
        BattleModelOwner enemy = session.ReadPresentationModelOwner("enemy");
        NativeBattleForeignActor actor = new(enemy, transform, new(0, 0, 0, 0), (_, _) => default) { Geometry = geometry };
        NativeBattleGraphicsModel model = new(0x22) { Transform = transform, Color40 = 0x808080 };
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, session.ReadPresentationModelOwner("party"), new(0)), (3, enemy, model)]) with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        NativeBattleHostedTestFixture.BindProcess(session, graphics, drawing);
        NativeBattleHostedTestFixture.SetField(session.Presentation!, "presentationForeignActors", new Dictionary<BattleModelOwner, NativeBattleForeignActor> { [enemy] = actor });
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() });
        NativeCrtRandomState random = new(123);
        NativeBattleHostedTestFixture.SetField(session.Presentation!, "nativeCrtRandom", random);
        NativeBattleFileStaging? staging = session.Presentation?.NativeFileStaging;
        NativeBattleCameraProjection projection = new(identity, 128, 160, 108);
        NativeBattlePilotVisualIdentity visual = new(274, 1, 0, 1, 0);
        string method = compactContext ? "DrawNativeDuel215Body69D100" : "DrawNativeEffectModelContext92";
        object?[] arguments = compactContext ? [graphics, enemy, identity, projection, visual] : [graphics, enemy, world, new NativeBattleVector(19, 23, 29), projection, 33, (byte)4, (byte)4, visual];
        NativeBattleGraphicsState result = (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, arguments)!;
        byte depthShift = compactContext ? (byte)6 : (byte)4;
        NativeBattleRawModelBodyDraw expected = NativeBattleForeignModelDrawing.DrawBody(geometry, world, projection, 0x808080, colors, visual, depthShift, new(0, 0, 320, 216), 17, null);
        NativeBattleGeometryPrimitive expectedPrimitive = Assert.Single(expected.Packets.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        NativeBattleLogicalPacket actual = Assert.Single(result.PrimitiveFrame!.Pending.Packets);
        Assert.Equal(expectedPrimitive.Words[0] >> 24, actual.Words[0] >> 24);
        Assert.Equal(expectedPrimitive.Words.Skip(1).ToArray(), actual.Words.Skip(1).ToArray());
        Assert.Same(surface, actual.PilotGeometry!.ForeignSurface);
        Assert.Equal(expectedPrimitive.PilotGeometry!.Identity, actual.PilotGeometry.Identity);
        Assert.Null(model.SequenceState);
        Assert.Null(session.Presentation?.ModelCpuResources);
        Assert.Same(staging, session.Presentation?.NativeFileStaging);
        Assert.Same(random, typeof(BattleSession).GetField("nativeCrtRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session));
        Assert.True(session.Presentation!.Memory.PrimitiveProcess!.ByteCursor is > 0);
    }
}
