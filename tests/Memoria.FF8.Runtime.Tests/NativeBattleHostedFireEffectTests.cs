using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedFireEffectTests
{
    [Fact]
    public void ThreeHostedHeadersDrainInOrderWithTheirOriginalContactFrames()
    {
        byte[] rows = Enumerable.Range(0, 25).Select(row => (byte)row).ToArray();
        byte[][] groups = [rows, rows.Reverse().ToArray(), [.. rows.Skip(7), .. rows.Take(7)]];
        var fixture = Fixture(groups);
        Assert.Equal(75, fixture.Inputs.Packets.ContactCount);
        Assert.Equal(2, fixture.Inputs.Packets.Headers[0].ReadByte(17));
        Assert.Equal(1128, NativeBattleFireEffect.RequiredChildCapacity(fixture.Effect, fixture.Inputs.Packets));
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        NativeBattleEffectTaskPool bank = effect.Pools[1];
        Assert.Equal(1128, bank.Slots.Length);
        Assert.Equal((ushort)36, bank.Stride);
        Assert.True(bank.BankAddress > 0x24D5D6Cu);
        Assert.Equal((uint?)0x24CA828, effect.ReturnedDescriptor);
        ImmutableArray<int> completion = effect.FireModelCompletion!.Value;
        Assert.All(completion, value => Assert.Equal(1, value));
        Assert.Equal(25, completion.Length);

        List<int> applied = [];
        List<(int Frame, int Ordinal)> reported = [];
        List<int> anchors = [];
        Func<int, NativeBattleModuleAnchor> readAnchor = fixture.Inputs.ReadModelAnchor;
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
        Assert.Equal(Enumerable.Range(0, 75), applied);
        Assert.Equal(Enumerable.Range(0, 75).Select(ordinal => (8 + 12 * (ordinal / 25), ordinal)), reported);
        Assert.Equal(groups.SelectMany(group => group).Select(row => (int)row), anchors);
        Assert.All(effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void HostedSingleHighRowPausesAndContinuesFromIndependentJsonAtContactPhase()
    {
        var fixture = Fixture([[24]]);
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        NativeBattleFireInputs inputs = fixture.Inputs;
        for (int frame = 0; frame < 8; frame++)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            Assert.Empty(step.Contacts);
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
        }
        Assert.Equal(0, effect.FireModelCompletion!.Value[24]);
        string children = NativeBattleTestSnapshots.Json(effect.Pools[1]);
        NativeBattleFireStep paused = NativeBattleFireEffect.Step(effect, inputs with { GraphicsFlags = 0x200 });
        Assert.Equal(inputs.CrtRandom, paused.CrtRandomAfter);
        Assert.Equal(children, NativeBattleTestSnapshots.Json(paused.Effect.Pools[1]));
        Assert.Empty(paused.Contacts);
        Assert.NotEmpty(paused.Draws);

        string checkpoint = NativeBattleTestSnapshots.Json(new FireCheckpoint(paused.Effect, inputs.Packets, paused.CrtRandomAfter));
        FireCheckpoint restored = NativeBattleTestSnapshots.RestoreJson<FireCheckpoint>(checkpoint);
        List<int> uninterrupted = [];
        List<int> resumed = [];
        NativeBattleFireStep expected = NativeBattleFireEffect.Step(paused.Effect, inputs with { ApplyContact = uninterrupted.Add });
        NativeBattleFireStep actual = NativeBattleFireEffect.Step(restored.Effect, inputs with { Packets = restored.Packets, CrtRandom = restored.Random, ApplyContact = resumed.Add });

        Assert.Equal(0, Assert.Single(expected.Contacts));
        Assert.Equal(expected.Contacts.ToArray(), actual.Contacts.ToArray());
        Assert.Equal(uninterrupted.ToArray(), resumed.ToArray());
        Assert.Equal(0, Assert.Single(resumed));
        Assert.Equal(expected.CrtRandomAfter, actual.CrtRandomAfter);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Effect), NativeBattleTestSnapshots.Json(actual.Effect));
        Assert.Equal(NativeBattleTestSnapshots.Json(StableDraws(expected)), NativeBattleTestSnapshots.Json(StableDraws(actual)));
    }

    [Fact]
    public void OneHostedHeaderPublishesAllThreeHundredOccurrencesAtContactPhase()
    {
        byte[] rows = Enumerable.Range(0, 300).Select(index => (byte)((index % 3) switch { 0 => 7, 1 => 16, _ => 24 })).ToArray();
        var fixture = Fixture([rows]);
        Assert.Equal(300, fixture.Inputs.Packets.Headers[0].ReadContactCount());
        NativeBattleOriginalEffectState effect = Enter(fixture.Effect, fixture.Inputs.Packets);
        Assert.Equal(4501, effect.Pools[1].Slots.Length);
        List<int> applied = [];
        NativeBattleFireInputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        for (int frame = 0; frame <= 8; frame++)
        {
            NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, inputs);
            Assert.Equal(Enumerable.Range(0, frame == 8 ? 300 : 0), step.Contacts);
            effect = step.Effect;
            inputs = inputs with { CrtRandom = step.CrtRandomAfter };
        }
        Assert.Equal(Enumerable.Range(0, 300), applied);
        Assert.Equal(rows, applied.Select(ordinal => inputs.Packets.ReadContact(ordinal).Primary.ActorIndex));
    }

    [Fact]
    public void FireRejectsMismatchedOrUnboundRosterRowsBeforeCreatingTaskPools()
    {
        var fixture = Fixture([[7, 16, 24]]);
        BattleModelRoster roster = fixture.Effect.PresentationRoster!;
        NativeBattleOriginalEffectState different = fixture.Effect with { PresentationRoster = new(roster.Rows.SetItem(16, new("other-actor", false))) };
        Assert.Throws<InvalidDataException>(() => NativeBattleFireEffect.RequiredChildCapacity(different, fixture.Inputs.Packets));

        BattleModelRoster withHole = new(roster.Rows.SetItem(24, null));
        BattleModelTargets boundTargets = default(BattleModelTargets).Add(7).Add(16);
        NativeBattleOriginalEffectState unbound = fixture.Effect with
        {
            PresentationRoster = withHole,
            Parameters = fixture.Effect.Parameters! with { PresentationTargets = boundTargets }
        };
        unbound.Validate();
        NativeBattleActionPacketsState invalidPackets = fixture.Inputs.Packets with { PresentationRoster = withHole };
        Assert.Throws<InvalidDataException>(() => NativeBattleFireEffect.RequiredChildCapacity(unbound, invalidPackets));

        Assert.Throws<InvalidDataException>(() => NativeBattleFireEffect.InitializePools(fixture.Effect, fixture.Inputs.Packets, 0x24CA848));
        Assert.Empty(fixture.Effect.Pools);
        Assert.Null(fixture.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void NativeFireKeepsItsOriginalPhysicalTaskHeaderAndCompletionBank()
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(3, 2, 11, -1, 1, 2, null));
        (packets, _) = packets.AppendContact(new(new(4, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(2, 1, new(2, 0x6298A0, 0x629880), new(3, 0, 0x10, 0, 0, 0), null);
        NativeBattleOriginalEffectState effect = NativeBattleFireEffect.InitializePools(selected, packets);
        NativeBattleEffectTaskPool bank = effect.Pools[1];
        Assert.Equal(0x24CA838u, bank.DescriptorAddress);
        Assert.Equal(0x24CA848u, bank.BankAddress);
        Assert.Equal(150, bank.Slots.Length);
        Assert.Equal(0x24CBD60u, bank.BankAddress + (uint)(bank.Stride * bank.Slots.Length));
        Assert.Equal(7, effect.FireModelCompletion!.Value.Length);
        byte[] task = NativeBattleEffectTaskMemory.CaptureBank(bank).Read(0, 34);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0x80, 0xA3, 0x62, 0 }, task[..12]);
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan(32)));
    }

    private static NativeBattleOriginalEffectState Enter(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        int capacity = NativeBattleFireEffect.RequiredChildCapacity(effect, packets);
        (_, uint address) = NativeBattlePresentationTaskStorage.Reserve(NativeBattleEffectMemory.FromColdProcess(), effect, 36, capacity);
        return NativeBattleFireEffect.InitializePools(effect, packets, address);
    }

    private static (NativeBattleOriginalEffectState Effect, NativeBattleFireInputs Inputs) Fixture(byte[][] groups)
    {
        BattleModelRoster roster = new(Enumerable.Range(0, 25).Select(row => (BattleModelOwner?)new BattleModelOwner($"actor-{row}", row < 11)).ToImmutableArray());
        int occurrences = groups.Sum(group => group.Length);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(roster, Math.Max(49, occurrences));
        BattleModelTargets targets = default;
        foreach (byte[] group in groups)
        {
            (packets, _) = packets.Prepare(new(0, 2, 11, -1, 1, 2, null));
            foreach (byte row in group)
            {
                targets = targets.Add(row);
            }
            packets = packets.AppendContacts(group.Select(row => new NativeBattleContactPacket(new(row, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0))));
            (packets, _) = packets.Publish(group.Length);
        }
        packets = packets.WritePlayerRepeatMode(0, checked((byte)groups.Length));
        NativeBattleEffectParameters parameters = new(0, 0, targets.NativePhysicalBits, 0, 0, 0) { PresentationTargets = targets };
        NativeBattleOriginalEffectState effect = new(2, 1, new(2, 0x6298A0, 0x629880), parameters, null) { PresentationRoster = roster };
        ImmutableArray<NativeBattleModuleResource> resources = [Sprite(0xDEDC2C, 0), Sprite(0xDEDDD8, 0), Sprite(0xDEDF84, 0), Sprite(0x123450, 5)];
        NativeBattleFireInputs inputs = new(0, packets, NativeCrtRandomState.FromColdProcess(), row => new((short)(row * 100), -600, 200, 2000), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), resources);
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
