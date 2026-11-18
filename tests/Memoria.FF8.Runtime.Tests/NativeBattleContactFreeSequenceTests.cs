using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactFreeSequenceTests
{
    private const string TextLoop = "91060C01C105E578A9C378C502E578E7F9A39DA9E6FF";
    private const string LocalStoreLoop = "C102E500A9A39DA9E6FF";

    [Fact]
    public void Authored_text_pose_expression_and_retirement_loop_has_no_contact_producer()
    {
        NativeBattleGraphicsResource resource = Resource(TextLoop);
        NativeBattleContactFreeSequence proof = Assert.IsType<NativeBattleContactFreeSequence>(NativeBattleContactFreeSequence.Read(resource, 1));
        Assert.True(proof.Matches(resource, Retired(20)));
        Assert.False(proof.Matches(resource, Retired(8)));
        Assert.False(proof.Matches(resource, Retired(17)));
    }

    [Theory]
    [InlineData("AAA39DA9E6FF")]
    [InlineData("B2A39DA9E6FF")]
    [InlineData("B4A39DA9E6FF")]
    [InlineData("B7A39DA9E6FF")]
    [InlineData("A501A39DA9E6FF")]
    [InlineData("A701A39DA9E6FF")]
    [InlineData("930001A39DA9E6FF")]
    [InlineData("91AAB2E6FEA39DA9E6FF")]
    [InlineData("A39D")]
    [InlineData("A9E6FF")]
    [InlineData("C201E50BC240E508C201E703A3A9E6FF")]
    [InlineData("C240E508A39DA9E6FF")]
    [InlineData("C201E50BA39DA9E6FF")]
    [InlineData("C201E521A39DA9E6FF")]
    public void Contact_producers_unproved_selectors_bad_branches_and_unretired_programs_are_not_certified(string program)
    {
        Assert.Null(NativeBattleContactFreeSequence.Read(Resource(program), 1));
    }

    [Fact]
    public void Contact_opcode_bytes_inside_text_operands_do_not_create_a_contact_instruction()
    {
        NativeBattleGraphicsResource resource = Resource("91AAB2A39DA9E6FF");
        NativeBattleContactFreeSequence proof = Assert.IsType<NativeBattleContactFreeSequence>(NativeBattleContactFreeSequence.Read(resource, 1));
        Assert.True(proof.Matches(resource, Retired(6)));
    }

    [Fact]
    public void Cursor_reachable_without_retirement_is_not_a_retirement_witness()
    {
        NativeBattleGraphicsResource resource = Resource("C201E703A3A9E6FF");
        Assert.Null(NativeBattleContactFreeSequence.Read(resource, 1));
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(119, false)]
    [InlineData(120, true)]
    public void Expression_store_requires_the_private_local_or_scratch_operand(byte variable, bool permitted)
    {
        NativeBattleGraphicsResource resource = Resource($"C201E5{variable:X2}A39DA9E6FF");
        Assert.Equal(permitted, NativeBattleContactFreeSequence.Read(resource, 1) is not null);
    }

    [Fact]
    public void Proof_rejects_changed_resource_selector_pending_alternate_cursor_and_linked_owner()
    {
        NativeBattleGraphicsResource resource = Resource("A39DA9E6FF");
        NativeBattleContactFreeSequence proof = Assert.IsType<NativeBattleContactFreeSequence>(NativeBattleContactFreeSequence.Read(resource, 1));
        NativeBattleGraphicsModel model = Retired(3);
        Assert.True(proof.Matches(resource, model));
        Assert.False(proof.Matches(Resource("A39DA9E6FF"), model));
        Assert.False(proof.Matches(resource, model with { Sequence = 2 }));
        Assert.False(proof.Matches(resource, model with { ResourceFlags44 = 4 }));
        Assert.False(proof.Matches(resource, model with { Flags0 = 0 }));
        Assert.False(proof.Matches(resource, model with { ModelId4 = 0x8F }));
        Assert.False(proof.Matches(resource, model with { ModelId4 = null }));
        Assert.False(proof.Matches(resource, model with { SequenceState = model.SequenceState! with { Pending3 = 1 } }));
        Assert.False(proof.Matches(resource, model with { SequenceState = model.SequenceState! with { Alternate43 = 1 } }));
        Assert.False(proof.Matches(resource, model with { SequenceState = model.SequenceState! with { Default2 = 2 } }));
        Assert.False(proof.Matches(resource, model with { SequenceState = model.SequenceState! with { Offset46 = 0 } }));
        Assert.False(proof.Matches(resource, model.WriteLinkedModel(OriginalOwner(4))));
        Assert.Null(NativeBattleContactFreeSequence.Read(resource, 0));
    }

    [Fact]
    public void Real_selector_initializes_pending_and_cursor_without_changing_default()
    {
        NativeBattleGraphicsResource resource = Resource("A39DA9E6FF");
        NativeBattleGraphicsModel before = Retired(3) with { SequenceState = new() { Default2 = 2, Pending3 = 7, Offset46 = 3, Callback42 = 5 } };
        NativeBattleGraphicsModel selected = NativeBattleSequenceSelections.SelectResource505BC0(before, resource, 1);
        Assert.Equal((byte)1, selected.Sequence);
        Assert.Equal((ushort)1, selected.ResourceFlags44);
        Assert.Equal((byte)0, selected.SequenceState!.Pending3);
        Assert.Equal((ushort)0, selected.SequenceState.Offset46);
        Assert.Equal((byte)0, selected.SequenceState.Callback42);
        Assert.Equal((byte)2, selected.SequenceState.Default2);
    }

    [Fact]
    public void Exact_linked_ring_requires_each_control_program_and_unchanged_topology()
    {
        BattleSession logical = new(new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy-0", Name = "First" }, new() { Id = "enemy-1", Name = "Second" }] }, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        BattleModelOwner first = new BattleEnemyActorModel(new("enemy-0"));
        BattleModelOwner second = new BattleEnemyActorModel(new("enemy-1"));
        NativeBattleGraphicsResource firstResource = Resource(TextLoop);
        NativeBattleGraphicsResource secondResource = Resource(LocalStoreLoop);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => owner == first ? Retired(20).WriteLinkedModel(second) : owner == second ? Retired(8).WriteLinkedModel(first) : Retired(3));
        NativeBattleGraphicsResource Read(BattleModelOwner owner) => owner == first ? firstResource : secondResource;
        NativeBattleContactFreeRing proof = Assert.IsType<NativeBattleContactFreeRing>(NativeBattleContactFreeRing.Read(graphics.ActorModels, first, 1, Read));
        Assert.Equal(2, proof.Members.Length);
        Assert.True(proof.Matches(graphics.ActorModels, Read));
        Assert.False(proof.Matches(graphics.ActorModels.Set(first, graphics.ActorModels.Get(first).WriteLinkedModel(null)), Read));
        Assert.False(proof.Matches(graphics.ActorModels.Set(second, graphics.ActorModels.Get(second).WriteLinkedModel(null)), Read));
        Assert.False(proof.Matches(graphics.ActorModels.Set(second, graphics.ActorModels.Get(second) with { SequenceState = graphics.ActorModels.Get(second).SequenceState! with { Pending3 = 1 } }), Read));
        Assert.False(proof.Matches(graphics.ActorModels, owner => owner == first ? firstResource : Resource("AAA39DA9E6FF")));
        Assert.Null(NativeBattleContactFreeRing.Read(graphics.ActorModels, first, 1, owner => owner == first ? firstResource : Resource("AAA39DA9E6FF")));
        Assert.Null(NativeBattleContactFreeRing.Read(graphics.ActorModels.Set(second, graphics.ActorModels.Get(second) with { Flags0 = 0 }), first, 1, Read));
        Assert.Null(NativeBattleContactFreeRing.Read(graphics.ActorModels.Set(second, graphics.ActorModels.Get(second) with { ModelId4 = 0x8F }), first, 1, Read));
        Assert.Null(NativeBattleContactFreeRing.Read(graphics.ActorModels.Set(second, graphics.ActorModels.Get(second) with { ModelId4 = null }), first, 1, Read));
        foreach (uint? flags in new uint?[] { null, 8, 16, 24 })
        {
            BattleActorModels blocked = graphics.ActorModels.Set(first, graphics.ActorModels.Get(first) with { Flags8 = flags });
            Assert.Null(NativeBattleContactFreeRing.Read(blocked, first, 1, Read));
            Assert.False(proof.Matches(blocked, Read));
        }
    }

    [Fact]
    public void Retirement_rederives_missing_private_proof_and_rejects_foreign_header_and_replay()
    {
        BattleSession logical = new(new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }], NativeGlobalRandom = new(123) }, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner is BattlePartyModelOwner ? (byte)1 : (byte)103 }) with { EntryReady = true };
        NativeBattleGraphicsResource resource = Resource("A39DA9E6FF");
        NativeBattleGraphicsDefinition resources = NativeBattleHostedTestFixture.CreateGraphicsDefinition(new NativeBattleGraphicsBinding("ally", 1, resource), new NativeBattleGraphicsBinding("enemy", 103, resource));
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics, resources);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess() });
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, aliases));
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelTextureCache), NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster));
        BattleModelOwner owner = new BattleEnemyActorModel(new("enemy"));
        NativeBattleActionHeaderPreparation header = new(owner, 8, 1, -1, 123, 0, null);
        NativeBattleContactPacket contact = new(new(owner, 0, 0, 0, 0, 0, 0), null);
        long token = process.BeginHostedAction(header, [contact, contact]);
        BattleActionHeaderReference reference = new(0);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(process.ActionPackets, reference, null);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), process.Graphics with { ActorModels = process.Graphics.ActorModels.Set(owner, Retired(3)), ActionGraphics = context });
        Set("hostedPresentationInvocation", null);
        Set("lastHostedActionCompleted", token);
        Assert.True(process.HasHostedContactFreeRetirement(token, reference, header));
        Assert.False(process.HasHostedContactFreeRetirement(token + 1, reference, header));
        Assert.False(process.HasHostedContactFreeRetirement(token, reference, header with { Action = 124 }));
        Assert.False(process.HasHostedContactFreeRetirement(token, reference, header with { Source = new BattlePartyActorModel(new("ally")) }));
        Set("lastHostedActionIssued", token + 1);
        Assert.False(process.HasHostedContactFreeRetirement(token, reference, header));
        Set("lastHostedActionIssued", token);

        BattleNativeAction native = new(8, 123, 3, 123)
        {
            AttackType = 0, Power = 0, HitCount = 1, Statuses0 = 0, Statuses1 = 0, MagicId = 0,
            TargetFlags = 0, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0,
            Hit = 1, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleActionPresentation command = new("enemy", "enemy", BattleActionPhase.Recovery, BattleTargetRequest.None, BattleAbility.EnemyAbility, 123) { NativeAction = native };
        BattleSession.ExpandedHostedInvocation invocation = new(command, header, [new("enemy", contact), new("enemy", contact)], null, BattleSession.ExpandedHostedContactDisposition.RequiredImpacts) { HeaderReference = reference };
        BattleSession.ExpandedInvocationProgress progress = new(invocation, roster, null);
        SetLogical("expandedPresentationProcess", process);
        SetLogical("expandedHostedActionToken", (long?)token);
        SetLogical("expandedOperationCursor", new BattleSession.ExpandedOperationCursor([invocation], 0, 0));
        SetLogical("expandedInvocationProgress", progress);
        MethodInfo validate = typeof(BattleSession).GetMethod("ValidateExpandedEffectContactCompletion", BindingFlags.Instance | BindingFlags.NonPublic)!;
        validate.Invoke(logical, []);
        progress.Occurrences.Add(0);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => validate.Invoke(logical, [])).InnerException);
        progress.Occurrences.Add(1);
        validate.Invoke(logical, []);
        progress.Occurrences.Clear();
        BattleSession.ExpandedHostedInvocation damaging = invocation with { Command = command with { NativeAction = native with { AttackType = 1, Power = 1 } } };
        SetLogical("expandedOperationCursor", new BattleSession.ExpandedOperationCursor([damaging], 0, 0));
        SetLogical("expandedInvocationProgress", new BattleSession.ExpandedInvocationProgress(damaging, roster, null));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => validate.Invoke(logical, [])).InnerException);

        void Set(string name, object? value)
        {
            typeof(BattlePresentationProcess).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(process, value);
        }

        void SetLogical(string name, object? value)
        {
            typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(logical, value);
        }
    }

    private static NativeBattleGraphicsModel Retired(ushort offset)
    {
        return new(3) { ModelId4 = 103, Flags2 = 4, Flags8 = 0, HasLinkedModelRing140 = false, Sequence = 1, ResourceFlags44 = 0x46, SequenceState = new() { Default2 = 1, Pending3 = 0, Offset46 = offset } };
    }

    private static NativeBattleGraphicsResource Resource(string hex)
    {
        byte[] program = Convert.FromHexString(hex);
        byte[] bytes = new byte[4 + program.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 4);
        program.CopyTo(bytes, 4);
        return new(bytes.ToImmutableArray()) { AnimationFrames = [1, 1, 1] };
    }
}
