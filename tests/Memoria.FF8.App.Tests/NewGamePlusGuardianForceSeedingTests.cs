using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusGuardianForceSeedingTests
{
    [Fact]
    public void StoredCharacterLevelUsesSourceCurveThenTheTargetExperienceThreshold()
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = CompletedSaveFixture.State();
        source = source with { CharacterVitals = source.CharacterVitals! with { Definitions = source.CharacterVitals!.Definitions with { Characters = source.CharacterVitals.Definitions.Characters.SetItem(0, source.CharacterVitals.Definitions.Characters[0] with { ExperienceStep = 4, ExperienceFraction = 0 }) } } };
        source.CharacterVitals!.Records[0] = source.CharacterVitals.Records[0] with { Experience = 40 };
        native = native with { CharacterVitals = native.CharacterVitals! with { Definitions = native.CharacterVitals!.Definitions with { Characters = native.CharacterVitals.Definitions.Characters.SetItem(0, native.CharacterVitals.Definitions.Characters[0] with { ExperienceStep = 1, ExperienceFraction = 0 }) } } };
        NewGamePlusDraft draft = new(source, native, new());
        Assert.Equal(2, draft.Characters.Single(character => character.CharacterId == 0).IndividualLevel);
        GameState started = draft.ApplyTo(native);
        Assert.Equal(10u, started.CharacterVitals!.Records[0].Experience);
        Assert.Equal(2, NativeStatDefinitions.Level(started.CharacterVitals.Records[0].Experience, native.CharacterVitals.Definitions.Characters[0]));
    }

    [Fact]
    public void StoredKeepsAcquiredProgressAndSeedsUnacquiredForcesFromPristineTarget()
    {
        GameState source = CompletedSaveFixture.State();
        GameState native = CompletedSaveFixture.State();
        source.CharacterVitals!.GuardianForces[0] = source.CharacterVitals.GuardianForces[0] with { Flags = 1, Experience = 9999 };
        source.CharacterVitals.GuardianForces[1] = source.CharacterVitals.GuardianForces[1] with { Flags = 0, Experience = 7777 };
        native.CharacterVitals!.GuardianForces[1] = native.CharacterVitals.GuardianForces[1] with { Flags = 0, Experience = 13 };
        NewGamePlusDraft draft = new(source, native, new());
        Assert.Equal(9999u, draft.GuardianForces.GuardianForces[0].Experience);
        Assert.Equal(13u, draft.GuardianForces.GuardianForces[1].Experience);
        draft.ToggleGuardianForce(1);
        Assert.Equal(13u, draft.GuardianForces.GuardianForces[1].Experience);
        Assert.Equal(7777u, source.CharacterVitals.GuardianForces[1].Experience);
    }
}
