using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CharacterMappingAssociationTests
{
    [Fact]
    public void EverySelectablePairCanBeAssociatedWithoutChangingOtherRetainedAssociations()
    {
        CharacterAppearanceMapping[] original = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion)];
        for (int story = 0; story < 9; story++)
        {
            for (int appearance = 0; appearance < 11; appearance++)
            {
                CharacterAppearanceMapping[] assigned = CharacterMappingAssociations.Assign(original, story, appearance);
                Assert.Contains(assigned, mapping => mapping.StoryCharacter == story && mapping.AppearanceCharacter == appearance);
                if (story != 0)
                {
                    Assert.Same(original[0], assigned.Single(mapping => mapping.StoryCharacter == 0));
                }
                Assert.Equal(Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion, original[0].RigProfile);
            }
        }
    }

    [Fact]
    public void SharedAppearanceDoesNotSwapNativeCharactersOrRemoveAnotherAssociation()
    {
        CharacterAppearanceMapping[] first = CharacterMappingAssociations.Assign([], 0, 3);
        CharacterAppearanceMapping[] second = CharacterMappingAssociations.Assign(first, 1, 3);
        Assert.Equal(2, second.Length);
        Assert.Equal(new[] { 0, 1 }, second.Select(mapping => mapping.StoryCharacter));
        Assert.All(second, mapping => Assert.Equal(3, mapping.AppearanceCharacter));
    }

    [Fact]
    public void UnknownAndReversedProfilesRemainExplicitlyUnsupported()
    {
        Assert.Throws<NotSupportedException>(() => CharacterMappingAssociations.Validate([new(SessionGame.Ff8, 0, SessionGame.Ff7, 0, Ff7ModelAppearance.GeneralFieldProfileVersion)]));
        Assert.Throws<NotSupportedException>(() => CharacterMappingAssociations.Validate([new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "unknown")]));
        var mapping = new CharacterAppearanceMapping(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.GeneralFieldProfileVersion);
        Assert.Throws<NotSupportedException>(() => CharacterMappingAssociations.Validate([mapping, mapping]));
    }
}
