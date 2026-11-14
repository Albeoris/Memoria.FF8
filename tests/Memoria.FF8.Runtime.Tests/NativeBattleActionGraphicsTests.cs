namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionGraphicsTests
{
    private static readonly BattleActionHeaderReference FirstHeader = new(0);
    private static readonly BattlePartyActorModel Source = new(new("caster"));
    private static readonly BattlePartyActorModel Cover1 = new(new("cover-1"));
    private static readonly BattlePartyActorModel Cover2 = new(new("cover-2"));
    private static readonly BattleEnemyActorModel Target = new(new("target"));
    private static readonly BattleEnemyActorModel Second = new(new("second"));

    [Fact]
    public void FirstHeaderTargetCountComesFromTypedNormalization()
    {
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(Prepare(1, count: 1), FirstHeader);
        Assert.Equal(1, NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(action));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(action with { FirstHeaderTargetCount = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(null));
        Assert.Equal(0, NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(NativeBattleActionGraphics.Normalize(Prepare(1), FirstHeader, action)));
    }

    [Theory]
    [InlineData(33)]
    [InlineData(45)]
    [InlineData(48)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(52)]
    public void Target_expression_writes_its_exact_word_without_clobbering_neighbor_fields(byte variable)
    {
        NativeBattleGraphicsModel model = new(2) { Rotation12 = 10, Rotation14 = 20, Rotation16 = 30, Transform = new(new(1, 2, 3), new(4, 5, 6), 7, 8, 9) { Scale148 = new(11, 12, 13) } };
        NativeBattleGraphicsModel expected = variable switch
        {
            33 or 48 => model with { Rotation12 = -2 },
            49 => model with { Rotation14 = -2 },
            50 => model with { Rotation16 = -2 },
            45 => model with { Transform = model.Transform with { Scale148 = new(11, -2, 13) } },
            52 => model with { Transform = model.Transform with { Position28 = new(4, -2, 6) } },
            _ => throw new InvalidDataException()
        };
        Assert.Equal(expected, NativeBattleActionGraphics.WriteSequenceTarget5048E0(model, variable, 0x1FFFE));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(2, 0, 0, 0, 1)]
    [InlineData(2, 0, 0, 0x40, 0)]
    [InlineData(2, 0, 1, 0x40, 1)]
    [InlineData(10, 0, 1, 0, 0)]
    [InlineData(2, 2, 1, 0, 0)]
    [InlineData(2, 8, 1, 0, 0)]
    [InlineData(2, 16, 1, 0, 0)]
    public void Sequence_busy_query_uses_508540_and_excludes_the_current_model(int flags0, uint flags8, byte kind, ushort resourceFlags, short expected)
    {
        BattleActorModels models = new([new(Source, new(2) { Flags8 = 0, Kind5 = 1 }), new(Target, new((ushort)flags0) { Flags8 = flags8, Kind5 = kind, ResourceFlags44 = resourceFlags })]);
        Assert.Equal(expected, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, Source));
    }

    [Fact]
    public void BusyQueryIncludesExtraOwnersAndRejectsUnknownCurrentOwner()
    {
        var extra = new BattlePartyActorModel(new("extra"));
        BattleActorModels models = new([new(Source, new(0)), new(extra, new(2) { Flags8 = 0, Kind5 = 1 })]);

        Assert.Equal(1, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, Source));
        Assert.Equal(0, NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, extra));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AnyOtherModelBusy5044B0(models, Target));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(0x3D, 0)]
    [InlineData(0x3F, 2)]
    public void SequenceFlagReadsTypedActiveOccurrence(byte contactFlags, short expected)
    {
        NativeBattleActionPacketsState packets = Fresh();
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Source, 1, 13, (sbyte)(contactFlags ^ 2), 0, 0, null));
        BattleModelOwner? cover = (contactFlags & 8) == 0 ? null : Cover1;
        packets = packets.AppendContacts([Contact(Target, 0, contactFlags, cover), Contact(Second, 0, (byte)(contactFlags ^ 2), cover)]);
        (packets, _) = packets.Publish(2);
        packets = packets.ActivateHeader(header);

        Assert.Equal(expected, NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets));
        Assert.Equal(expected ^ 2, NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets.AdvanceContact()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroContactHeaderReadsOnlyItsRetainedCameraFlag(bool observed)
    {
        NativeBattleRetainedActionPackets retained = observed
            ? NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[32 * 20], new byte[49 * 24])
            : NativeBattleRetainedActionPackets.Fresh();
        BattleModelRoster roster = new([Source]);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, retained, new(roster, [(NativeBattleModelSlot.Decode(0), Source)]));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets));
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Source, 1, 13, 2, 0, 0, null));
        (packets, _) = packets.Publish(0);
        NativeBattleActionPacketsState active = packets.ActivateHeader(header);
        Assert.Equal(new BattleContactCursor(0), active.ActiveContact);
        if (observed)
        {
            Assert.Equal(0, NativeBattleActionGraphics.ReadCameraFlag50A6E0(active));
            Assert.Equal(0, NativeBattleActionGraphics.ReadCameraFlag50A6E0(active.ResetCounts()));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadCameraFlag50A6E0(active));
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0x50BD00)]
    [InlineData(0, 0, 0, 1, 0x50BD80)]
    [InlineData(0x26, 1, 0, 0, 0x50B2A0)]
    [InlineData(0xEC, 1, 0, 0, 0x50B830)]
    [InlineData(0xF5, 1, 0, 0, 0x50B830)]
    [InlineData(0xED, 1, 0, 0, 0x50BEE0)]
    [InlineData(0xEE, 1, 0, 0, 0x50BEE0)]
    [InlineData(0xF1, 1, 0, 0, 0x50BDC0)]
    [InlineData(0xF7, 1, 0, 0, 0x50B0C0)]
    [InlineData(0xFC, 65535, 0, 0, 0x50B190)]
    [InlineData(2, 65535, 0, 0, 0x50BC20)]
    [InlineData(2, 1, 0, 0, 0x50BB00)]
    [InlineData(2, 1, 187, 0, 0x50B190)]
    public void SelectorUsesOriginalCommandAndHeaderBranches(int command, int action, int effect, int count, int expected)
    {
        NativeBattleActionPacketsState packets = Prepare((byte)command, (ushort)action, (ushort)effect, 0, (byte)count);
        Assert.Equal((uint)expected, NativeBattleActionGraphics.SelectService(packets.ReadHeader(FirstHeader)).Callback);
    }

    [Theory]
    [InlineData(0x26, 15)]
    [InlineData(0xF4, 70)]
    [InlineData(0xFE, 15)]
    public void SpecialGuardianActionsOwnSequence11AndTheirGraphicsFlag(int command, int action)
    {
        NativeBattleActionServiceSelection selected = NativeBattleActionGraphics.SelectService(Prepare((byte)command, (ushort)action).ReadHeader(FirstHeader));
        Assert.Equal(0x50A9A0u, selected.Callback);
        Assert.Equal(0x40000000u, selected.SetGraphicsFlags);
        Assert.Equal((byte?)11, selected.Sequence);
    }

    [Fact]
    public void NormalSequenceAndDarksideFlagFollowTheDefaultBranch()
    {
        NativeBattleActionServiceSelection selected = NativeBattleActionGraphics.SelectService(Prepare(0x1C, 8, 100, 7).ReadHeader(FirstHeader));
        Assert.Equal(0x50A9A0u, selected.Callback);
        Assert.Equal(0x10000000u, selected.SetGraphicsFlags);
    }

    [Fact]
    public void RepeatedHeadersKeepOccurrenceOrderForFirstTargetAndRosterOrderForSets()
    {
        NativeBattleActionDataReference data = new(0x47E970, 12);
        NativeBattleActionPacketsState packets = Fresh();
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Source, 240, 0, -1, 0, 187, data));
        packets = packets.AppendContacts([Contact(Second, 0x30, 8, Cover2), Contact(Second)]);
        (packets, _) = packets.Publish(2);
        (packets, _) = packets.Prepare(new(Source, 240, 0, -1, 0, 187, null));
        (packets, _) = packets.AppendContact(Contact(Target, 0, 8, Cover1));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(header, 2);

        NativeBattleActionGraphicsState result = NativeBattleActionGraphics.Normalize(packets, header);
        Assert.Equal(new BattleModelOwner[] { Target, Second }, result.TargetActors.Enumerate());
        Assert.Equal(new BattleModelOwner[] { Cover1, Cover2 }, result.CoverActors.Enumerate());
        Assert.Equal(Second, result.FirstTarget);
        Assert.Equal(Cover1, result.CoverSource);
        Assert.Equal(Source, result.CameraActor);
        Assert.Equal(2, result.FirstHeaderTargetCount);
        Assert.Equal(2, result.HitFlags);
        Assert.Equal(data, result.PendingData);
    }

    [Fact]
    public void TargetSetPreservesThirtyDistinctOwnersWithoutTreatingContactsAsASet()
    {
        BattleModelOwner[] ordered = Enumerable.Range(0, 30).Select(index => (BattleModelOwner)new BattleEnemyActorModel(new($"enemy-{index}"))).ToArray();
        NativeBattleActionPacketsState packets = Fresh(new BattleModelRoster(ordered.Prepend(Source)));
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Source, 0, 0, -1, 0, 0, null));
        NativeBattleContactPacket[] occurrences = ordered.Reverse().Select(owner => Contact(owner)).Append(Contact(ordered[^1])).ToArray();
        packets = packets.AppendContacts(occurrences);
        (packets, _) = packets.Publish(occurrences.Length);
        packets = packets.WritePlayerRepeatMode(header, 1);

        NativeBattleActionGraphicsState result = NativeBattleActionGraphics.Normalize(packets, header);
        Assert.Equal(ordered, result.TargetActors.Enumerate());
        Assert.Equal(ordered[^1], result.FirstTarget);
        Assert.Equal(31, result.FirstHeaderTargetCount);
    }

    [Fact]
    public void EmptyHeaderRetainsBoundOwnersAndRejectsARetiredReference()
    {
        NativeBattleActionPacketsState packets = Prepare();
        NativeBattleActionGraphicsState prior = new(FirstHeader, Source, new(0), BattleModelTargets.From([Target]), BattleModelTargets.From([Cover1]), Cover1, Target, 2, null, 0x50B190);
        NativeBattleActionGraphicsState next = NativeBattleActionGraphics.Normalize(packets, FirstHeader, prior);
        Assert.Equal(Target, next.FirstTarget);
        Assert.Equal(Cover1, next.CoverSource);
        Assert.True(next.TargetActors.IsEmpty);
        Assert.True(next.CoverActors.IsEmpty);
        Assert.Equal(0, next.HitFlags);
        var retired = new BattleEnemyActorModel(new("retired"));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.Normalize(packets, FirstHeader, prior with { FirstTarget = retired }));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.Normalize(packets, FirstHeader, prior with { CoverSource = retired }));
    }

    [Fact]
    public void UnknownRetainedRepeatByteCannotBeInventedForAiHeaders()
    {
        NativeBattleActionPacketsState packets = Fresh();
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Target, 240, 0, -1, 0, 187, null));
        (packets, _) = packets.Publish(0);
        Assert.Contains("repeat mode", Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.Normalize(packets, header)).Message);
    }

    private static NativeBattleActionPacketsState Prepare(byte command = 240, ushort action = 0, ushort effect = 187, byte sequence = 0, byte count = 0)
    {
        NativeBattleActionPacketsState packets = Fresh();
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(Source, command, sequence, -1, action, effect, null));
        if (count != 0)
        {
            (packets, _) = packets.AppendContact(Contact(Target));
        }
        (packets, _) = packets.Publish(count);
        return packets.WritePlayerRepeatMode(header, 1);
    }

    private static NativeBattleActionPacketsState Fresh(BattleModelRoster? roster = null)
    {
        if (roster is not null)
        {
            return NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), new(roster, [(NativeBattleModelSlot.Decode(0), Source)]));
        }
        roster = new([Source, Cover1, Cover2, Target, Second]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Source), (NativeBattleModelSlot.Decode(1), Cover1), (NativeBattleModelSlot.Decode(2), Cover2), (NativeBattleModelSlot.Decode(3), Target), (NativeBattleModelSlot.Decode(4), Second)]);
        return NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner target, byte flags = 0, byte hitFlags = 0, BattleModelOwner? cover = null)
    {
        return new(new(target, 0, flags, hitFlags, 0, 0, 0) { CoverSource = cover }, null);
    }
}
