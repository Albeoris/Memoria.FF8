using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CharacterMappingAssociationTests
{
    [Fact]
    public void EverySelectablePairRetainsOnlyUnrelatedAssociations()
    {
        CharacterAppearanceMapping[] original = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion)];
        for (int story = 0; story < 9; story++)
        {
            for (int appearance = 0; appearance < 11; appearance++)
            {
                CharacterAppearanceMapping[] assigned = CharacterMappingAssociations.Assign(original, story, appearance);
                Assert.Contains(assigned, mapping => mapping.StoryCharacter == story && mapping.AppearanceCharacter == appearance);
                if (story != 0 && appearance != 0)
                {
                    Assert.Same(original[0], assigned.Single(mapping => mapping.StoryCharacter == 0));
                }
                else
                {
                    Assert.Single(assigned);
                }
                Assert.Equal(Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion, original[0].RigProfile);
                Assert.Equal(assigned.Length, assigned.Select(mapping => mapping.StoryCharacter).Distinct().Count());
                Assert.Equal(assigned.Length, assigned.Select(mapping => mapping.AppearanceCharacter).Distinct().Count());
            }
        }
    }

    [Fact]
    public void OccupiedAppearanceMovesToNewStoryWithoutSwappingPreviousAppearance()
    {
        CharacterAppearanceMapping[] first = CharacterMappingAssociations.Assign([], 0, 3);
        CharacterAppearanceMapping[] second = CharacterMappingAssociations.Assign(first, 1, 4);
        CharacterAppearanceMapping[] third = CharacterMappingAssociations.Assign(second, 2, 5);

        CharacterAppearanceMapping[] assigned = CharacterMappingAssociations.Assign(third, 1, 3);

        Assert.Equal(new[] { 1, 2 }, assigned.Select(mapping => mapping.StoryCharacter));
        Assert.Equal(new[] { 3, 5 }, assigned.Select(mapping => mapping.AppearanceCharacter));
        Assert.Same(third[2], assigned[1]);
        Assert.Equal(new[] { 3, 4, 5 }, third.Select(mapping => mapping.AppearanceCharacter));
    }

    [Fact]
    public void OldSharedAppearancesRemainValidAndNormalizeToLastRecordedOwner()
    {
        CharacterAppearanceMapping[] original =
        [
            new(SessionGame.Ff7, 2, SessionGame.Ff8, 0, Ff7ModelAppearance.GeneralFieldProfileVersion),
            new(SessionGame.Ff7, 1, SessionGame.Ff8, 3, Ff7ModelAppearance.GeneralFieldProfileVersion),
            new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion)
        ];

        CharacterMappingAssociations.Validate(original);
        CharacterAppearanceMapping[] normalized = CharacterMappingAssociations.Normalize(original);

        Assert.Equal(new[] { 0, 1 }, normalized.Select(mapping => mapping.StoryCharacter));
        Assert.Same(original[2], normalized[0]);
        Assert.Same(original[1], normalized[1]);
        Assert.Equal(3, original.Length);
        Assert.Equal(normalized, CharacterMappingAssociations.Normalize(normalized));
    }

    [Fact]
    public void AssignmentAlsoNormalizesUnrelatedOldSharedAppearances()
    {
        CharacterAppearanceMapping[] original =
        [
            new(SessionGame.Ff7, 0, SessionGame.Ff8, 3, Ff7ModelAppearance.GeneralFieldProfileVersion),
            new(SessionGame.Ff7, 1, SessionGame.Ff8, 3, Ff7ModelAppearance.GeneralFieldProfileVersion)
        ];

        CharacterAppearanceMapping[] assigned = CharacterMappingAssociations.Assign(original, 2, 4);

        Assert.Equal(new[] { 1, 2 }, assigned.Select(mapping => mapping.StoryCharacter));
        Assert.Equal(new[] { 3, 4 }, assigned.Select(mapping => mapping.AppearanceCharacter));
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
