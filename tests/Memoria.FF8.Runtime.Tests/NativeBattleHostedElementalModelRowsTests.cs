using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedElementalModelRowsTests
{
    private static readonly BattleModelOwner Source = OriginalOwner(0);
    private static readonly BattleModelOwner OriginalTarget = OriginalOwner(3);
    private static readonly BattlePartyActorModel AddedAlly = new(new("added-elemental-ally"));
    private static readonly BattleEnemyActorModel AddedEnemy = new(new("added-elemental-enemy"));
    private static readonly BattlePartyActorModel Missing = new(new("unbound-elemental-actor"));
    private static readonly BattleModelRoster HostedRoster = new([Source, AddedAlly, AddedEnemy]);
    private static readonly NativeBattleModelAliases HostedAliases = new(HostedRoster, [(NativeBattleModelSlot.Decode(0), Source)]);
    private static readonly NativeBattleModelTransform Transform = new(default, default, 0, 123, 4096);
    private static readonly NativeBattleActionMatrix View = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);

    [Theory]
    [InlineData(102, 3)]
    [InlineData(102, 7)]
    [InlineData(102, 24)]
    [InlineData(103, 3)]
    [InlineData(103, 7)]
    [InlineData(103, 24)]
    [InlineData(104, 3)]
    [InlineData(104, 7)]
    [InlineData(104, 24)]
    [InlineData(105, 3)]
    [InlineData(105, 7)]
    [InlineData(105, 24)]
    public void Primary_callbacks_retain_exact_original_addresses_and_live_hosted_owners(int effect, int row)
    {
        BattleModelOwner expected = row switch { 3 => OriginalTarget, 7 => AddedAlly, 24 => AddedEnemy, _ => throw new ArgumentOutOfRangeException(nameof(row)) };
        NativeBattleActionPacketsState packets = Packets(effect, expected);
        List<BattleModelOwner> observed = [];
        ElementalFrame frame = Spawn(effect, packets, observed);

        Assert.Contains(expected, observed);
        Assert.All(observed, owner => Assert.True(owner == expected || effect == 104 && owner == Source));
        NativeBattleModulePoolTask primary = Primary(frame.Effect, effect);
        Assert.Equal(expected, primary.ReadExecutionReference<BattleExecutionReference.Model>(PointerOffset(effect), 4).Owner);
        if (expected == OriginalTarget)
        {
            Assert.Equal(0x1D97494u, primary.ReadDword(PointerOffset(effect)));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => primary.ReadDword(PointerOffset(effect)));
        }
        Assert.Equal((ushort)1, primary.ReadWord(12));

        ElementalFrame restored = NativeBattleTestSnapshots.Independent(frame);
        NativeBattleActionPacketsState restoredPackets = NativeBattleTestSnapshots.Independent(packets);
        ElementalFrame continued = Advance(effect, frame, packets, []);
        ElementalFrame resumed = Advance(effect, restored, restoredPackets, []);
        Assert.Equal(expected, Primary(resumed.Effect, effect).ReadExecutionReference<BattleExecutionReference.Model>(PointerOffset(effect), 4).Owner);
        Assert.Equal((ushort)2, Primary(resumed.Effect, effect).ReadWord(12));
        Assert.True(string.Equals(Serialize(continued, effect), Serialize(resumed, effect), StringComparison.Ordinal), "The detached task continued differently.");
    }

    [Theory]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    public void Producer_rejects_an_unbound_contact_before_model_callbacks(int effect)
    {
        NativeBattleActionPacketsState packets = Packets(effect, AddedAlly);
        ElementalFrame frame = Advance(effect, Initialize(effect, packets), packets, []);
        string before = Serialize(frame, effect);
        BattleModelRoster retired = new([Source, AddedEnemy]);
        NativeBattleActionPacketsState malformed = packets.WithRoster(retired, new NativeBattleModelAliases(retired, [(NativeBattleModelSlot.Decode(0), Source)]));
        List<BattleModelOwner> observed = [];

        AssertRejected( () => Advance(effect, frame, malformed, observed));
        AssertUnchanged(before, frame, effect);
        Assert.Empty(observed);
    }

    [Fact]
    public void Blizzaga_rejects_an_unbound_source_before_model_callbacks()
    {
        NativeBattleActionPacketsState packets = Packets(104, AddedAlly);
        ElementalFrame frame = Advance(104, Initialize(104, packets), packets, []);
        string before = Serialize(frame, 104);
        List<BattleModelOwner> observed = [];

        Assert.Throws<NotSupportedException>(() => Advance(104, frame, packets, observed, source: Missing));
        AssertUnchanged(before, frame, 104);
        Assert.Empty(observed);
    }

    [Theory]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    public void Retained_primary_callback_requires_typed_provenance_even_with_known_raw_addresses(int effect)
    {
        NativeBattleActionPacketsState packets = Packets(effect, AddedAlly);
        ElementalFrame frame = Spawn(effect, packets, []);
        foreach (uint address in new[] { 0x1D97494u, 0x1D97495u, uint.MaxValue })
        {
            ElementalFrame branch = NativeBattleTestSnapshots.Independent(frame);
            Primary(branch.Effect, effect).WriteDword(PointerOffset(effect), address);
            List<BattleModelOwner> observed = [];
            AssertRejected( () => Advance(effect, branch, packets, observed));
            Assert.Empty(observed);
        }
    }

    [Fact]
    public void Original_address_decoder_has_no_aliases_for_added_models()
    {
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(HostedRoster, HostedAliases, _ => new(0));
        Assert.True(HostedRoster.Contains(AddedAlly));
        Assert.True(HostedRoster.Contains(AddedEnemy));
        Assert.False(HostedRoster.Contains(Missing));
        Assert.Null(HostedAliases.EncodeAddress(AddedAlly));
        Assert.Null(HostedAliases.EncodeAddress(AddedEnemy));
        Assert.True(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(0x1D97494, out NativeBattleModelSlot slot));
        Assert.Equal(NativeBattleModelSlot.Decode(3), slot);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(0x1D97495, out _));
        Assert.Throws<InvalidDataException>(() => graphics.NativeBindings.DecodeLiteral(3, graphics.ActorModels));
    }

    private static NativeBattleActionPacketsState Packets(int effect, BattleModelOwner target)
    {
        NativeBattleActionPacketsState packets = target == OriginalTarget
            ? NativeBattleActionPacketsState.Create(OriginalRoster, NativeBattleRetainedMemory.FromColdProcess().ActionPackets, OriginalAliases)
            : NativeBattleActionPacketsState.Create(HostedRoster, NativeBattleRetainedActionPackets.Fresh(), HostedAliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(Source, 2, 4, 0, 8, (ushort)effect, null));
        (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(header, 1);
    }

    private sealed record ElementalFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);

    private static ElementalFrame Initialize(int id, NativeBattleActionPacketsState packets)
    {
        BattleModelOwner target = packets.ReadContact(new BattleContactOccurrence(0)).Primary.Recipient;
        NativeBattleEffectParameters parameters = new(Source, 0, BattleModelTargets.From([target]), new(0), 0, 0);
        NativeBattleEffectBinding binding = id switch
        {
            102 => new(102, 0x6DC2D0, 0x6DC2B0),
            103 => new(103, 0x6DA300, 0x6DA2E0) { BlizzaraResources = NativeBattleBlizzaraFaceTests.Resources() },
            104 => new(104, 0x6D6310, 0x6D62F0) { BlizzagaResources = NativeBattleBlizzagaFaceTests.Resources() },
            105 => new(105, 0x6D3DB0, 0x6D3DC0),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        NativeBattleOriginalEffectState effect = new((ushort)id, id - 1, binding, parameters, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeCrtRandomState random = new(1);
        switch (id)
        {
            case 102:
                return new(memory, NativeBattleThundaraController.Initialize6DC2E0(effect, packets), random);
            case 103:
                return new(memory, NativeBattleBlizzaraController.Initialize6DA310(effect, packets), random);
            case 104:
                return new(memory, NativeBattleBlizzagaController.Initialize6D6320(effect, packets), random);
            case 105:
                NativeBattleThundaga105Frame frame = NativeBattleThundaga105Controller.Initialize6D3DE0(new(memory with { Thundaga105 = NativeBattleThundaga105State.FromColdProcess() }, effect, random), ThundagaInputs(packets, []), ThundagaResources);
                return new(frame.Memory, frame.Effect, frame.Random);
            default:
                throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    private static ElementalFrame Spawn(int id, NativeBattleActionPacketsState packets, List<BattleModelOwner> observed)
    {
        ElementalFrame frame = Initialize(id, packets);
        frame = Advance(id, frame, packets, observed);
        return Advance(id, frame, packets, observed);
    }

    private static ElementalFrame Advance(int id, ElementalFrame state, NativeBattleActionPacketsState packets, List<BattleModelOwner> observed, BattleModelOwner? source = null)
    {
        NativeBattleVector Bone(BattleModelOwner owner)
        {
            observed.Add(owner);
            return new((short)(100 + Coordinate(owner) * 20), -200, 300);
        }
        NativeBattleModelTransform ReadTransform(BattleModelOwner owner)
        {
            observed.Add(owner);
            return Transform;
        }
        switch (id)
        {
            case 102:
                NativeBattleThundaraFrame thundara = NativeBattleThundaraController.Advance6DC390(new(state.Memory, state.Effect, state.Random), new(0, packets, Bone, ReadTransform, (frame, _) => frame));
                return new(thundara.Memory, thundara.Effect, thundara.Random);
            case 103:
                NativeBattleBlizzaraFrame blizzara = NativeBattleBlizzaraController.Advance6DA3A0(new(state.Memory, state.Effect, state.Random), new(0, packets, Bone, ReadTransform, (frame, _) => frame));
                return new(blizzara.Memory, blizzara.Effect, blizzara.Random);
            case 104:
                NativeBattleBlizzagaFrame blizzaga = NativeBattleBlizzagaController.Advance6D6400(new(state.Memory, state.Effect, state.Random), new(0, packets, source ?? Source, View, Bone, ReadTransform, owner => new(10, 20, 30, Coordinate(owner)), (frame, request) =>
                {
                    observed.Add(request.Actor);
                    return new(frame, -100, 200);
                }, (frame, task, _, _, _) => new(frame, task), (frame, _) => frame));
                return new(blizzaga.Memory, blizzaga.Effect, blizzaga.Random);
            case 105:
                NativeBattleThundaga105Frame thundaga = NativeBattleThundaga105Controller.Advance6D3E50(new(state.Memory, state.Effect, state.Random), ThundagaInputs(packets, observed), ThundagaResources);
                return new(thundaga.Memory, thundaga.Effect, thundaga.Random);
            default:
                throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    private static string Serialize(ElementalFrame frame, int id) => id switch
    {
        102 => NativeBattleTestSnapshots.Json(new NativeBattleThundaraFrame(frame.Memory, frame.Effect, frame.Random)),
        103 => NativeBattleTestSnapshots.Json(new NativeBattleBlizzaraFrame(frame.Memory, frame.Effect, frame.Random)),
        104 => NativeBattleTestSnapshots.Json(new NativeBattleBlizzagaFrame(frame.Memory, frame.Effect, frame.Random)),
        105 => NativeBattleTestSnapshots.Json(new NativeBattleThundaga105Frame(frame.Memory, frame.Effect, frame.Random)),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static short Coordinate(BattleModelOwner owner)
    {
        if (owner == AddedAlly)
        {
            return 7;
        }
        if (owner == AddedEnemy)
        {
            return 24;
        }
        return (short)OriginalSlot(owner);
    }

    private static NativeBattleModulePoolTask Primary(NativeBattleOriginalEffectState effect, int id)
    {
        uint descriptor = id switch
        {
            102 => NativeBattleThundaraController.PrimaryDescriptor,
            103 => NativeBattleBlizzaraController.PrimaryDescriptor,
            104 => NativeBattleBlizzagaController.PrimaryDescriptor,
            105 => NativeBattleThundaga105Controller.PrimaryDescriptor,
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        return effect.Pools.Single(pool => pool.DescriptorAddress == descriptor).Slots[0];
    }

    private static int PointerOffset(int id) => id == 104 ? 24 : 16;

    private static void AssertUnchanged(string before, ElementalFrame frame, int effect)
    {
        Assert.True(string.Equals(before, Serialize(frame, effect), StringComparison.Ordinal), "Rejected hosted input changed the arena or task state.");
    }

    private static void AssertRejected(Action advance) => Assert.Throws<InvalidDataException>(advance);

    private static NativeBattleThundaga105Inputs ThundagaInputs(NativeBattleActionPacketsState packets, List<BattleModelOwner> observed) => new(packets, _ => new byte[8].ToImmutableArray(), _ => new(0, -500, 0, 0), row =>
    {
        observed.Add(row);
        return 0xAB123456u;
    }, _ => 4096, (frame, task, row, anchor) =>
    {
        observed.Add(row);
        NativeBattleThundaga105ProgramDraw draw = NativeBattleThundaga105ProgramDrawing.Advance6D43B0(frame.Memory, task, ThundagaResources.ProgramSource, ThundagaResources.NodeSelector.Data, anchor, View, View, new(View, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        return new(frame with { Memory = draw.Memory }, draw.Task);
    }, (frame, _) => frame);

    private static readonly NativeBattleThundaga105Resources ThundagaResources = CreateThundagaResources();

    private static NativeBattleThundaga105Resources CreateThundagaResources()
    {
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 64);
        var pairs = Enumerable.Range(0, 14).Select(index =>
        {
            uint address = (uint)(0x10000 + index * 0x100);
            return new NativeBattleThundagaFragmentPair(new(1, address, new byte[84].ToImmutableArray()), new(address + 84, new byte[32].ToImmutableArray()));
        }).ToImmutableArray();
        return new(new("animation", []), new(0x13009C0, source.ToImmutableArray()), new(0, 0x131136C, 0, 0, new byte[12].ToImmutableArray()), new(0, 0x13115DC, 0, 0, new byte[12].ToImmutableArray()), new("sound", new byte[4].ToImmutableArray()), new("selector", new byte[32].ToImmutableArray()), pairs);
    }
}
