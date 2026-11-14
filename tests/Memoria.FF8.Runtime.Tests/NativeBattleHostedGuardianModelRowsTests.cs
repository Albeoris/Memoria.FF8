using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedGuardianModelRowsTests
{
    [Theory]
    [InlineData((byte)7)]
    [InlineData((byte)24)]
    public void HostedEntriesPreserveCentroidsAndRetainBoundCasterPointers(byte caster)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, caster, 325, 7, 24);
        ImmutableArray<NativeBattleGraphicsModel> models = Models(fixture.Models);
        NativeBattleDiablos325Frame diablos = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = Parameters(caster, targets) } };
        NativeBattleDiablos325Frame native = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleDiablos325Frame hosted = NativeBattleDiablos325Entry.Initialize6541E0(diablos, packets, models);
        NativeBattleDiablos325State diablosState = RoundTrip(hosted.Memory.Diablos325!);
        diablosState.Validate();
        Assert.Equal(native.Memory.Diablos325! with { Caster5188 = NativeBattleGraphicsModelAddress.Encode(caster) }, diablosState);
        Assert.Equal(native.Memory.Textures!.Count, hosted.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => (diablosState with { Caster5188 = diablosState.Caster5188 + 1 }).Validate());

        var (carbunclePackets, carbuncleTargets) = Prepare(roster, caster, 278, 7, 24);
        NativeBattleCarbuncle278Frame carbuncle = Carbuncle(fixture.Frame, Parameters(caster, carbuncleTargets));
        NativeBattleCarbuncle278Frame carbuncleHosted = NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, carbunclePackets, models, 42);
        NativeBattleCarbuncle278State carbuncleState = RoundTrip(carbuncleHosted.Memory.Carbuncle278!);
        carbuncleState.Validate();
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(caster), carbuncleState.Caster8124);
        Assert.Equal(-2, carbuncleState.Centroid81C4);
        Assert.Equal(-2002, carbuncleState.Base81CC);
        Assert.Equal(-5650, carbuncleState.Lift81D0);
        Assert.Equal(carbuncle.Random.Draw(out _), carbuncleHosted.Random);
        Assert.Equal(2, carbuncleHosted.Memory.Textures!.Count);
        Assert.Throws<InvalidDataException>(() => (carbuncleState with { Caster8124 = carbuncleState.Caster8124 + 1 }).Validate());
    }

    [Fact]
    public void EntriesRejectMismatchedBankAndUnboundCasterOrRecipientBeforeResourceUse()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, 7, 325, 7, 24);
        NativeBattleEffectParameters parameters = Parameters(7, targets);
        NativeBattleDiablos325Frame diablos = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = parameters, ResourcePointers = [] } };
        NativeBattleCarbuncle278Frame carbuncle = Carbuncle(fixture.Frame, parameters);
        carbuncle = carbuncle with { Effect = carbuncle.Effect with { ResourcePointers = [] } };
        ImmutableArray<NativeBattleGraphicsModel> models = Models(fixture.Models);
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos, packets, fixture.Models));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, packets, models.SetItem(24, models[24] with { Index = 8 }), 42));
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos with { Effect = diablos.Effect with { Parameters = parameters with { Actor78 = 8 } } }, packets, models));
        NativeBattleActionPacketsState forged = packets with { Contacts = packets.Contacts.SetItem(1, Packet(8)) };
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, forged, models, 42));
        NativeBattleActionPacketsState empty = NativeBattleActionPacketsState.FromPresentationStorage(roster, NativeBattleActionPacketsState.ContactCapacity);
        (empty, _) = empty.Prepare(new(7, 240, 0, 0, 3, 325, null));
        (empty, _) = empty.Publish(0);
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Entry.Initialize6541E0(diablos, empty, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Entry.Initialize680C50(carbuncle, empty, models, 42));
        NativeBattlePandemona291Frame pandemona = Pandemona(parameters);
        NativeBattlePandemona291Resources resources = pandemona.Effect.Binding.Pandemona291Resources!;
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Entry.Initialize6ED250(pandemona, resources, empty, _ => throw new InvalidOperationException()));
        Assert.Equal(0, carbuncle.Memory.Textures!.Count);
        Assert.Empty(diablos.Effect.Pools);
    }

    [Fact]
    public void DiablosSinkPreflightsAllStoredRecipientsAndResumesTheirActualRows()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        var (packets, targets) = Prepare(Roster(), 7, 325, 7, 24);
        NativeBattleDiablos325Frame source = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = Parameters(7, targets) } };
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(source, packets, Models(fixture.Models));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2505190, NativeBattleEffectNodeMemory.ArenaAddress, 0x994, 1);
        (pool, int slot) = pool.Append(0x654E7E);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 368).WriteDword(0x94, NativeBattleGraphicsModelAddress.Encode(7)).WriteDword(0xA0, NativeBattleGraphicsModelAddress.Encode(24));
        List<int> reads = [];
        List<int> draws = [];
        Dictionary<int, NativeBattleGraphicsModel> actors = Models(fixture.Models).ToDictionary(actor => actor.Index);
        var services = new NativeBattleDiablos325RecipientServices(row =>
        {
            reads.Add(row);
            return actors[row];
        }, (row, actor) => actors[row] = actor, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, row, _) =>
        {
            draws.Add(row);
            return current;
        }) { ReadForeignBounds = _ => (100, -100) };

        NativeBattleModulePoolTask forged = RoundTrip(task).WriteDword(0xA0, NativeBattleGraphicsModelAddress.Encode(8));
        Assert.Throws<InvalidDataException>(() => NativeBattleDiablos325Recipients.Advance654E7E(frame, forged, packets, services));
        Assert.Empty(reads);
        NativeBattleDiablos325ProgrammeStep step = NativeBattleDiablos325Recipients.Advance654E7E(frame, RoundTrip(task), packets, services);
        Assert.Equal(new[] { 7, 24 }, draws);
        Assert.Equal(new[] { 7, 7, 24, 24 }, reads);
        Assert.Equal((ushort)100, step.Task.ReadWord(0xA4));
        Assert.Equal(12, actors[24].Flags0 & 12);
    }

    [Fact]
    public void PandemonaConstructionSortsHostedRecordsAndChecksRingBeforeReadingTheLinkedRow()
    {
        var (packets, targets) = Prepare(Roster(), 7, 291, 7, 24);
        NativeBattlePandemona291Frame frame = Pandemona(Parameters(7, targets));
        int vibrations = 0;
        List<int> reads = [];
        var inputs = new NativeBattlePandemona291ConstructionInputs(packets, ReadModel, (current, row) => new(current, new((short)row, -10)), current =>
        {
            vibrations++;
            return new(current, 456);
        });
        NativeBattleActionPacketsState forged = packets with { Contacts = packets.Contacts.SetItem(1, Packet(8)) };
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs with { Packets = forged }));
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Entry.Initialize6ED250(frame, frame.Effect.Binding.Pandemona291Resources!, forged, ReadModel));
        Assert.Equal(0, vibrations);
        Assert.Empty(reads);

        NativeBattlePandemona291Frame started = NativeBattlePandemona291Entry.Initialize6ED250(frame, frame.Effect.Binding.Pandemona291Resources!, packets, ReadModel);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), started.Memory.Pandemona291!.Caster2556260);
        NativeBattlePandemona291Frame result = NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs);
        Assert.Equal(1, vibrations);
        NativeBattleEffectTaskPool creature = result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8);
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(24), creature.Slots[0].ReadDword(0x14));
        Assert.Equal(NativeBattleGraphicsModelAddress.Encode(7), creature.Slots[0].ReadDword(0x34));
        Assert.Throws<InvalidDataException>(() => NativeBattlePandemona291Construction.Initialize6ED3B6(frame, inputs with
        {
            ReadModel = row => ReadModel(row).WriteLinkedModelIndex140(row == 24 ? 4 : null)
        }));
        Assert.DoesNotContain(4, reads);

        NativeBattleGraphicsModel ReadModel(int row)
        {
            reads.Add(row);
            return new(row, 2)
            {
                HasLinkedModelRing140 = false,
                Rotation12 = 3,
                Rotation14 = -1,
                Rotation16 = 5,
                Transform = new(default, new((short)row, 3, 100), 7, 0, 0)
            };
        }
    }

    [Fact]
    public void PandemonaSessionTargetReaderRejectsUnboundRecordBeforeChangingTheGraphicsBank()
    {
        BattleModelRoster roster = Roster();
        var (packets, targets) = Prepare(roster, 7, 291, 7);
        NativeBattlePandemona291Frame frame = Pandemona(Parameters(7, targets));
        const int creature = 0x21A4;
        frame = frame with
        {
            Memory = frame.Memory with
            {
                Pandemona291 = frame.Memory.Pandemona291! with { Pause2556258 = 0, Ground25562E0 = 0 },
                Arena = frame.Memory.Arena!.Write(creature + 0x14, BitConverter.GetBytes(NativeBattleGraphicsModelAddress.Encode(8)))
            }
        };
        var owner = new BattleSession(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, packets);
        var graphics = new NativeBattleGraphicsState(Models(NativeBattleDiablos325Tests.Fixture().Models), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        MethodInfo callback = typeof(BattleSession).GetMethod("AdvanceNativePandemona291Targets", BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException invalid = Assert.Throws<TargetInvocationException>(() => callback.Invoke(owner, [frame, NativeBattleEffectNodeMemory.ArenaAddress + creature, 224, null, graphics, false]));
        Assert.Contains("target record", Assert.IsType<NotSupportedException>(invalid.InnerException).Message);
    }

    private static BattleModelRoster Roster() => new(Enumerable.Range(0, 25).Select(row => row switch
    {
        7 => new BattleModelOwner("party", true),
        24 => new BattleModelOwner("enemy", false),
        _ => null
    }).ToImmutableArray());

    private static ImmutableArray<NativeBattleGraphicsModel> Models(ImmutableArray<NativeBattleGraphicsModel> native) =>
        Enumerable.Range(0, 25).Select(row => row switch
        {
            7 => native[0] with { Index = row, GeometryWorldMatrix = NativeBattleForeignModelGeometry.Identity },
            24 => native[1] with { Index = row, Flags0 = 2, GeometryWorldMatrix = NativeBattleForeignModelGeometry.Identity },
            _ => new NativeBattleGraphicsModel(row, 0)
        }).ToImmutableArray();

    private static (NativeBattleActionPacketsState, BattleModelTargets) Prepare(BattleModelRoster roster, byte caster, ushort effect, params byte[] targets)
    {
        var bridge = new NativeBattleEffectContactBridge<byte>(targets.Select(target => (target, Packet(target))), roster);
        return bridge.PrepareHosted(240, 0, 3, effect, caster);
    }

    private static NativeBattleEffectParameters Parameters(byte caster, BattleModelTargets targets) =>
        new(caster, 0, targets.NativePhysicalBits, 0, 0, 0) { PresentationTargets = targets };

    private static NativeBattleContactPacket Packet(byte target) => new(new(target, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));

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

    private static T RoundTrip<T>(T value) => NativeBattleTestSnapshots.RestoreJson<T>(NativeBattleTestSnapshots.Json(value))!;
}
