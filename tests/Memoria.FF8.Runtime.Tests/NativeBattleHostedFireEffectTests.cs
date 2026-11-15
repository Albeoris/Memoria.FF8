using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedFireEffectTests
{
    private static readonly BattleModelOwner[] Owners = Enumerable.Range(0, 11).Select(index => (BattleModelOwner)new BattlePartyActorModel(new($"party-{index}")))
        .Concat(Enumerable.Range(0, 14).Select(index => new BattleEnemyActorModel(new($"enemy-{index}")))).ToArray();
    private static readonly BattleModelRoster Roster = new(Owners);
    private static readonly NativeBattleModelAliases Aliases = new(Roster, []);

    [Fact]
    public void ThreeHostedHeadersDrainInOrderWithTheirOriginalContactFrames()
    {
        BattleModelOwner[] rows = Owners;
        BattleModelOwner[][] groups = [rows, rows.Reverse().ToArray(), [.. rows.Skip(7), .. rows.Take(7)]];
        var fixture = Fixture(groups);
        Assert.Equal(75, fixture.Inputs.Packets.ContactCount);
        Assert.Equal(2, fixture.Inputs.Packets.ExportRetained().Headers[0].ReadOriginalByte(17));
        Assert.Equal(1128, NativeBattleFireEffect.RequiredChildCapacity(fixture.Effect, fixture.Inputs.Packets));
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        NativeBattleEffectTaskPool bank = effect.Pools[1];
        Assert.Equal(1128, bank.Slots.Length);
        Assert.Equal((ushort)36, bank.Stride);
        Assert.True(bank.BankAddress > 0x24D5D6Cu);
        Assert.Equal((uint?)0x24CA828, effect.ReturnedDescriptor);
        ImmutableDictionary<BattleModelOwner, int> completion = effect.FireModelCompletion!;
        Assert.All(completion.Values, value => Assert.Equal(1, value));
        Assert.Equal(25, completion.Count);

        List<BattleContactOccurrence> applied = [];
        List<(int Frame, BattleContactOccurrence Occurrence)> reported = [];
        List<BattleModelOwner> anchors = [];
        Func<BattleModelOwner, NativeBattleModuleAnchor> readAnchor = fixture.Inputs.ReadModelAnchor;
        NativeBattleFireInputs inputs = fixture.Inputs with
        {
            ApplyContact = applied.Add,
            ReadModelAnchor = row =>
            {
                anchors.Add(row);
                return readAnchor(row);
            }
        };
        int frame = 0;
        while (effect.ReturnedDescriptor != 0 && frame < 80)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            if (frame == 1)
            {
                byte[] link = NativeBattleEffectTaskMemory.CaptureBank(step.Effect.Pools[1]).Read(4, sizeof(uint));
                Assert.Equal(bank.BankAddress + 36, BinaryPrimitives.ReadUInt32LittleEndian(link));
            }
            reported.AddRange(step.Contacts.Select(ordinal => (frame, ordinal)));
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
            frame++;
        }

        Assert.Equal((uint?)0, effect.ReturnedDescriptor);
        Assert.Equal(Occurrences(75), applied);
        Assert.Equal(Enumerable.Range(0, 75).Select(ordinal => (8 + 12 * (ordinal / 25), new BattleContactOccurrence(ordinal))), reported);
        Assert.Equal(groups.SelectMany(group => group), anchors);
        Assert.All(effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void HostedSingleHighRowPausesAndContinuesFromIndependentCopyAtContactPhase()
    {
        var fixture = Fixture([[Owners[24]]]);
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        NativeBattleFireInputs inputs = fixture.Inputs;
        for (int frame = 0; frame < 8; frame++)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            Assert.Empty(step.Contacts);
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
        }
        Assert.Equal(0, effect.FireModelCompletion![Owners[24]]);
        string children = NativeBattleTestSnapshots.Json(effect.Pools[1]);
        NativeBattleFireStep paused = NativeBattleFireEffect.Step(effect, inputs with { GraphicsFlags = 0x200 });
        Assert.Equal(inputs.CrtRandom, paused.CrtRandomAfter);
        Assert.Equal(children, NativeBattleTestSnapshots.Json(paused.Effect.Pools[1]));
        Assert.Empty(paused.Contacts);
        Assert.NotEmpty(paused.Draws);

        FireCheckpoint restored = NativeBattleTestSnapshots.Independent(new FireCheckpoint(paused.Effect, inputs.Packets, paused.CrtRandomAfter));
        List<BattleContactOccurrence> uninterrupted = [];
        List<BattleContactOccurrence> resumed = [];
        NativeBattleFireStep expected = NativeBattleFireEffect.Step(paused.Effect, inputs with { ApplyContact = uninterrupted.Add });
        NativeBattleFireStep actual = NativeBattleFireEffect.Step(restored.Effect, inputs with { Packets = restored.Packets, CrtRandom = restored.Random, ApplyContact = resumed.Add });

        Assert.Equal(new BattleContactOccurrence(0), Assert.Single(expected.Contacts));
        Assert.Equal(expected.Contacts.ToArray(), actual.Contacts.ToArray());
        Assert.Equal(uninterrupted.ToArray(), resumed.ToArray());
        Assert.Equal(new BattleContactOccurrence(0), Assert.Single(resumed));
        Assert.Equal(expected.CrtRandomAfter, actual.CrtRandomAfter);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Effect), NativeBattleTestSnapshots.Json(actual.Effect));
        Assert.Equal(NativeBattleTestSnapshots.Json(StableDraws(expected)), NativeBattleTestSnapshots.Json(StableDraws(actual)));
    }

    [Fact]
    public void OneHostedHeaderPublishesAllThreeHundredOccurrencesAtContactPhase()
    {
        BattleModelOwner[] rows = Enumerable.Range(0, 300).Select(index => Owners[(index % 3) switch { 0 => 7, 1 => 16, _ => 24 }]).ToArray();
        var fixture = Fixture([rows]);
        Assert.Equal(300, fixture.Inputs.Packets.ReadHeader(new BattleActionHeaderReference(0)).ReadContactCount());
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        Assert.Equal(4501, effect.Pools[1].Slots.Length);
        List<BattleContactOccurrence> applied = [];
        NativeBattleFireInputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        for (int frame = 0; frame <= 8; frame++)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            Assert.Equal(Occurrences(frame == 8 ? 300 : 0), step.Contacts);
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
        }
        Assert.Equal(Occurrences(300), applied);
        Assert.Equal(rows, applied.Select(ordinal => inputs.Packets.ReadContact(ordinal).Primary.Recipient));
    }

    [Fact]
    public void FireRejectsMismatchedOrUnboundRosterRowsBeforeCreatingTaskPools()
    {
        var fixture = Fixture([[Owners[7], Owners[16], Owners[24]]]);
        BattleModelOwner replacement = new BattleEnemyActorModel(new("other-actor"));
        NativeBattleOriginalEffectState different = fixture.Effect with { Parameters = fixture.Effect.Parameters! with { TargetActors = BattleModelTargets.From([Owners[7], replacement, Owners[24]]) } };
        Assert.Throws<InvalidDataException>(() => NativeBattleFireEffect.RequiredChildCapacity(different, fixture.Inputs.Packets));

        BattleModelRoster retired = new(Owners.Where(owner => owner != Owners[24]));
        NativeBattleOriginalEffectState unbound = fixture.Effect with { Parameters = fixture.Effect.Parameters! with { TargetActors = BattleModelTargets.From([Owners[7], Owners[16]]) } };
        unbound.Validate(retired);
        NativeBattleActionPacketsState invalidPackets = fixture.Inputs.Packets.WithRoster(retired, new(retired, []));
        Assert.Throws<InvalidDataException>(() => invalidPackets.ReadHeaderContacts(new BattleActionHeaderReference(0)));

        Assert.Throws<InvalidDataException>(() => NativeBattleFireEffect.InitializePools(fixture.Effect, fixture.Inputs.Packets, 0x24CA848));
        Assert.Empty(fixture.Effect.Pools);
        Assert.Null(fixture.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void NativeFireKeepsItsOriginalPhysicalTaskHeaderAndCompletionBank()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(OriginalRoster, NativeBattleRetainedMemory.FromColdProcess().ActionPackets, OriginalAliases);
        (packets, _) = packets.Prepare(new(OriginalOwner(3), 2, 11, -1, 1, 2, null));
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(4), 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(2, 1, new(2, 0x6298A0, 0x629880), new(OriginalOwner(3), 0, BattleModelTargets.From([OriginalOwner(4)]), new BattleActionHeaderReference(0), 0, 0), null);
        NativeBattleOriginalEffectState effect = NativeBattleFireEffect.InitializePools(selected, packets);
        NativeBattleEffectTaskPool bank = effect.Pools[1];
        Assert.Equal(0x24CA838u, bank.DescriptorAddress);
        Assert.Equal(0x24CA848u, bank.BankAddress);
        Assert.Equal(150, bank.Slots.Length);
        Assert.Equal(0x24CBD60u, bank.BankAddress + (uint)(bank.Stride * bank.Slots.Length));
        Assert.Equal(7, effect.FireModelCompletion!.Count);
        byte[] task = NativeBattleEffectTaskMemory.CaptureBank(bank).Read(0, 34);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0x80, 0xA3, 0x62, 0 }, task[..12]);
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan(32)));
    }

    private static NativeBattleOriginalEffectState Enter(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        int capacity = NativeBattleFireEffect.RequiredChildCapacity(effect, packets);
        (_, uint address) = NativeBattlePresentationTaskStorage.Reserve(NativeBattleEffectMemory.FromColdProcess(), effect, packets.Roster, 36, capacity);
        return NativeBattleFireEffect.InitializePools(effect, packets, address);
    }

    private static IEnumerable<BattleContactOccurrence> Occurrences(int count) => Enumerable.Range(0, count).Select(index => new BattleContactOccurrence(index));

    private static (NativeBattleOriginalEffectState Effect, NativeBattleFireInputs Inputs) Fixture(BattleModelOwner[][] groups)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(Roster, NativeBattleRetainedActionPackets.Fresh(), Aliases);
        BattleModelTargets targets = BattleModelTargets.From(groups.SelectMany(group => group));
        foreach (BattleModelOwner[] group in groups)
        {
            (packets, _) = packets.Prepare(new(Owners[0], 2, 11, -1, 1, 2, null));
            packets = packets.AppendContacts(group.Select(owner => new NativeBattleContactPacket(new(owner, 0, 0, 0, 0, 0, 0), null)));
            (packets, _) = packets.Publish(group.Length);
        }
        packets = packets.WritePlayerRepeatMode(new BattleActionHeaderReference(0), checked((byte)groups.Length));
        NativeBattleEffectParameters parameters = new(Owners[0], 0, targets, new BattleActionHeaderReference(0), 0, 0);
        NativeBattleOriginalEffectState effect = new(2, 1, new(2, 0x6298A0, 0x629880), parameters, null);
        ImmutableArray<NativeBattleModuleResource> resources = [Sprite(0xDEDC2C, 0), Sprite(0xDEDDD8, 0), Sprite(0xDEDF84, 0), Sprite(0x123450, 5)];
        NativeBattleFireInputs inputs = new(0, packets, NativeCrtRandomState.FromColdProcess(), owner => new((short)(Array.IndexOf(Owners, owner) * 100), -600, 200, 2000), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), resources);
        return (effect, inputs);
    }

    private static NativeBattleModuleResource Sprite(uint address, byte id)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int frame = 0; frame <= 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 44);
        }
        return new(id, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static ImmutableArray<NativeBattleModuleDraw> StableDraws(NativeBattleFireStep step)
    {
        // Allocation generations belong to the process-local visual identity map.
        return step.Draws.Select(draw => draw.PilotIdentity is { } identity ? draw with { PilotIdentity = identity with { Generation = 0 } } : draw).ToImmutableArray();
    }

    private sealed record FireCheckpoint(NativeBattleOriginalEffectState Effect, NativeBattleActionPacketsState Packets, NativeCrtRandomState Random);
}
