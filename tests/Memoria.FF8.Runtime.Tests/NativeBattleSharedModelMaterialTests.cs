using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSharedModelMaterialTests
{
    private const uint StagedRow = NativeBattleFileStaging.Address + 0x200;
    private const int Coefficients = 0x3F08;
    private static readonly NativeBattleActionMatrix Identity = NativeBattleCuragaMatrices.Identity8DD770();
    private static readonly NativeBattleCameraProjection Projection = new(Identity, 1000, 160, 108);
    private static readonly ImmutableArray<byte> Lengths = new byte[256].ToImmutableArray().SetItem(0x24, 7).SetItem(0x32, 6);
    private static readonly BattleModelRoster Roster = new([new("first", true), null, null, null, null, null, null, new("second", true)]);
    private static readonly NativeBattlePoseSource Pose = CreatePose();

    [Fact]
    public void Shared_draw_uses_current_body_and_linked_sources_for_two_actors()
    {
        var (session, graphics) = Fixture();
        graphics = DrawShared(session, graphics, 0);
        graphics = DrawShared(session, graphics, 7);
        NativeBattleGraphicsModel changed = graphics.Models[0] with { ActiveResourceRow100 = 2 };
        graphics = DrawShared(session, graphics with { Models = graphics.Models.SetItem(0, changed) }, 0);

        ImmutableArray<NativeBattleLogicalCommand> commands = graphics.PrimitiveFrame!.Pending.Traverse(Lengths);
        Assert.Equal(new[] { 0x80u, 0x81u, 0x81u, 0x82u }, Pages(commands, "first"));
        Assert.Equal(new[] { 0x82u, 0x83u }, Pages(commands, "second"));
        NativeBattleLogicalCommand[] shadows = commands.Where(command => command.Opcode == 0xE1).ToArray();
        Assert.Equal(6, shadows.Length);
        Assert.All(shadows, command => Assert.Null(command.ModelTextureOwner));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Staged_body_uses_typed_cpu_owner_or_retains_the_legacy_global_path(bool typed, bool hosted)
    {
        var (session, graphics) = Fixture(hosted);
        graphics = Stage(session, graphics, typed);
        graphics = DrawShared(session, graphics, 0);

        NativeBattleLogicalCommand body = Assert.Single(graphics.PrimitiveFrame!.Pending.Traverse(Lengths).Where(command => command.Opcode == 0x24 && command.Words[4] >> 16 == 0x82));
        Assert.Equal(hosted ? "first" : null, body.ModelTextureOwner?.RequireActorId());
    }

    [Fact]
    public void Hosted_staged_numeric_body_is_rejected_before_any_packet_publication()
    {
        var (session, graphics) = Fixture();
        graphics = Stage(session, graphics, typed: false);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!;

        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => DrawShared(session, graphics, 0));

        Assert.Contains("numeric pointer", Assert.IsType<NotSupportedException>(error.InnerException).Message);
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Empty(graphics.PrimitiveFrame!.Pending.Traverse(Lengths));
    }

    [Fact]
    public void Primary_history_uses_passed_texture_owner_and_retains_material_at_the_depth_receipt()
    {
        var (session, graphics) = Fixture(hosted: false);
        NativeBattleEffectMemory memory = session.NativeEffectMemory! with { Textures = Textures() };
        NativeBattleOriginalEffectState effect = Effect(324);
        graphics = graphics with { OriginalEffect = effect };

        var drawn = Assert.IsType<ValueTuple<NativeBattleEffectMemory, NativeBattleFileStaging, NativeBattleGraphicsState>>(Call(session, "DrawNativePrimaryHistory76", memory, effect, session.NativeFileStaging, graphics, 0, new NativeBattlePilotVisualIdentity(324, 0, 0, 0, 0)));

        NativeBattleLogicalCommand body = Assert.Single(drawn.Item3.PrimitiveFrame!.Pending.Traverse(Lengths));
        Assert.Equal("first", body.ModelTextureOwner!.RequireActorId());
        Assert.Equal(body.Packet, Assert.Single(drawn.Item3.OriginalEffect!.PresentationPackets));
        Assert.True(body.PilotGeometry!.UseSourceDepths);
        Assert.Null(session.NativeEffectMemory!.Textures!.PresentationTextures);
    }

    [Fact]
    public void Deformed_body_uses_passed_texture_owner_for_body_and_weapon_only()
    {
        var (session, graphics) = Fixture(hosted: false);
        NativeBattleEffectMemory memory = session.NativeEffectMemory! with { Textures = Textures() };
        object drawn = Call(session, "DrawNativeDeformedBody", memory, Effect(317), new NativeCrtRandomState(1), session.NativeFileStaging, graphics, (byte)7, (short)0, NativeBattleEffectNodeMemory.ArenaAddress + Coefficients, 8, null);
        NativeBattleGraphicsState result = (NativeBattleGraphicsState)drawn.GetType().GetProperty("Graphics")!.GetValue(drawn)!;
        ImmutableArray<NativeBattleLogicalCommand> commands = result.PrimitiveFrame!.Pending.Traverse(Lengths);

        Assert.Equal(new[] { 0x82u, 0x83u }, Pages(commands, "second"));
        Assert.All(commands.Where(command => command.Opcode != 0x24), command => Assert.Null(command.ModelTextureOwner));
        Assert.Null(session.NativeEffectMemory!.Textures!.PresentationTextures);
    }

    [Fact]
    public void Rinoa_private_ordering_forwards_the_actual_body_material()
    {
        var (session, graphics) = Fixture();
        NativeBattleOriginalEffectState effect = Effect(89);
        NativeBattleRinoa89Frame frame = new(session.NativeEffectMemory!, effect, session.NativeFileStaging!, new(1));
        NativeBattleRinoa89CaptureStep begun = NativeBattleRinoa89Capture.Begin6FBEE0(frame, graphics.PrimitiveFrame!.Pending);
        Set(session, nameof(BattleSession.NativeEffectMemory), begun.Frame.Memory);
        SetField(session, "nativeCrtRandom", new NativeCrtRandomState(1));
        graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = graphics.PrimitiveFrame with { Pending = begun.Ordering } };

        graphics = DrawShared(session, graphics, 7, NativeBattleRinoa89Capture.TableAddress);

        NativeBattleEffectArena arena = session.NativeEffectMemory!.Arena!;
        uint root = NativeBattleRinoa89Capture.TableAddress + (17 + (1000 >> 4)) * 4u;
        ImmutableArray<NativeBattleLogicalCommand> commands = graphics.PrimitiveFrame!.Pending.Traverse(Lengths, (reference, count) => (byte[])Call(session, "ReadNativePhysicalPrimitive", reference, count), initialOrderingAddress: root, readAddressedRoot: address =>
        {
            int offset = checked((int)(address - NativeBattleRinoa89Capture.TableAddress));
            return new(arena.ReadUInt32(NativeBattleRinoa89Capture.TableOffset + offset), offset / 4);
        });
        Assert.Equal(new[] { 0x82u, 0x83u }, Pages(commands, "second"));
        NativeBattlePrimitiveProcess process = session.NativeEffectMemory!.PrimitiveProcess!;
        foreach (NativeBattleLogicalCommand command in commands.Where(command => command.Opcode == 0x24))
        {
            bool[] known = process.ReadCommonPacketSnapshot(command.Packet.ByteOffset, 32).Known;
            Assert.All(known.Take(30), value => Assert.True(value));
            Assert.All(known.Skip(30), value => Assert.False(value));
        }
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(bool hosted = true)
    {
        BattleDefinition definition = new() { Combatants = [new() { Id = "first", Name = "First", IsPlayer = true }, new() { Id = "second", Name = "Second" }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        NativeBattleShadowSource shadow = new(Identity, new(4096, 4096, 4096), Enumerable.Repeat(default(NativeBattleVector), 10).ToImmutableArray());
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [], []) { ShadowSource = shadow, SummonColors = NativeBattleGteColorInterpolation.CodeConstants45F270 };
        NativeBattleGraphicsResource resource = new([0, 0]) { PoseSource = Pose, LinkedPoseSource = Pose, LinkedGeometry = [] };
        ImmutableArray<uint> handlers = new uint[256].ToImmutableArray();
        NativeBattlePrimitiveTables tables = new(Lengths, new byte[256].ToImmutableArray(), handlers, handlers, handlers);
        SetField(session, "definition", definition with { NativeGraphics = new(rules, [new("first", 0, resource)], []) { PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(), PrimitiveTables = tables } });
        SetField(session, "presentationNativeTargets", new Dictionary<int, NativeBattleEffectModelBinding> { [7] = new("second", Model(7), resource) });
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { Software = process.Software! with { EnvironmentBuffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(new byte[92].ToImmutableArray()) } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Set(session, nameof(BattleSession.NativeEffectMemory), memory with { PrimitiveProcess = process, Textures = hosted ? Textures() : memory.Textures, Arena = NativeBattleEffectArena.Clear().Write(Coefficients, new byte[8]), ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() });
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 4 };
        NativeBattleModelCpuBank bank = cpu.Cpu.GetBank(0);
        for (int row = 0; row < 4; row++)
        {
            byte[] geometry = Geometry((ushort)(0x80 + row));
            bank = bank.Write(row * 128, geometry);
            NativeBattleModelCpuResourceRow selected = cpu.Rows[row].Set(16, new(0, row * 128));
            selected = selected with { Lengths = selected.Lengths.SetItem(16, geometry.Length) };
            cpu = cpu with { Rows = cpu.Rows.SetItem(row, selected) };
        }
        Set(session, nameof(BattleSession.NativeModelCpuResources), cpu with { Cpu = cpu.Cpu.WithBank(0, bank) });
        Set(session, nameof(BattleSession.NativeFileStaging), NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.FromColdProcess()).Write(0x1D969A8, new byte[4]));
        Set(session, nameof(BattleSession.NativeCamera), NativeBattleCameraState.FromColdProcess() with { Projection = Projection, FocalLength = 1000 });
        var models = Roster.Rows.Select((actor, index) => actor is null ? new NativeBattleGraphicsModel(0) : Model(index)).ToImmutableArray();
        return (session, new(models, Roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = Roster, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() });
    }

    private static NativeBattleGraphicsModel Model(int actor)
    {
        NativeBattleGraphicsModel model = new(2)
        {
            Flags2 = 0, Color40 = 0x808080, Color44 = 0x32808080,
            GeometryWorldMatrix = Identity with { Z = 1000 },
            GeometryBounds52 = new(new(-30, 0, -30), new(30, 0, 30)),
            Transform = new(default, default, 0, 0, 4096),
            LinkedTransform = new(default, default),
            ActiveResourceRow100 = actor == 0 ? 0 : 2,
            LinkedTextureResourceIndex = actor == 0 ? 1 : 3,
            SequenceState = new() { MeshMask124 = 1, DecodedPose = Pose.Sample(0, 1, 0), LinkedPose = new(0, 1, 1, 0) }
        };
        return NativeBattleBodyBoneMatrices.Publish508F60(model, Pose, Pose);
    }

    private static NativeBattleGraphicsState Stage(BattleSession session, NativeBattleGraphicsState graphics, bool typed)
    {
        uint source = StagedRow + 0x200;
        NativeBattleFileStaging staging = session.NativeFileStaging!;
        byte[] geometry = Geometry(0x82);
        staging = staging.Write(source, geometry).Write(StagedRow + 0x300, Pose.Skeleton.AsSpan()).Write(StagedRow + 0x400, Pose.Animations.AsSpan()).Write(StagedRow + 0x500, [0, 0]);
        staging = staging.WriteResourcePointer(StagedRow, new(null, StagedRow + 0x300, Pose.Skeleton.Length))
            .WriteResourcePointer(StagedRow + 4, typed ? new(new(0, 256), null, geometry.Length) : new(null, source, geometry.Length))
            .WriteResourcePointer(StagedRow + 8, new(null, StagedRow + 0x400, Pose.Animations.Length))
            .WriteResourcePointer(StagedRow + 12, new(null, StagedRow + 0x500, 2));
        Set(session, nameof(BattleSession.NativeFileStaging), staging);
        return graphics with { Models = graphics.Models.SetItem(0, graphics.Models[0] with { ModelId4 = 0, ActiveStagedResourceRow100 = StagedRow }) };
    }

    private static NativeBattleGraphicsState DrawShared(BattleSession session, NativeBattleGraphicsState graphics, int actor, uint? privateRoot = null)
    {
        return (NativeBattleGraphicsState)Call(session, "DrawNativeEffectModel5099D0", graphics, actor, Identity with { Z = 1000 }, Projection, 33, (byte)4, (byte)4, privateRoot, (short)0, null, true, true, true, null, null, false, true, false);
    }

    private static uint[] Pages(ImmutableArray<NativeBattleLogicalCommand> commands, string actor)
    {
        return commands.Where(command => command.Opcode == 0x24 && command.ModelTextureOwner?.ActorId == actor).Select(command => command.Words[4] >> 16).Order().ToArray();
    }

    private static NativeBattleEffectTextureState Textures()
    {
        return NativeBattleEffectTextureState.FromColdProcess()
            .WithPresentationTexture("first", new(NativeBattleEffectTextureState.FromColdProcess(), NativeBattlePcTextureWindowCache.Reset4647A0(null)))
            .WithPresentationTexture("second", new(NativeBattleEffectTextureState.FromColdProcess(), NativeBattlePcTextureWindowCache.Reset4647A0(null)));
    }

    private static NativeBattleOriginalEffectState Effect(ushort id) => new(id, id - 1, new(id, 1, 1), new(OriginalOwner(0), 0, BattleModelTargets.From([OriginalOwner(0)]), new BattleActionHeaderReference(0), 0, 0), 1);

    private static NativeBattlePoseSource CreatePose()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        return new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
    }

    private static byte[] Geometry(ushort page)
    {
        byte[] data = new byte[68];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 4);
        short[] xy = [-10, -10, 10, -10, 0, 10, 10, 10];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 6), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(52), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(54), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(66), page);
        return data;
    }

    private static void Set(BattleSession session, string property, object value) => typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
    private static void SetField(BattleSession session, string field, object value) => typeof(BattleSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, value);
    private static object Call(BattleSession session, string method, params object?[] args) => typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args)!;
}
