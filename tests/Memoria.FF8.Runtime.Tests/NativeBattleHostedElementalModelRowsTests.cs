using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedElementalModelRowsTests
{
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
    public void Primary_callbacks_roundtrip_native_addresses_and_hosted_handles(int effect, int row)
    {
        NativeBattleGraphicsRoster? roster = row == 3 ? null : Roster();
        NativeBattleActionPacketsState packets = Packets(effect, row, roster);
        List<int> observed = [];
        ElementalFrame frame = Spawn(effect, packets, observed);

        Assert.Contains(row, observed);
        Assert.All(observed, actor => Assert.True(actor == row || effect == 104 && actor == 0));
        uint expected = row == 3 ? 0x1D97494u : NativeBattleGraphicsModelAddress.Encode(row);
        NativeBattleModulePoolTask primary = Primary(frame.Effect, effect);
        Assert.Equal(expected, primary.ReadDword(PointerOffset(effect)));
        Assert.Equal((ushort)1, primary.ReadWord(12));

        ElementalFrame restored = Restore(frame, effect);
        NativeBattleActionPacketsState restoredPackets = NativeBattleTestSnapshots.RestoreJson<NativeBattleActionPacketsState>(NativeBattleTestSnapshots.Json(packets));
        ElementalFrame continued = Advance(effect, frame, packets, []);
        ElementalFrame resumed = Advance(effect, restored, restoredPackets, []);
        Assert.Equal(expected, Primary(resumed.Effect, effect).ReadDword(PointerOffset(effect)));
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
        NativeBattleActionPacketsState packets = Packets(effect, 7, Roster());
        ElementalFrame frame = Advance(effect, Initialize(effect, packets), packets, []);
        string before = Serialize(frame, effect);
        NativeBattleContactPacket contact = packets.ReadContact(0);
        NativeBattleActionPacketsState malformed = packets with { Contacts = packets.Contacts.SetItem(0, contact with { Primary = contact.Primary with { ActorIndex = 8 } }) };
        List<int> observed = [];

        AssertRejected(effect, () => Advance(effect, frame, malformed, observed));
        AssertUnchanged(before, frame, effect);
        Assert.Empty(observed);
    }

    [Fact]
    public void Blizzaga_rejects_an_unbound_source_before_model_callbacks()
    {
        NativeBattleActionPacketsState packets = Packets(104, 7, Roster());
        ElementalFrame frame = Advance(104, Initialize(104, packets), packets, []);
        string before = Serialize(frame, 104);
        List<int> observed = [];

        Assert.Throws<NotSupportedException>(() => Advance(104, frame, packets, observed, source: 8));
        AssertUnchanged(before, frame, 104);
        Assert.Empty(observed);
    }

    [Theory]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    public void Retained_primary_callback_rejects_holes_and_nonmodel_addresses(int effect)
    {
        NativeBattleActionPacketsState packets = Packets(effect, 7, Roster());
        ElementalFrame frame = Spawn(effect, packets, []);
        string snapshot = Serialize(frame, effect);

        foreach (uint address in new[] { NativeBattleGraphicsModelAddress.Encode(8), NativeBattleGraphicsModelAddress.Encode(7) + 1, NativeBattleGraphicsModelAddress.Encode(24) + NativeBattleGraphicsModelAddress.RowSize, 0x1D97704u })
        {
            ElementalFrame branch = Restore(frame, effect, snapshot);
            Primary(branch.Effect, effect).WriteDword(PointerOffset(effect), address);
            List<int> observed = [];
            AssertRejected(effect, () => Advance(effect, branch, packets, observed));
            Assert.Empty(observed);
        }

        ElementalFrame shorter = Restore(frame, effect, snapshot);
        Primary(shorter.Effect, effect).WriteDword(PointerOffset(effect), NativeBattleGraphicsModelAddress.Encode(24));
        NativeBattleActionPacketsState shorterPackets = Packets(effect, 7, new(Roster().Rows.Take(8).ToImmutableArray()));
        AssertRejected(effect, () => Advance(effect, shorter, shorterPackets, []));
    }

    [Fact]
    public void Address_decoder_distinguishes_native_extent_from_bound_hosted_rows()
    {
        NativeBattleGraphicsRoster roster = Roster();

        Assert.True(roster.ContainsRow(0));
        Assert.True(roster.ContainsRow(7));
        Assert.True(roster.ContainsRow(24));
        Assert.False(roster.ContainsRow(-1));
        Assert.False(roster.ContainsRow(8));
        Assert.False(roster.ContainsRow(25));
        Assert.True(NativeBattleGraphicsModelAddress.TryDecode(NativeBattleGraphicsModelAddress.Encode(24), out int row, roster));
        Assert.Equal(24, row);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecode(NativeBattleGraphicsModelAddress.Encode(8), out row, roster));
        Assert.Equal(-1, row);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecode(NativeBattleGraphicsModelAddress.Encode(3), out row, roster));
        Assert.Equal(-1, row);
        Assert.False(NativeBattleGraphicsModelAddress.TryDecode(NativeBattleGraphicsModelAddress.Encode(7), out _, roster: null));
        Assert.True(NativeBattleGraphicsModelAddress.TryDecode(0x1D97494, out row, roster: null));
        Assert.Equal(3, row);
    }

    private static NativeBattleGraphicsRoster Roster()
    {
        var rows = new NativeBattleGraphicsActor?[25].ToImmutableArray().SetItem(0, new("source", true)).SetItem(7, new("first", true)).SetItem(24, new("last", false));
        return new(rows);
    }

    private static NativeBattleActionPacketsState Packets(int effect, int row, NativeBattleGraphicsRoster? roster)
    {
        NativeBattleActionPacketsState packets = roster is null ? NativeBattleRetainedMemory.FromColdProcess().ActionPackets : NativeBattleActionPacketsState.FromPresentationStorage(roster);
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, (ushort)effect, null));
        (packets, _) = packets.AppendContact(new(new((byte)row, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private sealed record ElementalFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);

    private static ElementalFrame Initialize(int id, NativeBattleActionPacketsState packets)
    {
        int row = packets.ReadContact(0).Primary.ActorIndex;
        NativeBattleGraphicsActorSet targets = default(NativeBattleGraphicsActorSet).Add(row);
        NativeBattleEffectParameters parameters = new(0, 0, targets.NativePhysicalBits, 0, 0, 0)
        {
            PresentationTargets = packets.PresentationRoster is null ? null : targets
        };
        NativeBattleEffectBinding binding = id switch
        {
            102 => new(102, 0x6DC2D0, 0x6DC2B0),
            103 => new(103, 0x6DA300, 0x6DA2E0) { BlizzaraResources = NativeBattleBlizzaraFaceTests.Resources() },
            104 => new(104, 0x6D6310, 0x6D62F0) { BlizzagaResources = NativeBattleBlizzagaFaceTests.Resources() },
            105 => new(105, 0x6D3DB0, 0x6D3DC0),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        NativeBattleOriginalEffectState effect = new((ushort)id, id - 1, binding, parameters, null) { PresentationRoster = packets.PresentationRoster };
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

    private static ElementalFrame Spawn(int id, NativeBattleActionPacketsState packets, List<int> observed)
    {
        ElementalFrame frame = Initialize(id, packets);
        frame = Advance(id, frame, packets, observed);
        return Advance(id, frame, packets, observed);
    }

    private static ElementalFrame Advance(int id, ElementalFrame state, NativeBattleActionPacketsState packets, List<int> observed, int source = 0)
    {
        NativeBattleVector Bone(int row)
        {
            observed.Add(row);
            return new((short)(100 + row * 20), -200, 300);
        }
        NativeBattleModelTransform ReadTransform(int row)
        {
            observed.Add(row);
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
                NativeBattleBlizzagaFrame blizzaga = NativeBattleBlizzagaController.Advance6D6400(new(state.Memory, state.Effect, state.Random), new(0, packets, source, View, Bone, ReadTransform, row => new(10, 20, 30, (short)row), (frame, request) =>
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

    private static ElementalFrame Restore(ElementalFrame frame, int id, string? snapshot = null)
    {
        snapshot ??= Serialize(frame, id);
        switch (id)
        {
            case 102:
                NativeBattleThundaraFrame thundara = NativeBattleTestSnapshots.RestoreJson<NativeBattleThundaraFrame>(snapshot);
                return new(thundara.Memory, thundara.Effect, thundara.Random);
            case 103:
                NativeBattleBlizzaraFrame blizzara = NativeBattleTestSnapshots.RestoreJson<NativeBattleBlizzaraFrame>(snapshot);
                return new(blizzara.Memory, blizzara.Effect, blizzara.Random);
            case 104:
                NativeBattleBlizzagaFrame blizzaga = NativeBattleTestSnapshots.RestoreJson<NativeBattleBlizzagaFrame>(snapshot);
                return new(blizzaga.Memory, blizzaga.Effect, blizzaga.Random);
            case 105:
                NativeBattleThundaga105Frame thundaga = NativeBattleTestSnapshots.RestoreJson<NativeBattleThundaga105Frame>(snapshot);
                return new(thundaga.Memory, thundaga.Effect, thundaga.Random);
            default:
                throw new ArgumentOutOfRangeException(nameof(id));
        }
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

    private static void AssertRejected(int effect, Action advance)
    {
        if (effect == 105)
        {
            Assert.Throws<InvalidDataException>(advance);
        }
        else
        {
            Assert.Throws<NotSupportedException>(advance);
        }
    }

    private static NativeBattleThundaga105Inputs ThundagaInputs(NativeBattleActionPacketsState packets, List<int> observed) => new(packets, _ => new byte[8].ToImmutableArray(), _ => new(0, -500, 0, 0), row =>
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
