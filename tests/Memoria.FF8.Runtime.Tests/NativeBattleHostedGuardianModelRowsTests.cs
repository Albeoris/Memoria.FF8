using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedGuardianModelRowsTests
{
    private static readonly BattleModelOwner Party = new BattlePartyActorModel(new("party"));
    private static readonly BattleModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattleModelOwner Other = new BattleEnemyActorModel(new("other"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostedEntriesPreserveCentroidsAndKeepExtraCastersUnaliased(bool partyCaster)
    {
        BattleModelOwner caster = partyCaster ? Party : Enemy;
        var fixture = NativeBattleDiablos325Tests.Fixture();
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, caster, 325, Party, Enemy);
        BattleActorModels models = Models(fixture.Models);
        NativeBattleDiablos325Frame diablos = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = Parameters(caster, targets) } };
        NativeBattleDiablos325Frame native = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Frame hosted = NativeBattleDiablos325Entry.Initialize6541E0(diablos, packets, models);
        NativeBattleDiablos325State diablosState = Independent(hosted.Memory.Diablos325!);
        diablosState.Validate();
        Assert.Equal(native.Memory.Diablos325! with { Caster5188 = null }, diablosState);
        Assert.Equal(native.Memory.Textures!.Count, hosted.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => (diablosState with { Caster5188 = NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(0)) + 1 }).Validate());

        var (carbunclePackets, carbuncleTargets) = Prepare(roster, caster, 278, Party, Enemy);
        NativeBattleCarbuncle278Frame carbuncle = Carbuncle(fixture.Frame, Parameters(caster, carbuncleTargets));
        NativeBattleCarbuncle278Frame carbuncleHosted = NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, carbunclePackets, models, 42);
        NativeBattleCarbuncle278State carbuncleState = Independent(carbuncleHosted.Memory.Carbuncle278!);
        carbuncleState.Validate();
        Assert.Null(carbuncleState.Caster8124);
        Assert.Equal(-2, carbuncleState.Centroid81C4);
        Assert.Equal(-2002, carbuncleState.Base81CC);
        Assert.Equal(-5650, carbuncleState.Lift81D0);
        Assert.Equal(carbuncle.Random.Draw(out _), carbuncleHosted.Random);
        Assert.Equal(2, carbuncleHosted.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => (carbuncleState with { Caster8124 = NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(0)) + 1 }).Validate());
    }

    [Fact]
    public void EntriesRejectUnboundCasterRecipientsAndEmptyActions()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, Party, 325, Party, Enemy);
        NativeBattleEffectParameters parameters = Parameters(Party, targets);
        NativeBattleDiablos325Frame diablos = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = parameters } };
        NativeBattleCarbuncle278Frame carbuncle = Carbuncle(fixture.Frame, parameters);
        BattleActorModels models = Models(fixture.Models);
        Assert.Throws<KeyNotFoundException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos, packets, fixture.Models));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, packets, models.Remove(Enemy), 42));
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos with { Effect = diablos.Effect with { Parameters = parameters with { Source = Other } } }, packets, models));
        BattleModelRoster retired = new([Party]);
        NativeBattleActionPacketsState forged = packets.WithRoster(retired, new(retired, []));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, forged, models, 42));
        var (empty, _) = Prepare(roster, Party, 325);
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos, empty, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, empty, models, 42));
        NativeBattlePandemona291Frame pandemona = Pandemona(parameters);
        NativeBattlePandemona291Resources resources = pandemona.Effect.Binding.Pandemona291Resources!;
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Entry.Initialize6ED250(pandemona, resources, empty, _ => throw new InvalidOperationException()));
        Assert.Equal(0, carbuncle.Memory.Textures!.Count);
        Assert.Empty(diablos.Effect.Pools);
    }

    [Fact]
    public void DiablosSinkPreflightsAllStoredRecipientsAndResumesTheirOwners()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        var (packets, targets) = Prepare(Roster(), Party, 325, Party, Enemy);
        NativeBattleDiablos325Frame source = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = Parameters(Party, targets) } };
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(source, packets, Models(fixture.Models));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2505190, NativeBattleEffectNodeMemory.ArenaAddress, 0x994, 1);
        (pool, int slot) = pool.Append(0x654E7E);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 368);
        NativeBattleDiablos325State sink = frame.Memory.Diablos325! with { Recipients = [new(Party, new(0)), new(Enemy, new(1))] };
        frame = frame with { Memory = frame.Memory with { Diablos325 = sink } };
        List<BattleModelOwner> reads = [];
        List<BattleModelOwner> draws = [];
        Dictionary<BattleModelOwner, NativeBattleGraphicsModel> actors = Models(fixture.Models).Entries.ToDictionary(entry => entry.Owner, entry => entry.State);
        var services = new NativeBattleDiablos325RecipientServices(row =>
        {
            reads.Add(row);
            return actors[row];
        }, (row, actor) => actors[row] = actor, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, row, _, _) =>
        {
            draws.Add(row);
            return current;
        }) { ReadForeignBounds = _ => (100, -100) };

        NativeBattleDiablos325Frame forged = frame with { Memory = frame.Memory with { Diablos325 = sink with { Recipients = sink.Recipients.SetItem(1, new(Other, new(1))) } } };
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Recipients.Advance654E7E(forged, task, packets, services));
        Assert.Empty(reads);
        NativeBattleDiablos325ProgrammeStep step = NativeBattleDiablos325Recipients.Advance654E7E(frame, Independent(task), packets, services);
        Assert.Equal(new[] { Party, Enemy }, draws);
        Assert.Equal(new[] { Party, Party, Enemy, Enemy }, reads);
        Assert.Equal((ushort)100, step.Task.ReadWord(0xA4));
        Assert.Equal(12, actors[Enemy].Flags0 & 12);
    }

    [Fact]
    public void PandemonaConstructionSortsHostedRecordsAndRejectsContradictoryRingBeforeLookup()
    {
        var (packets, targets) = Prepare(Roster(), Party, 291, Party, Enemy);
        NativeBattlePandemona291Frame frame = Pandemona(Parameters(Party, targets));
        int vibrations = 0;
        List<BattleModelOwner> reads = [];
        var inputs = new NativeBattlePandemona291ConstructionInputs(packets, ReadModel, (current, row) => new(current, new((short)(row == Party ? 7 : 24), -10)), current =>
        {
            vibrations++;
            return new(current, 456);
        });
        var (retiring, _) = Prepare(Roster(), Party, 291, Party, Enemy);
        BattleModelRoster retired = new([Party]);
        NativeBattleActionPacketsState forged = retiring.WithRoster(retired, new(retired, []));
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs with { Packets = forged }));
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Entry.Initialize6ED250(frame, frame.Effect.Binding.Pandemona291Resources!, forged, ReadModel));
        Assert.Equal(0, vibrations);
        Assert.Empty(reads);

        NativeBattlePandemona291Frame started = NativeBattlePandemona291Entry.Initialize6ED250(frame, frame.Effect.Binding.Pandemona291Resources!, packets, ReadModel);
        Assert.Equal(Party, started.Effect.Parameters!.Source);
        Assert.Equal(0x1D99A78u, started.Memory.Pandemona291!.Action255625C);
        NativeBattlePandemona291Frame result = NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs);
        Assert.Equal(1, vibrations);
        NativeBattleEffectTaskPool creature = result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8);
        Assert.Equal(Enemy, creature.Slots[0].ReadExecutionReference<BattleExecutionReference.Model>(0x14, 4).Owner);
        Assert.Equal(Party, creature.Slots[0].ReadExecutionReference<BattleExecutionReference.Model>(0x34, 4).Owner);
        Assert.Throws<NotSupportedException>(() => creature.Slots[0].ReadDword(0x14));
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs with
        {
            ReadModel = row => row == Enemy ? ReadModel(row) with { LinkedModel = Other } : ReadModel(row)
        }));
        Assert.DoesNotContain(Other, reads);

        NativeBattleGraphicsModel ReadModel(BattleModelOwner row)
        {
            reads.Add(row);
            return new(2)
            {
                HasLinkedModelRing140 = false,
                Rotation12 = 3,
                Rotation14 = -1,
                Rotation16 = 5,
                Transform = new(default, new((short)(row == Party ? 7 : 24), 3, 100), 7, 0, 0)
            };
        }
    }

    [Fact]
    public void PandemonaSessionTargetReaderRejectsUnboundRecordBeforeChangingTheGraphicsBank()
    {
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, Party, 291, Party);
        NativeBattlePandemona291Frame frame = Pandemona(Parameters(Party, targets));
        const int creature = 0x21A4;
        frame = frame with
        {
            Memory = frame.Memory with
            {
                Pandemona291 = frame.Memory.Pandemona291! with { Pause2556258 = 0, Ground25562E0 = 0 },
                Arena = frame.Memory.Arena!.Write(creature + 0x14, BitConverter.GetBytes(NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(4))))
            }
        };
        var owner = new BattleSession(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, packets);
        var graphics = NativeBattleHostedTestFixture.Graphics(roster, new(roster, []), Models(NativeBattleDiablos325Tests.Fixture().Models).Get);
        MethodInfo callback = typeof(BattleSession).GetMethod("AdvanceNativePandemona291Targets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException invalid = Assert.Throws<TargetInvocationException>(() => callback.Invoke(owner, [frame, NativeBattleEffectNodeMemory.ArenaAddress + creature, 224, null, graphics, false]));
        Assert.Contains("execution reference", Assert.IsType<NotSupportedException>(invalid.InnerException).Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BattleModelRoster Roster() => new([Party, Enemy]);

    private static BattleActorModels Models(BattleActorModels native) =>
        new([new(Party, native.Get(OriginalOwner(0)) with { GeometryWorldMatrix = NativeBattleForeignModelGeometry.Identity }),
             new(Enemy, native.Get(OriginalOwner(1)) with { Flags0 = 2, GeometryWorldMatrix = NativeBattleForeignModelGeometry.Identity })]);

    private static (NativeBattleActionPacketsState, BattleModelTargets) Prepare(BattleModelRoster roster, BattleModelOwner caster, ushort effect, params BattleModelOwner[] targets)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), new(roster, []));
        (packets, _) = packets.Prepare(new(caster, 240, 0, 0, 3, effect, null));
        packets = packets.AppendContacts(targets.Select(target => new NativeBattleContactPacket(new(target, 0, 0, 0, 0, 0, 0), null)));
        (packets, _) = packets.Publish(targets.Length);
        return (packets, BattleModelTargets.From(targets));
    }

    private static NativeBattleEffectParameters Parameters(BattleModelOwner caster, BattleModelTargets targets) =>
        new(caster, 0, targets, new BattleActionHeaderReference(0), 0, 0);

    private static NativeBattleCarbuncle278Frame Carbuncle(NativeBattleDiablos325Frame source, NativeBattleEffectParameters parameters)
    {
        NativeBattleEffectArena arena = source.Memory.Arena!;
        arena = arena.Write(0x2A120, arena.Read(0x100, 66));
        NativeBattleEffectMemory memory = source.Memory with { Arena = arena, Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() };
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), parameters, null) { ResourcePointers = [new(0x2508110, 0x100)] };
        return new(memory, effect, NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattlePandemona291Frame Pandemona(NativeBattleEffectParameters parameters)
    {
        uint[] addresses = [0x13A0E58, 0x13A4F88, 0x13A6508, 0x13A8508, 0x13AB5C0, 0x13AC1B8, 0x13ACDB0];
        var resources = new NativeBattlePandemona291Resources(PandemonaTim(0x31620, 5644, "first"), PandemonaTim(0x2220, 524, "second"))
        {
            AnimationSources = addresses.Select(address =>
            {
                byte[] bytes = new byte[16];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0xFF01);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0x1234);
                return new NativeBattlePandemona291Animation(address, bytes.ToImmutableArray());
            }).ToImmutableArray()
        };
        NativeBattleOriginalEffectState effect = new(291, 290, new(291, 0x6ED250, 0) { Pandemona291Resources = resources }, parameters, null);
        NativeBattlePandemona291State state = NativeBattlePandemona291State.FromColdProcess() with { ModelBuffer25562F4 = NativeBattleEffectNodeMemory.ArenaAddress };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Pandemona291 = state };
        return new(memory, effect, NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleEffectHeapAllocation PandemonaTim(int length, int clutLength, string identity)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 9);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), clutLength);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + clutLength), length - 8 - clutLength);
        return new(identity, [.. bytes]);
    }

    private static T Independent<T>(T value) => NativeBattleTestSnapshots.Independent(value);
}
