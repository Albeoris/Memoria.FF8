using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRandomEncounterTests
{
    private static readonly byte[] OriginalRandom = File.ReadAllBytes(@"C:\Steam\steamapps\common\FINAL FANTASY VIII\FF8_EN.exe").AsSpan(0x780A18, 256).ToArray();
    private static readonly byte[] OriginalWeights = [37, 37, 37, 37, 36, 36, 24, 12];
    private static readonly NativeFieldEncounterDefinition Field = new(4, ImmutableArray.Create<ushort>(34, 34, 37, 36));
    private static readonly NativeWorldEncounterDefinition World = new(255, ImmutableArray.Create<ushort>(512, 513, 514, 515, 516, 517, 518, 519));

    [Fact]
    public void FieldChecksStrictlyAbove256AndPreservesTheLowByteRemainder()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess() with { FieldStep = 252 };
        NativeFieldEncounterInput input = new(true, 1348, 0, false, false, 0, 0);
        NativeRandomEncounterResult boundary = NativeRandomEncounter.AdvanceField(source, Field, OriginalRandom, input);
        Assert.Equal((ushort)256, boundary.State.FieldStep);
        Assert.Equal((byte)0, boundary.State.FieldIndex);
        NativeRandomEncounterResult checkedStep = NativeRandomEncounter.AdvanceField(boundary.State, Field, OriginalRandom, input);
        Assert.Equal((ushort)4, checkedStep.State.FieldStep);
        Assert.Equal((ushort)1, checkedStep.State.FieldDanger);
        Assert.Equal((byte)1, checkedStep.State.FieldIndex);
        Assert.Null(checkedStep.Formation);
    }

    [Fact]
    public void FieldUsesOriginalThresholdsAndSkipsPreviousFormationBeforeMovingToTheNextSlot()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess() with { FieldStep = 256, FieldDanger = 182, LastFieldFormation = 34 };
        NativeRandomEncounterResult result = NativeRandomEncounter.AdvanceField(source, Field, OriginalRandom, new(true, 1348, 0, false, false, 0, 0));
        Assert.Equal((ushort)37, result.Formation);
        Assert.Equal((ushort)0, result.State.FieldDanger);
        Assert.Equal((byte)1, result.State.FieldFormationIndex);
        Assert.Equal((ushort)37, result.State.LastFieldFormation);
    }

    [Fact]
    public void FieldRollWrapAdds13AndEncHalfHalvesTheRatByte()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess() with { FieldStep = 256, FieldDanger = 20, FieldIndex = 255 };
        NativeRandomEncounterResult result = NativeRandomEncounter.AdvanceField(source, Field, OriginalRandom, new(true, 1348, 4, false, false, 0, 0));
        Assert.Equal((ushort)2, result.State.FieldStep);
        Assert.Equal((byte)0, result.State.FieldIndex);
        Assert.Equal((byte)13, result.State.FieldOffset);
        Assert.Equal((ushort)34, result.Formation);
    }

    [Fact]
    public void WorldChecksEverySeventeenthMovingCallbackAndRerollsOneRepeatedFormation()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess().InitializeWorld(0) with { LastWorldFormation = 516 };
        NativeWorldEncounterInput input = new(8, 0, 6, 0, true, false);
        for (int callback = 0; callback < 16; callback++)
        {
            NativeWorldRandomEncounterResult step = NativeRandomEncounter.AdvanceWorld(source, World, OriginalRandom, OriginalWeights, input, 0, 0);
            Assert.Null(step.Formation);
            source = step.State;
        }
        Assert.Equal((ushort)256, source.WorldStep);
        NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(source, World, OriginalRandom, OriginalWeights, input, 0, 0);
        Assert.Equal((ushort)518, result.Formation);
        Assert.Equal((byte)2, result.State.WorldFormationIndex);
        Assert.Equal((byte)0, result.State.WorldDanger);
        Assert.Equal((ushort)0, result.State.WorldStep);
    }

    [Theory]
    [InlineData(32, 6, 8, 0)]
    [InlineData(33, 6, 8, 0)]
    [InlineData(50, 6, 8, 0)]
    [InlineData(0, 27, 8, 0)]
    [InlineData(0, 28, 8, 0)]
    [InlineData(0, 6, 0, 0)]
    [InlineData(0, 6, 8, 8)]
    public void WorldSuppressionPreservesAllCountersBeforeAnySeedIsRequired(int entity, byte ground, int movement, byte abilities)
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess();
        NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(source, World, OriginalRandom, OriginalWeights, new(movement, entity, ground, abilities, true, false), 0, 0);
        Assert.Equal(source, result.State);
        Assert.Null(result.Formation);
    }

    [Fact]
    public void WorldEncHalfUsesQuarterStepAndFailureRetainsByteDanger()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess().InitializeWorld(0) with { WorldStep = 256 };
        NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(source, World with { Rate = 0 }, OriginalRandom, OriginalWeights, new(24, 0, 6, 4, true, false), 0, 0);
        Assert.Null(result.Formation);
        Assert.Equal((byte)3, result.State.WorldDanger);
        Assert.Equal((ushort)0, result.State.WorldStep);
        NativeWorldRandomEncounterResult next = NativeRandomEncounter.AdvanceWorld(result.State, World, OriginalRandom, OriginalWeights, new(24, 0, 6, 4, true, false), 0, 0);
        Assert.Equal((ushort)4, next.State.WorldStep);
    }

    [Fact]
    public void WorldConsumesTheSharedForestCursorAfterEarlierNativeRandomConsumers()
    {
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0).DrawRandom(OriginalRandom, out byte earlierDraw);
        Assert.Equal((byte)182, earlierDraw);
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess().InitializeWorld(0) with { WorldStep = 256 };
        NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(source, World with { Rate = 239 }, OriginalRandom, OriginalWeights, new(8, 0, 6, 0, true, false), forest.RandomCursor, forest.RandomOffset);
        Assert.Null(result.Formation);
        Assert.Equal((byte)2, result.RandomCursor);
        Assert.Equal((byte)0, result.RandomOffset);
        Assert.Equal((byte)0, result.State.WorldFormationIndex);
    }

    [Fact]
    public void WorldRollWrapSubtractsTheUpdatedSharedOffsetBeforeTheStrictComparison()
    {
        NativeRandomEncounterState source = NativeRandomEncounterState.FromColdProcess().InitializeWorld(0) with { WorldStep = 256 };
        NativeWorldRandomEncounterResult result = NativeRandomEncounter.AdvanceWorld(source, World with { Rate = 249 }, OriginalRandom, OriginalWeights, new(8, 0, 6, 0, true, false), 255, 0);
        Assert.Null(result.Formation);
        Assert.Equal((byte)0, result.RandomCursor);
        Assert.Equal((byte)13, result.RandomOffset);
        Assert.Equal((byte)1, result.State.WorldDanger);
    }
}



