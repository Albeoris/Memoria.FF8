using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GuardianForceDefinitionValidationTests
{
    [Fact]
    public void Validated_definitions_reject_corrupted_record_copies_and_replacement_collections()
    {
        NativeGuardianForceMenuDefinitions definitions = CreateDefinitions();
        definitions.Validate();

        NativeGuardianForceMenuDefinitions[] invalid =
        [
            definitions with { Learning = definitions.Learning.SetItem(0, definitions.Learning[0].SetItem(0, new(0, 255, 128))) },
            definitions with { AbilityPoints = [] },
            definitions with { CommandAbilityCommands = [] },
            definitions with { PartyAbilityFlags = [] },
            definitions with { Templates = definitions.Templates.SetItem(0, [definitions.Templates[0][0] with { RequiredPoints = -1 }]) },
            definitions with { InitialStates = definitions.InitialStates.SetItem(0, definitions.InitialStates[0] with { LearnedBank = null! }) },
            definitions with { InitialStates = definitions.InitialStates.SetItem(0, definitions.InitialStates[0] with { AbilityProgress = [] }) }
        ];
        foreach (NativeGuardianForceMenuDefinitions copy in invalid)
        {
            Assert.Throws<InvalidDataException>(copy.Validate);
            Assert.Throws<InvalidDataException>(copy.Validate);
        }
        (definitions with { }).Validate();
        definitions.Validate();
    }

    [Fact]
    public void Source_template_validation_tracks_both_source_and_definition_identities()
    {
        NativeGuardianForceMenuDefinitions definitions = CreateDefinitions();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = definitions.Templates.SetItem(0, [new(101, false, 20)]);
        definitions.ValidateTemplates(source);
        definitions.ValidateTemplates(source);

        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> invalidSource = source.SetItem(0, [source[0][0] with { RequiredPoints = -1 }]);
        Assert.Throws<InvalidDataException>(() => definitions.ValidateTemplates(invalidSource));
        Assert.Throws<InvalidDataException>(() => definitions.ValidateTemplates(invalidSource));
        NativeGuardianForceMenuDefinitions invalidDefinitions = definitions with { AbilityPoints = [] };
        Assert.Throws<InvalidDataException>(() => invalidDefinitions.ValidateTemplates(source));

        definitions.ValidateTemplates(source.SetItem(0, [source[0][0] with { RequiredPoints = 30 }]));
        definitions.ValidateTemplates(source);
    }

    [Fact]
    public void Warm_validation_reuses_definitions_and_source_without_allocating_projections()
    {
        NativeGuardianForceMenuDefinitions definitions = CreateDefinitions();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = definitions.Templates.SetItem(0, [new(101, false, 20)]);
        for (int index = 0; index < 10; index++)
        {
            definitions.Validate();
            definitions.ValidateTemplates(source);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            definitions.Validate();
            definitions.ValidateTemplates(source);
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Validated_definition_cache_does_not_hide_mutable_vital_changes()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Validate();
        NativeCharacterVitals copy = vitals.DeepCopy();
        copy.GuardianForces[0] = copy.GuardianForces[0] with { AbilityProgress = [] };

        Assert.Throws<InvalidDataException>(copy.Validate);
        vitals.Validate();
    }

    private static NativeGuardianForceMenuDefinitions CreateDefinitions()
    {
        return new(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5]))
        {
            Templates = Enumerable.Range(0, 16).Select(_ => ImmutableArray.Create(new GuardianForceAbilityTemplate(100, false, 20))).ToImmutableArray(),
            InitialStates = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceInitialState(new(0, 0), ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToImmutableArray()
        };
    }
}
