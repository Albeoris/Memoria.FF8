using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldRequestPacketTests
{
    [Fact]
    public void UnknownPhysicalWordsRemainUnknownThroughPartialWritersAndSerialization()
    {
        NativeFieldRequestPacket unknown = new();
        NativeFieldRequestPacket next = unknown.WithScriptMenuRequest(8).WithWorldRequest(new(-7, -8, -9)).WithConsumedRequest().WithTimerExpiry();
        NativeFieldRequestPacket restored = JsonSerializer.Deserialize<NativeFieldRequestPacket>(JsonSerializer.Serialize(next))!;

        Assert.Equal(next, restored);
        Assert.Null(restored.X);
        Assert.Null(restored.Y);
        Assert.Null(restored.ExtraWord);
        Assert.Equal((ushort)65527, restored.DirectionWord);
        Assert.Throws<NotSupportedException>(() => restored.ToFieldTransition());
        Assert.Null(unknown.Kind);
        Assert.Null(unknown.FieldId);
        Assert.Null(unknown.X);
        Assert.Null(unknown.Y);
        Assert.Null(unknown.Triangle);
        Assert.Null(unknown.DirectionWord);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFieldRequestPacket>("{}"));
        Assert.Throws<InvalidDataException>(() => (unknown with { Version = 2 }).WithTimerExpiry());
    }

    [Fact]
    public void FieldRequestsPreserveRawDirectionWordWhileArrivalProjectsOnlyItsLowByte()
    {
        NativeFieldRequestPacket source = KnownPacket();
        FieldTransition request = new(601, short.MinValue, short.MaxValue, ushort.MaxValue, 0xDC);
        NativeFieldRequestPacket written = source.WithFieldRequest(request, 0xFEDC);

        Assert.Equal((ushort)0xFEDC, written.DirectionWord);
        Assert.Equal((short)-111, written.ExtraWord);
        Assert.Equal((byte)1, written.Kind);
        Assert.Equal(request with { NativeExtraWord = -111 }, written.ToFieldTransition());
        Assert.Equal(written, JsonSerializer.Deserialize<NativeFieldRequestPacket>(JsonSerializer.Serialize(written)));
        Assert.Equal((short)1005, written.WithFieldRequest(request with { NativeExtraWord = 1005 }, 0xFEDC).ExtraWord);
        Assert.Equal((ushort)0xABF3, source.DirectionWord);
    }

    [Fact]
    public void CoordinateSessionRequestsPublishRawWordsWithoutClearingUnwrittenPacketFields()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeFieldRequestPacket source = KnownPacket();
        session.SetFieldRequestPacket(source);
        FieldTransition arrival = new(601, -12, 34, 7, 0xDC);

        session.RequestField(arrival, 0xFEDC);

        Assert.Equal(source.WithFieldRequest(arrival, 0xFEDC), session.State.FieldRequestPacket);
        Assert.Equal((ushort)0xFEDC, session.State.FieldRequestPacket!.DirectionWord);
        Assert.Equal(arrival, session.State.FieldArrival);
        NativeFieldRequestPacket retained = session.State.FieldRequestPacket;
        session.RequestField(602, 0);
        Assert.Same(retained, session.State.FieldRequestPacket);
        Assert.Null(session.State.FieldArrival);
    }

    [Theory]
    [InlineData(-1, 0f, 0f, 0, 0, 0)]
    [InlineData(65536, 0f, 0f, 0, 0, 0)]
    [InlineData(600, -32769f, 0f, 0, 0, 0)]
    [InlineData(600, 0f, 32768f, 0, 0, 0)]
    [InlineData(600, 0.5f, 0f, 0, 0, 0)]
    [InlineData(600, 0f, -0.5f, 0, 0, 0)]
    [InlineData(600, float.NaN, 0f, 0, 0, 0)]
    [InlineData(600, 0f, float.PositiveInfinity, 0, 0, 0)]
    [InlineData(600, 0f, 0f, -1, 0, 0)]
    [InlineData(600, 0f, 0f, 65536, 0, 0)]
    [InlineData(600, 0f, 0f, 0, 1, 0)]
    public void FieldRequestRefusesValuesThatCannotBeItsExactNativeWords(int field, float x, float y, int triangle, int direction, ushort rawDirection)
    {
        NativeFieldRequestPacket source = KnownPacket();
        Assert.Throws<InvalidDataException>(() => source.WithFieldRequest(new(field, x, y, triangle, direction), rawDirection));
        Assert.Equal(KnownPacket(), source);
    }

    [Fact]
    public void MenuAndWorldRequestsWriteOnlyTheirActualPacketFields()
    {
        NativeFieldRequestPacket source = KnownPacket();
        NativeFieldRequestPacket menu = source.WithScriptMenuRequest(ushort.MaxValue);
        Assert.Equal(source with { Kind = 5, FieldId = ushort.MaxValue }, menu);
        NativeFieldRequestPacket world = menu.WithWorldRequest(new(-7, -8, -9));
        Assert.Equal(menu with { Kind = 7, FieldId = 65529, Triangle = 65528, DirectionWord = 65527 }, world);
        Assert.Equal(world with { Kind = 0 }, world.WithConsumedRequest());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 3)]
    [InlineData(5, 5)]
    [InlineData(8, 8)]
    public void BattleIssuePreservesPlacementUntilTheExecutedPostVmWriterPublishesItsNullableRead(int? kind, int? expectedKind)
    {
        byte? nativeKind = kind is int value ? checked((byte)value) : null;
        byte? expectedByte = expectedKind is int expected ? checked((byte)expected) : null;
        NativeFieldRequestPacket source = KnownPacket() with { Kind = nativeKind };
        NativeFieldRequestPacket issued = source.WithBattleRequest(42);
        Assert.Equal(source with { Kind = expectedByte, FieldId = 42 }, issued);
        NativeFieldRequestPacket unknownPlacement = issued.WithBattleRequest(42, null, null, null);
        Assert.Equal(issued with { X = null, Y = null, Triangle = null }, unknownPlacement);
        Assert.Equal(issued.DirectionWord, unknownPlacement.DirectionWord);
        Assert.Equal(issued.ExtraWord, unknownPlacement.ExtraWord);
        Assert.Equal(unknownPlacement with { X = -123, Y = 456, Triangle = 17 }, unknownPlacement.WithBattleRequest(42, -123, 456, 17));
    }

    [Fact]
    public void WorldReturnDoesNotReplaceRetainedRequestKindDestinationOrExtraWord()
    {
        NativeFieldRequestPacket source = KnownPacket() with { Kind = 7 };
        FieldTransition arrival = new(601, 32, -99, 7, 0xF3, 1234);

        Assert.Equal(source with { X = 32, Y = -99, Triangle = 7, DirectionWord = 0xF3 }, source.WithWorldReturn(arrival));
    }

    [Fact]
    public void TimerExpiryChangesOnlyItsThreeNativeWrittenFields()
    {
        NativeFieldRequestPacket source = KnownPacket();
        NativeFieldRequestPacket expired = source.WithTimerExpiry();
        Assert.Equal(source with { Kind = 1, FieldId = 75, Triangle = 0x7FFF }, expired);
        Assert.Equal(new FieldTransition(75, -321, 789, 0x7FFF, 0xF3, -111), expired.ToFieldTransition());
    }

    [Fact]
    public void ARealExpiredTimerCommitsItsPreservedRawPacketAndOrdinaryFieldRequestTogether()
    {
        NativeFieldRequestPacket packet = KnownPacket();
        GameSession session = ExpiredSession(packet);

        session.RequestTimerExpiredField(packet);

        Assert.Equal(packet.WithTimerExpiry(), session.State.FieldRequestPacket);
        Assert.Equal(packet.WithTimerExpiry().ToFieldTransition(), session.State.FieldArrival);
        Assert.Equal(75, session.State.FieldId);
        Assert.Equal(932, session.State.PreviousFieldId);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(NativeFieldLoadContext.FromFieldRequest(75), session.State.FieldLoadContext);
        Assert.Null(session.State.EventState);
        Assert.Equal(-321, session.State.X);
        Assert.Equal(789, session.State.Y);
    }

    [Theory]
    [InlineData("missing-x")]
    [InlineData("missing-y")]
    [InlineData("missing-direction")]
    public void ExpiryWithUnknownInheritedWordsRefusesWithoutChangingAnySessionState(string missing)
    {
        NativeFieldRequestPacket packet = missing switch
        {
            "missing-x" => KnownPacket() with { X = null },
            "missing-y" => KnownPacket() with { Y = null },
            _ => KnownPacket() with { DirectionWord = null }
        };
        GameSession session = ExpiredSession(packet);
        GameState before = session.State;

        Assert.Throws<NotSupportedException>(() => session.RequestTimerExpiredField(packet));
        Assert.Same(before, session.State);
    }

    [Theory]
    [InlineData("stale-packet")]
    [InlineData("nonzero-timer")]
    [InlineData("inactive-timer")]
    [InlineData("suppressed")]
    [InlineData("unknown-suppression")]
    [InlineData("display-off")]
    [InlineData("missing-controls")]
    public void ExpiryRequiresItsCurrentPacketIdentityAndAllActualTimerGuards(string invalid)
    {
        NativeFieldRequestPacket packet = KnownPacket();
        GameState state = ExpiredSession(packet).State;
        state = invalid switch
        {
            "nonzero-timer" => state with { ExamTimer = state.ExamTimer!.SetRemaining(1) },
            "inactive-timer" => state with { ExamTimer = state.ExamTimer! with { TimerMemoryActivated = false } },
            "suppressed" => state with { ExamTimer = state.ExamTimer! with { Event183Mask1 = true } },
            "unknown-suppression" => state with { ExamTimer = state.ExamTimer! with { Event183Mask1 = null } },
            "display-off" => state with { WorldControls = state.WorldControls! with { EventFlags104 = state.WorldControls!.EventFlags104 & ~0x40u } },
            "missing-controls" => state with { WorldControls = null },
            _ => state
        };
        GameSession session = new(state);

        Assert.Throws<InvalidOperationException>(() => session.RequestTimerExpiredField(invalid == "stale-packet" ? packet with { } : packet));
        Assert.Same(state, session.State);
    }

    [Fact]
    public void StandaloneRoundTripMatchesOnlyTheActiveVmAndPreservesADifferentDormantPacket()
    {
        NativeFieldRequestPacket current = KnownPacket();
        NativeFieldRequestPacket dormant = current.WithTimerExpiry();
        GameState state = new() { FieldRequestPacket = current, EventState = OwnerCheckpoint(current), DepartingFieldState = OwnerCheckpoint(dormant) };
        string directory = Path.Combine(Path.GetTempPath(), "ff8-field-request-packet-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "standalone.json");
        try
        {
            new GameSession(state).Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(current, restored.FieldRequestPacket);
            Assert.True(JsonElement.DeepEquals(state.EventState!.Value, restored.EventState!.Value));
            Assert.True(JsonElement.DeepEquals(state.DepartingFieldState!.Value, restored.DepartingFieldState!.Value));
            GameState mismatch = state with { EventState = OwnerCheckpoint(dormant) };
            new GameSession(mismatch).Save(path);
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static JsonElement OwnerCheckpoint(NativeFieldRequestPacket packet)
    {
        JsonObject owner = JsonSerializer.SerializeToNode(new GameState { FieldRequestPacket = packet })!.AsObject();
        // Runtime owner components carry canonical banks without claiming an executable field Scene.
        owner.Remove(nameof(GameState.Scene));
        owner.Remove(nameof(GameState.EventState));
        owner.Remove(nameof(GameState.DepartingFieldState));
        return JsonSerializer.SerializeToElement(owner);
    }

    private static NativeFieldRequestPacket KnownPacket()
    {
        return new() { Kind = 0, FieldId = 600, X = -321, Y = 789, ExtraWord = -111, Triangle = 42, DirectionWord = 0xABF3 };
    }

    private static GameSession ExpiredSession(NativeFieldRequestPacket packet)
    {
        GameSession cold = GameSession.CreateColdProcess();
        NativeExamTimerState timer = cold.ExamTimer!.SetRemaining(0).Display(2, 3) with { Event183Mask1 = false };
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
        return new(cold.State with { Scene = SceneKind.Field, FieldId = 932, FieldRequestPacket = packet, ExamTimer = timer, WorldControls = controls with { EventFlags104 = controls.EventFlags104 | 0x40u } });
    }
}
