using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorMaterialBindingTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleGraphicsRoster Roster = new([new("first", true), null, null, null, null, null, null, new("second", false)]);
    private static readonly NativeBattlePoseSource Pose = CreatePose();

    [Fact]
    public void Body_and_both_link_paths_publish_the_actor_material_and_leave_stage_and_shadow_global()
    {
        var (session, graphics) = Fixture();
        graphics = Draw(session, graphics, 0, shared: true);
        graphics = Draw(session, graphics, 7, shared: false);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { ResourceStageId = 22 };
        stage = stage with { Groups = stage.Groups.SetItem(0, new(3, 0, 0x808080, 0, 0, 0, 1, Pose, [Identity])) };
        graphics = InvokeFrame(session, "SubmitNativeStagePackets", graphics, stage);

        ImmutableArray<NativeBattleLogicalCommand> commands = Commands(graphics);
        foreach (string actor in new[] { "first", "second" })
        {
            var draws = commands.Where(command => command.PresentationActorId == actor && command.Opcode == 0x24).ToArray();
            Assert.Equal(2, draws.Length);
            Assert.All(draws, command => Assert.Equal(actor, command.ModelTextureOwner!.RequireActorId()));
            Assert.Equal(new[] { 0x80u, 0x81u }, draws.Select(command => command.Words[4] >> 16).Order().ToArray());
        }
        var shadowModes = commands.Where(command => command.Opcode == 0xE1).ToArray();
        Assert.Equal(4, shadowModes.Length);
        Assert.All(shadowModes, command => Assert.Null(command.ModelTextureOwner));
        var shadowDraws = commands.Where(command => command.Opcode == 0x32).ToArray();
        Assert.Equal(16, shadowDraws.Length);
        Assert.All(shadowDraws, command => Assert.Null(command.ModelTextureOwner));
        NativeBattleLogicalCommand stageDraw = Assert.Single(commands.Where(command => command.TextureSourceOwner == "battle/a0stg022.x"));
        Assert.Null(stageDraw.PresentationActorId);
        Assert.Null(stageDraw.ModelTextureOwner);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("native")]
    [InlineData("staged")]
    [InlineData("magic")]
    [InlineData("target animation")]
    public void Material_selection_tracks_the_actual_draw_resource_path(string variant)
    {
        var (session, graphics) = Fixture(declareMap: variant != "legacy", declareRoster: variant != "native");
        NativeBattleGraphicsModel model = graphics.Models[0];
        model = variant switch
        {
            "staged" => model with { ActiveStagedResourceRow100 = 0x100u },
            "magic" => model with { ResourceMagicId = 1 },
            "target animation" => model with { ResourceMagicId = 1, ResourceRenzokukenTargetBank = 0 },
            _ => model
        };
        graphics = graphics with { Models = graphics.Models.SetItem(0, model) };

        graphics = Draw(session, graphics, 0, shared: false);

        var draws = Commands(graphics).Where(command => command.Opcode == 0x24).ToArray();
        Assert.Equal(2, draws.Length);
        Assert.All(draws, command =>
        {
            Assert.Equal("first", command.PresentationActorId);
            Assert.Equal(variant is "legacy" or "native" ? null : "first", command.ModelTextureOwner?.RequireActorId());
        });
    }

    [Fact]
    public void Declared_map_requires_the_selected_actors_bank_before_emission()
    {
        var (session, graphics) = Fixture(includeSecond: false);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Draw(session, graphics, 7, shared: false));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(0, session.NativeEffectMemory!.PrimitiveProcess!.ByteCursor);
        Assert.Empty(Commands(graphics));
    }

    [Fact]
    public void Generic_and_module_batches_forward_an_explicit_material_owner()
    {
        var (session, graphics) = Fixture();
        NativeBattleModelTextureOwner owner = new("second");
        NativeBattleGeometryPrimitive primitive = Assert.Single(NativeBattleRawGeometryPackets.Emit5099D0([Mesh(0x80)], [Identity], new(Identity, 1000, 160, 108), 0x808080, 1, null, 17, 4, null)) with { ModelTextureOwner = owner };
        graphics = InvokeFrame(session, "AppendNativePrimitivePackets", graphics, ImmutableArray.Create(primitive), null, "replacement");
        NativeBattleModulePacketBatch batch = new([new(primitive, 0, 1)], primitive.Words.Length * 4);
        graphics = InvokeFrame(session, "AppendNativeModulePacketBatch", graphics, batch, 1, null);

        ImmutableArray<NativeBattleLogicalCommand> commands = Commands(graphics);
        Assert.Equal(2, commands.Length);
        Assert.All(commands, command => Assert.Equal(owner, command.ModelTextureOwner));
        Assert.Single(commands.Where(command => command.TextureSourceOwner == "replacement"));
        Assert.Single(commands.Where(command => command.ModuleDraw == 1));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(bool declareMap = true, bool declareRoster = true, bool includeSecond = true)
    {
        BattleDefinition definition = new()
        {
            NativeStageSource = new(22, [null, null, null, null], [], new("stage:TIM", [])) { Geometry = [Mesh(0x80) with { StageGroup = 0 }] },
            Combatants = [new() { Id = "first", Name = "First", IsPlayer = true }, new() { Id = "second", Name = "Second" }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleShadowSource shadow = new(Identity, new(4096, 4096, 4096), Enumerable.Repeat(default(NativeBattleVector), 10).ToImmutableArray());
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [], []) { ShadowSource = shadow };
        typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, definition with
        {
            NativeGraphics = new(rules, [], []) { PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(), PrimitiveTables = Tables() }
        });
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        if (declareMap)
        {
            textures = textures.WithPresentationTexture("first", new(NativeBattleEffectTextureState.FromColdProcess(), NativeBattlePcTextureWindowCache.Reset4647A0(null)));
            if (includeSecond)
            {
                textures = textures.WithPresentationTexture("second", new(NativeBattleEffectTextureState.FromColdProcess(), NativeBattlePcTextureWindowCache.Reset4647A0(null)));
            }
        }
        Set(session, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), Textures = textures });
        Set(session, nameof(BattleSession.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 3 });
        Set(session, nameof(BattleSession.NativeFileStaging), NativeBattleFileStaging.FromColdProcess());
        Set(session, nameof(BattleSession.NativeCamera), NativeBattleCameraState.FromColdProcess() with { Projection = new(Identity, 1000, 160, 108) });
        var rows = declareRoster ? Roster.Rows : Roster.Rows.RemoveAt(7);
        ImmutableArray<NativeBattleGraphicsModel> models = rows.Select((actor, index) => actor is null ? new NativeBattleGraphicsModel(index, 0) : Model(index)).ToImmutableArray();
        NativeBattleGraphicsState graphics = new(models, rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = declareRoster ? Roster : null,
            PrimitiveFrame = NativeBattlePrimitiveFrame.Clear()
        };
        return (session, graphics);
    }

    private static NativeBattleGraphicsModel Model(int index)
    {
        NativeBattleGraphicsModel model = new(index, 2)
        {
            Flags2 = 0,
            Color40 = 0x808080,
            Color44 = 0x32808080,
            GeometryWorldMatrix = Identity,
            GeometryBounds52 = new(new(-30, 0, 970), new(30, 0, 1030)),
            Transform = new(default, default, 0, 0, 4096),
            BodyTextureSlots = new(new byte?[16].ToImmutableArray()),
            LinkedTextureSlots = new(new byte?[16].ToImmutableArray()),
            LinkedTransform = new(default, default),
            SequenceState = new() { MeshMask124 = 3, DecodedPose = Pose.Sample(0, 1, 0), LinkedPose = new(0, 1, 1, 0) }
        };
        return NativeBattleBodyBoneMatrices.Publish508C90(model, Pose);
    }

    private static NativeBattleGraphicsState Draw(BattleSession session, NativeBattleGraphicsState graphics, int row, bool shared)
    {
        NativeBattleGraphicsModel model = graphics.Models[row];
        NativeBattleGraphicsResource resource = new([0, 0])
        {
            PoseSource = Pose,
            LinkedPoseSource = Pose,
            Geometry = shared ? [Mesh(0x80), Mesh(0x80)] : [Mesh(0x80)],
            LinkedGeometry = [Mesh(0x81)],
            LinkedGeometryUsesBodySkeleton = shared
        };
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleGraphicsResource selected = NativeBattleVictoryResources.Resolve(model, resource, address => address switch
            {
                var value when value == staged => new(null, 0x200, Pose.Skeleton.Length),
                var value when value == staged + 8 => new(null, 0x300, Pose.Animations.Length),
                var value when value == staged + 12 => new(null, 0x400, resource.SequenceSection.Length),
                _ => throw new InvalidOperationException("The staged pose queried an unexpected resource field.")
            }, (pointer, length) => pointer.StagingAddress switch
            {
                0x200 => Pose.Skeleton.AsSpan(0, length).ToArray(),
                0x300 => Pose.Animations.AsSpan(0, length).ToArray(),
                0x400 => resource.SequenceSection.AsSpan(0, length).ToArray(),
                _ => throw new InvalidOperationException("The staged pose opened an unexpected resource.")
            });
            Assert.Equal(resource.Geometry, selected.Geometry);
            Assert.Equal(resource.LinkedGeometry, selected.LinkedGeometry);
            Assert.NotSame(resource.PoseSource, selected.PoseSource);
            resource = selected;
        }
        return InvokeFrame(session, "SubmitNativeModelPackets502D40", graphics, model, resource);
    }

    private static NativeBattlePoseSource CreatePose()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        return new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0]);
    }

    private static NativeBattleRawMesh Mesh(ushort page)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(14), page);
        return new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
    }

    private static NativeBattlePrimitiveTables Tables()
    {
        ImmutableArray<byte> lengths = new byte[256].ToImmutableArray().SetItem(0x24, 7).SetItem(0x32, 6);
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        return new(lengths, new byte[256].ToImmutableArray(), empty, empty, empty);
    }

    private static ImmutableArray<NativeBattleLogicalCommand> Commands(NativeBattleGraphicsState graphics) => graphics.PrimitiveFrame!.Pending.Traverse(Tables().WordLengths);

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);

    private static NativeBattleGraphicsState InvokeFrame(BattleSession session, string method, params object?[] args)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args)!;
    }
}
