using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpecializedModelMaterialTests
{
    private const string Group = "SubmitNativeModelGroup50F190";
    private const string Pandemona = "DrawNativePandemona291Actor";
    private const string Diablos = "DrawNativeDiablosRecipient657CB0";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleVector UnitScale = new(4096, 4096, 4096);
    private static readonly NativeBattlePoseSource Pose = CreatePose();
    private static readonly NativeBattlePrimitiveTables Tables = CreateTables();

    [Theory]
    [InlineData(Group, "hosted", true)]
    [InlineData(Pandemona, "hosted", true)]
    [InlineData(Pandemona, "hosted", false)]
    [InlineData(Diablos, "hosted", true)]
    [InlineData(Diablos, "hosted", false)]
    [InlineData(Group, "legacy", true)]
    [InlineData(Pandemona, "legacy", false)]
    [InlineData(Diablos, "legacy", false)]
    [InlineData(Group, "native", true)]
    [InlineData(Pandemona, "native", false)]
    [InlineData(Diablos, "native", false)]
    public void Body_and_linked_packets_preserve_the_selected_material_and_shadows_stay_global(string method, string ownership, bool shared)
    {
        var (session, graphics, row) = Fixture(ownership, shared);

        graphics = Draw(method, session, graphics, row);

        ImmutableArray<NativeBattleLogicalCommand> commands = Commands(graphics);
        NativeBattleLogicalCommand[] body = commands.Where(command => command.Opcode is 0x24 or 0x26).ToArray();
        Assert.Equal(new[] { 0x80u, 0x81u }, body.Select(command => command.Words[4] >> 16).Order().ToArray());
        Assert.All(body, command => Assert.Equal(ownership == "hosted" ? "selected" : null, command.ModelTextureOwner?.RequireActorId()));
        NativeBattleLogicalCommand[] shadows = commands.Where(command => command.Opcode is 0x32 or 0xE1).ToArray();
        Assert.Equal(method == Group ? 0 : 10, shadows.Length);
        Assert.All(shadows, command => Assert.Null(command.ModelTextureOwner));
    }

    [Theory]
    [InlineData(Group)]
    [InlineData(Pandemona)]
    [InlineData(Diablos)]
    public void Missing_declared_actor_bank_fails_before_body_or_shadow_publication(string method)
    {
        var (session, graphics, row) = Fixture("missing", shared: true);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;
        string stagingBefore = NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(session.NativeFileStaging!));

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Draw(method, session, graphics, row));

        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Contains("no bound presentation texture bank", error.InnerException!.Message);
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Equal(0, memory.PrimitiveProcess!.ByteCursor);
        string stagingAfter = NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(session.NativeFileStaging!));
        Assert.True(string.Equals(stagingBefore, stagingAfter, StringComparison.Ordinal), "Missing material rejection changed detached staging bytes or knownness.");
        Assert.Empty(Commands(graphics));
    }

    [Theory]
    [InlineData(Pandemona)]
    [InlineData(Diablos)]
    public void Effect_frame_supplies_the_material_even_when_session_memory_has_no_map(string method)
    {
        var (session, graphics, row) = Fixture("hosted", shared: false);
        NativeBattleEffectMemory frameMemory = session.NativeEffectMemory!;
        Set(session, nameof(BattleSession.NativeEffectMemory), frameMemory with { Textures = NativeBattleEffectTextureState.FromColdProcess() });

        graphics = Draw(method, session, graphics, row, frameMemory);

        NativeBattleLogicalCommand[] body = Commands(graphics).Where(command => command.Opcode == 0x24).ToArray();
        Assert.Equal(2, body.Length);
        Assert.All(body, command => Assert.Equal("selected", command.ModelTextureOwner?.RequireActorId()));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics, int Row) Fixture(string ownership, bool shared)
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "selected", Name = "Selected", IsPlayer = true }, new() { Id = "other", Name = "Other" }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleShadowSource shadow = new(Identity, UnitScale, Enumerable.Repeat(default(NativeBattleVector), 10).ToImmutableArray());
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [], []) { ShadowSource = shadow, SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 };
        NativeBattleGraphicsResource resource = new([0, 0])
        {
            PoseSource = Pose,
            LinkedPoseSource = Pose,
            Geometry = shared ? [Mesh(0x80), Mesh(0x80)] : [Mesh(0x80)],
            LinkedGeometry = [Mesh(0x81)],
            LinkedGeometryUsesBodySkeleton = shared
        };
        typeof(BattleSession).GetField("definition", PrivateInstance)!.SetValue(session, definition with
        {
            NativeGraphics = new(rules, [new("selected", 0, resource)], []) { PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(), PrimitiveTables = Tables }
        });

        int row = ownership == "native" ? 0 : 7;
        BattleModelOwner selected = session.ReadPresentationModelOwner("selected");
        typeof(BattleSession).GetField("presentationSourceOwner", PrivateInstance)!.SetValue(session, selected);
        typeof(BattleSession).GetField("presentationSourceResource", PrivateInstance)!.SetValue(session, resource);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        if (ownership != "legacy")
        {
            string owner = ownership == "missing" ? "other" : "selected";
            textures = textures.WithPresentationTexture(owner, new(NativeBattleEffectTextureState.FromColdProcess(), NativeBattlePcTextureWindowCache.Reset4647A0(null)));
        }
        Set(session, nameof(BattleSession.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), Textures = textures });
        Set(session, nameof(BattleSession.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 3 });
        Set(session, nameof(BattleSession.NativeFileStaging), NativeBattleFileStaging.FromColdProcess());
        Set(session, nameof(BattleSession.NativeCamera), NativeBattleCameraState.FromColdProcess() with { Projection = new(Identity, 1000, 160, 108) });

        NativeBattleGraphicsState graphics = row == 0
            ? OriginalGraphics([(0, selected, Model(row))])
            : OriginalGraphics([(3, session.ReadPresentationModelOwner("other"), new(0))]);
        if (row != 0)
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Add(selected, Model(row)) };
        }
        graphics = graphics with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        return (session, graphics, row);
    }

    private static NativeBattleGraphicsModel Model(int index)
    {
        NativeBattleGraphicsModel model = new(2)
        {
            ModelId4 = 0,
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

    private static NativeBattleGraphicsState Draw(string method, BattleSession session, NativeBattleGraphicsState graphics, int row, NativeBattleEffectMemory? frameMemory = null)
    {
        NativeBattleEffectMemory memory = frameMemory ?? session.NativeEffectMemory!;
        if (method == Group)
        {
            graphics = (NativeBattleGraphicsState)Invoke(Group, session, graphics, session.ReadPresentationModelOwner("selected"), (byte)0, (byte)15, 0x808080u, null);
            return (NativeBattleGraphicsState)Invoke(Group, session, graphics, session.ReadPresentationModelOwner("selected"), (byte)1, (byte)15, 0x808080u, null);
        }
        if (method == Pandemona)
        {
            NativeBattlePandemona291Frame frame = new(memory, Effect(291), new(1));
            var (_, drawn) = ((NativeBattlePandemona291Frame, NativeBattleGraphicsState))Invoke(Pandemona, session, frame, session.ReadPresentationModelOwner("selected"), default(NativeBattleVector), default(NativeBattleVector), UnitScale, graphics);
            return drawn;
        }
        NativeBattleDiablos325Frame diablos = new(memory, Effect(325));
        var (_, result) = ((NativeBattleDiablos325Frame, NativeBattleGraphicsState))Invoke(Diablos, session, diablos, session.ReadPresentationModelOwner("selected"), 4096, graphics);
        return result;
    }

    private static NativeBattleOriginalEffectState Effect(ushort id) => new(id, id - 1, new(id, 1, 1), new(OriginalOwner(0), 0, BattleModelTargets.From([OriginalOwner(0)]), new BattleActionHeaderReference(0), 0, 0), 1);

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

    private static NativeBattlePrimitiveTables CreateTables()
    {
        ImmutableArray<byte> lengths = new byte[256].ToImmutableArray().SetItem(0x24, 7).SetItem(0x26, 7).SetItem(0x32, 6);
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        return new(lengths, new byte[256].ToImmutableArray(), empty, empty, empty);
    }

    private static ImmutableArray<NativeBattleLogicalCommand> Commands(NativeBattleGraphicsState graphics) => graphics.PrimitiveFrame!.Pending.Traverse(Tables.WordLengths);

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);

    private static object Invoke(string method, BattleSession session, params object?[] args) => typeof(BattleSession).GetMethod(method, PrivateInstance)!.Invoke(session, args)!;
}
