using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldSaveCompatibilityTests
{
    [Fact]
    public void InitializationKeepsCombatOrderHolesAndMappedGuestIdentities()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { CombatParty = [4, 255, 0] };
        vitals.Records[4] = vitals.Records[4] with { CharacterId = 9 };
        vitals.Records[0] = vitals.Records[0] with { CharacterId = 8 };
        Assert.Equal(new byte[] { 9, 255, 8 }, NativeWorldSaveCompatibility.InitialParty(new GameState { CharacterVitals = vitals }));
    }

    [Fact]
    public void OriginalPartyTakesPrecedenceAndReturnsDetachedBytes()
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(NativePcSaveData.SaveMapOffset + 2), 0x08FF);
        new byte[] { 4, 255, 0 }.CopyTo(block, NativePcSaveData.MainOffset + 3304);
        GameState state = new() { OriginalSave = new(block) };
        byte[] party = NativeWorldSaveCompatibility.InitialParty(state);
        party[0] = 1;
        Assert.Equal(new byte[] { 4, 255, 0 }, NativeWorldSaveCompatibility.InitialParty(state));
    }

    [Fact]
    public void MissingMalformedAndAmbiguousCharacterOwnersRefuse()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldSaveCompatibility.InitialParty(new GameState()));
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        Assert.Throws<InvalidDataException>(() => NativeWorldSaveCompatibility.InitialParty(new GameState { CharacterVitals = vitals with { CombatParty = [8, 255, 255] } }));
        Assert.Throws<InvalidDataException>(() => NativeWorldSaveCompatibility.InitialParty(new GameState { CharacterVitals = vitals with { CombatParty = [0, 0, 255] } }));
        vitals.Records[1] = vitals.Records[1] with { CharacterId = vitals.Records[0].CharacterId };
        Assert.Throws<InvalidDataException>(() => NativeWorldSaveCompatibility.InitialParty(new GameState { CharacterVitals = vitals with { CombatParty = [0, 1, 255] } }));
    }

    [Theory]
    [InlineData(4, 0, 0, true)]
    [InlineData(0, 4, 0, false)]
    [InlineData(0, 0, 4, false)]
    public void MovementPrefersAuthenticFieldOwnersOverCompatibilityParty(int departing, int current, int world, bool learned)
    {
        GameState state = MovementState() with { DepartingFieldState = Snapshot(departing), EventState = Snapshot(current), WorldEventState = Snapshot(world) };
        Assert.Equal(learned, NativeWorldSalaryState.Begin(state).PrepareMovementProgress(0).LearnedAngelo);
        state = state with { DepartingFieldState = null };
        Assert.Equal(current == 4, NativeWorldSalaryState.Begin(state).PrepareMovementProgress(0).LearnedAngelo);
        state = state with { EventState = null };
        Assert.Equal(world == 4, NativeWorldSalaryState.Begin(state).PrepareMovementProgress(0).LearnedAngelo);
    }

    [Fact]
    public void WorldProcessSetterClonesJsonAndCanonicalFieldSupersedesIt()
    {
        GameSession session = new(new GameState());
        string json = JsonSerializer.Serialize(new { Version = 1, FieldId = 642, Scene = new { Party = new int[] { 4, 255, 0 } }, EventMemory = NativeEventMemory.Empty });
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            session.SetWorldEventState(document.RootElement);
        }
        Assert.Equal(4, session.State.WorldEventState!.Value.GetProperty("Scene").GetProperty("Party")[0].GetInt32());
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetWorldEventState(JsonSerializer.SerializeToElement(new { Version = 2, FieldId = 642, Scene = new { } })));
        Assert.Same(before, session.State);
        session.SetEventState(JsonSerializer.SerializeToElement(new { Version = 1 }));
        Assert.Null(session.State.WorldEventState);
    }

    private static JsonElement Snapshot(int identity) => JsonSerializer.SerializeToElement(new { Scene = new { Party = new int[] { identity, 255, 255 } } });

    [Fact]
    public void WorldProcessRegistersSceneOwnedMemoryAndRejectsRootAliasesAtomically()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(268, 1, NativeScalarValue.Exact(7));
        GameSession session = new(new GameState { EventMemory = memory });
        JsonElement world = JsonSerializer.SerializeToElement(new { Version = 1, FieldId = 642, Scene = new { WorldMusicSelection = new { Music = 7 } }, EventMemory = NativeEventMemory.Empty });
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetWorldEventState(world));
        Assert.Same(before, session.State);
    }

    private static GameState MovementState() => new()
    {
        CharacterVitals = MagicSystemFixture.Create(),
        LimitProgress = new(new string('0', 64), 0, 0, 0, 0, ImmutableArray.CreateRange(new byte[8])),
        SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0),
        EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [4] = 0, [12] = 0, [244] = 592 })
    };
}
