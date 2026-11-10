using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7CharacterPresentationIdentityTests
{
    [Fact]
    public void EveryAssociationUsesItsSelectedCommonNameAndPortraitIdentity()
    {
        string[] native = ["Cloud", "Barret", "Tifa", "Aeris", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid"];
        for (int story = 0; story < 9; story++)
        {
            for (int appearance = 0; appearance < 11; appearance++)
            {
                Ff7CharacterPresentationIdentity adapter = new([Mapping(story, appearance)]);
                Ff7CharacterDisplayIdentity identity = adapter.Resolve(story, native[story]);
                Assert.Equal(GameNames.DefaultName(GameNames.CharacterSubject(appearance)), identity.Name);
                Assert.Equal(identity.Name, identity.DefaultName);
                Assert.Equal(SessionGame.Ff8, identity.PortraitGame);
                Assert.Equal(appearance, identity.PortraitCharacter);
                Assert.True(identity.PresentationOwned);
            }
        }
    }

    [Fact]
    public void NativeCustomNameRemainsIntentionalAndMappingDoesNotRewriteItsIdentity()
    {
        Ff7CharacterPresentationIdentity adapter = new([Mapping(0, 0)]);
        Ff7CharacterDisplayIdentity customized = adapter.Resolve(0, "Neo");
        Assert.Equal("Neo", customized.Name);
        Assert.Equal("Squall", customized.DefaultName);
        Assert.Equal(SessionGame.Ff8, customized.PortraitGame);
        Assert.Equal(0, customized.PortraitCharacter);
        Ff7CharacterDisplayIdentity unmapped = adapter.Resolve(1, "Big B");
        Assert.Equal("Big B", unmapped.Name);
        Assert.Equal(SessionGame.Ff7, unmapped.PortraitGame);
        Assert.Equal(1, unmapped.PortraitCharacter);
        Assert.False(unmapped.PresentationOwned);
    }

    [Fact]
    public void SharedFF8NameOverridesAreSubjectsAndDoNotAliasNativeFF7CharacterNumbers()
    {
        Ff7CharacterPresentationIdentity adapter = new([Mapping(0, 4)], ff8Names: new Dictionary<int, string> { [GameNames.CharacterSubject(4)] = "Julia", [0] = "Leon" });
        Assert.Equal("Julia", adapter.Resolve(0, "Cloud").Name);
        Assert.Equal(4, adapter.Resolve(0, "Cloud").PortraitCharacter);
        Ff7CharacterPresentationIdentity squall = new([Mapping(0, 0)], ff8Names: new Dictionary<int, string> { [0] = "Leon" });
        Assert.Equal("Leon", squall.Resolve(0, "Cloud").Name);
    }

    [Fact]
    public void UntouchedMappedDefaultRemainsDynamicButExplicitOldDefaultPersistsThroughCodec()
    {
        CombinedSessionSave original = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = [1, 2, 3], Ff8PayloadFormat = "opaque-fixture", Mappings = [Mapping(0, 0)] };
        Assert.Same(original, Ff7CharacterPresentationIdentity.AcceptPresentationName(original, 0, "Squall", edited: false));
        Assert.Null(original.CharacterNameOverrides);
        CombinedSessionSave renamed = Ff7CharacterPresentationIdentity.AcceptPresentationName(original, 0, "Cloud", edited: true);
        Assert.Null(original.CharacterNameOverrides);
        using MemoryStream bytes = new();
        CombinedSessionCodec.Write(bytes, renamed);
        bytes.Position = 0;
        CombinedSessionSave cold = CombinedSessionCodec.Read(bytes);
        Assert.Equal(original.Ff8Payload, cold.Ff8Payload);
        Assert.Equal("Cloud", cold.CharacterNameOverrides![0]);
        Ff7CharacterPresentationIdentity changedMapping = new([Mapping(0, 4)], cold.CharacterNameOverrides);
        Assert.Equal("Cloud", changedMapping.Resolve(0, "Cloud").Name);
        Assert.Equal("Rinoa", changedMapping.Resolve(0, "Cloud").DefaultName);
        Ff7CharacterPresentationIdentity dynamic = new([Mapping(0, 4)]);
        Assert.Equal("Rinoa", dynamic.Resolve(0, "Cloud").Name);
        CombinedSessionSave legacy = JsonSerializer.Deserialize<CombinedSessionSave>(JsonSerializer.Serialize(original))!;
        Assert.Null(legacy.CharacterNameOverrides);
        legacy.Validate();
    }

    [Fact]
    public void DialogueCharacterAndPartyExpansionsUseTheSameProjectedNameWithoutProgressMutation()
    {
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new(0, 2)], Materia = [], Gil = 240 };
        string before = JsonSerializer.Serialize(progress);
        Ff7CharacterPresentationIdentity adapter = new([Mapping(0, 0)], ff8Names: new Dictionary<int, string> { [0] = "LongerThanNineName" });
        string Resolve(int character, string native) => adapter.Resolve(character, native).Name;
        FieldTextToken named = new(FieldTextTokenKind.CharacterName, 0, 0, 0, 0, "");
        FieldTextToken party = named with { Kind = FieldTextTokenKind.PartyName };
        Assert.Equal("Cloud", Ff7DialogueCursor.ExpandToken(named, progress, [0, 255, 255]));
        Assert.Equal("LongerThanNineName", Ff7DialogueCursor.ExpandToken(named, progress, [0, 255, 255], Resolve));
        Assert.Equal("LongerThanNineName", Ff7DialogueCursor.ExpandToken(party, progress, [0, 255, 255], Resolve));
        Assert.Equal(before, JsonSerializer.Serialize(progress));
    }

    [Fact]
    public void NativeRevealBudgetCountsTheSameMappedNameThatPresentationDisplays()
    {
        FieldTextToken token = new(FieldTextTokenKind.CharacterName, 0, 0, 0, 0, "");
        int RevealTicks(string name)
        {
            Ff7Dialogue dialogue = new(42, 0, 7, null, null, 0, Presentation: new(2, 304, 72));
            int ticks = 0;
            while (!dialogue.AwaitingInput && ticks < 64)
            {
                dialogue = Ff7DialogueCursor.AdvanceTick(dialogue, [token], expandToken: _ => name);
                ticks++;
                string saved = JsonSerializer.Serialize(dialogue);
                Ff7Dialogue cold = JsonSerializer.Deserialize<Ff7Dialogue>(saved)!;
                cold.Presentation!.Validate();
                Assert.Equal(dialogue, cold);
            }
            Assert.True(dialogue.AwaitingInput);
            Assert.Equal(42, dialogue.RequestId);
            Assert.Equal(7, dialogue.Text);
            Assert.Equal(1, dialogue.TokenPosition);
            return ticks;
        }
        Assert.True(RevealTicks("LongerThanNineName") > RevealTicks("Cloud"));
        new Ff7DialoguePresentation(2, 304, 72, ExpansionOffset: 9).Validate();
        Assert.Throws<InvalidDataException>(() => new Ff7DialoguePresentation(2, 304, 72, ExpansionOffset: 10).Validate());
    }

    [Fact]
    public void ActualRetainedFF8PayloadSuppliesSelectedCustomNameWithoutChangingItsBytes()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Selected FFVIII resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, world: true);
        GameplaySaveProgression progression = document.Progression!;
        GameplaySaveSceneFacts facts = progression.SceneFacts!;
        Dictionary<int, string> names = facts.Names.ToDictionary();
        names[GameNames.CharacterSubject(0)] = "Leon";
        document = document with { Progression = progression with { SceneFacts = facts with { Names = names } } };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        byte[] original = payload.ToArray();
        CombinedSessionSave saved = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = original, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, Mappings = [Mapping(0, 0)] };
        Assert.Equal("Leon", Ff7CharacterPresentationIdentity.FromSave(saved).Resolve(0, "Cloud").Name);
        Assert.Equal(original, saved.Ff8Payload);
    }

    [Theory]
    [InlineData(-1, "Name")]
    [InlineData(9, "Name")]
    [InlineData(0, "")]
    [InlineData(0, "Bad\nName")]
    [InlineData(0, " Name")]
    [InlineData(0, "123456789012345678901")]
    public void CorruptPresentationNameMetadataIsRejectedWithoutNewRequiredSaveFields(int character, string name)
    {
        CombinedSessionSave saved = new() { CharacterNameOverrides = new() { [character] = name } };
        Assert.Throws<InvalidDataException>(saved.ValidateCharacterNameOverrides);
    }

    private static CharacterAppearanceMapping Mapping(int story, int appearance) => new(SessionGame.Ff7, story, SessionGame.Ff8, appearance, Ff7ModelAppearance.GeneralFieldProfileVersion);
}
