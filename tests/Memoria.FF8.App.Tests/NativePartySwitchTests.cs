using System.Text.Json;
using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePartySwitchTests
{
    [Fact]
    public void LiveSwitchCarriesBothSelectedRowsAndRefreshesMembership()
    {
        NativeCharacterVitals party = CreateParty();
        string original = JsonSerializer.Serialize(party);
        NativeCharacterVitals source = party;
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party, WorldControls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 } }), action =>
        {
            PlayerAction input = NativePartySwitchMenuProjection.PlayerAction(action);
            inputs.Add(input);
            PlayerAction restoredInput = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(input))!;
            Assert.Equal(input, restoredInput);
            party = NativePartySwitchInput.Apply(party, restoredInput, 2);
        });
        controller.Open("switch-party");
        controller.SelectEntry("switch-party", "character-1");
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-party-target", "character-3");
        controller.Navigate(MenuInput.Confirm);

        Assert.Equal(new PlayerAction(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3), Assert.Single(inputs));
        Assert.Equal(new byte[] { 0, 3, 2 }, party.CombatParty);
        Assert.Equal("switch-party", controller.ScreenId);
        Assert.DoesNotContain("Unsupported", controller.Message);
        Assert.True(controller.Data.Characters.Single(character => character.Id == "character-3").InParty);
        Assert.False(controller.Data.Characters.Single(character => character.Id == "character-1").InParty);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(source.Records.Select(record => record.Junction), party.Records.Select(record => record.Junction));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(party))!;
        restored.Validate();
        Assert.Equal(party.CombatParty, restored.CombatParty);
    }

    [Fact]
    public void NativeLocksPreventCrossPaneReplacementButPermitPartyReordering()
    {
        NativeCharacterVitals source = CreateParty(locked: 0);
        Assert.False(NativePartySwitchInput.CanExchange(source, 0, 3, 2));
        Assert.Throws<InvalidOperationException>(() => NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 3), 2));
        NativeCharacterVitals result = NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 2), 2);
        Assert.Equal(new byte[] { 2, 1, 0 }, result.CombatParty);
        Assert.Equal(source.Records[0].MemberFlags, result.Records[0].MemberFlags);
    }

    [Fact]
    public void FieldPublicationPreservesForcedFieldCellsAndUsesSelectedMenuOrder()
    {
        NativeCharacterVitals source = CreateParty(forced: 0);
        NativeCharacterVitals result = NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3), 2);
        Assert.Equal(new byte[] { 3, 0, 2 }, result.PreparePartyMenuFieldSlots([1, 0, 2], result.CombatParty));
    }

    [Fact]
    public void MissingEntryPermissionDisablesMemberTabAndTargets()
    {
        NativeCharacterVitals source = CreateParty();
        MenuData data = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = source, WorldControls = NativeWorldControlState.CreateNewGame() });
        MenuController controller = new(() => data);
        controller.Open("switch");
        Assert.False(controller.Entries.Single(entry => entry.Id == "switch-party").Enabled);
        Assert.DoesNotContain("party-switch", data.SupportedActions);
        Assert.False(NativePartySwitchInput.CanExchange(source, 1, 3, 0));
        Assert.False(NativePartySwitchInput.CanExchange(source, 1, 7, 2));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 8)]
    public void MalformedPhysicalRowsFailBeforeMutation(int source, int target)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session);
        GameState before = session.State;
        Assert.Throws<ArgumentException>(() => events.Apply(new PlayerAction(PlayerActionKind.SwitchPartyMembers, source, Quantity: target)));
        Assert.Same(before, session.State);
        Assert.Empty(events.ActionLog);
    }

    [Fact]
    public void IncompleteAndEntirelyIncapacitatedPartiesCannotBePublished()
    {
        NativeCharacterVitals source = CreateParty();
        Assert.False(NativePartySwitchInput.CanExchange(source with { CombatParty = [0, 1, 255] }, 1, 3, 2));
        NativeCharacterVital[] records = source.Records.Select(record => record with { Status = (ushort)(record.Status | 1) }).ToArray();
        Assert.False(NativePartySwitchInput.CanExchange(source with { Records = records }, 1, 3, 2));
    }

    [Fact]
    public void MalformedTransportRejectsBeforeOpeningAnActionJournal()
    {
        PlayerAction valid = new(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 1);
        PlayerAction[] malformed = [valid with { Quantity = null }, valid with { Text = "1" }, valid with { Frames = 2 }, valid with { Horizontal = 1 }, valid with { Vertical = 1 }, valid with { CameraHorizontal = 1 }, valid with { WorldButtons = 1 }, valid with { WaitWhilePlayable = true }];
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session);
        GameState before = session.State;
        foreach (PlayerAction input in malformed)
        {
            Assert.Throws<ArgumentException>(() => events.Apply(input));
            Assert.Same(before, session.State);
            Assert.Empty(events.ActionLog);
        }
    }

    [Fact]
    public void NativeNormalSwitchPublishesItsResultAndRestoresBeforeReturn()
    {
        NativeCharacterVitals vitals = CreateParty();
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(vitals);
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetNativeEventOwners(session.WorldControls! with { Menu209 = 2 });
        EventEngine engine = new(session);
        byte[] program = NormalMenuProgram();
        engine.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        session.SetNativeEventOwners(session.WorldControls! with { Menu209 = 2 });
        engine.Scene.Party.Clear();
        engine.Scene.Party.AddRange([0, 1, 2]);
        for (int callback = 0; engine.PartyMenu?.Phase != NativePartyMenuPhase.Active; callback++)
        {
            Assert.True(callback < 8, engine.LastFault?.ToString());
            engine.Step();
        }
        PlayerAction action = new(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3);
        PlayerActionResult result = engine.Apply(JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(action))!);
        Assert.True(result.Accepted, result.Detail ?? $"menu entry {engine.PartyMenu?.NativeEntrySelection}; party {string.Join(",", session.CharacterVitals!.CombatParty)}");
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal((ushort)1, engine.PartyMenu!.NativeReturnResult);
        Assert.Equal(new byte[] { 0, 3, 2 }, session.CharacterVitals!.CombatParty);
        Assert.Equal(new[] { 0, 3, 2 }, engine.Scene.Party);
        Assert.Equal(session.CharacterVitals.CombatParty, engine.PartyMenu.TemporaryCombatParty);
        JsonElement checkpoint = engine.Capture();
        GameSession resumedSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(checkpoint);
        Assert.True(JsonElement.DeepEquals(checkpoint, resumed.Capture()));
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.Equal(NativePartyMenuPhase.Closing, engine.PartyMenu!.Phase);
        Assert.Equal((ushort)1, engine.PartyMenu.NativeReturnResult);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        for (int callback = 0; engine.PartyMenu!.Phase != NativePartyMenuPhase.FieldResumeInitialization; callback++)
        {
            Assert.True(callback < 5);
            Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        }
        Assert.Equal(new[] { 255, 255, 255 }, engine.Scene.FieldPartyNativeActorIds);
        Assert.NotNull(engine.Scene.PartyBindingMemory);
        Assert.Equal(new[] { 0, 3, 2 }, engine.Scene.Party);
        Assert.Null(engine.LastFault);
    }

    private static byte[] NormalMenuProgram()
    {
        // Declared command component with authentic installed menu owners; this is not a gameplay save.
        (Jsm.Opcode Opcode, int Argument)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int argument) = operations[index];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), argument == 0 ? (int)opcode : (int)opcode << 24 | argument);
        }
        return bytes;
    }

    private static NativeCharacterVitals CreateParty(int? locked = null, int? forced = null)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed character and kernel resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = source.Records.Select((record, row) => record with { CharacterId = (byte)row, MemberFlags = (ushort)(row < 4 ? 1 | (locked == row ? 2 : 0) | (forced == row ? 4 : 0) : 0) }).ToArray();
        return (source with { Records = records, CombatParty = [0, 1, 2], DreamEnabled = false }).RebuildParty();
    }
}
