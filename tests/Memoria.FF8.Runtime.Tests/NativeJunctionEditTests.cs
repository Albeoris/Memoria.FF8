using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeJunctionEditTests
{
    private static NativeCharacterVitals Source(bool shared = false)
    {
        NativeCharacterVitals source = MagicSystemFixture.Stock(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), 0, 2, 100);
        source = MagicSystemFixture.Modes(source, shared: shared, infinite: true);
        source = source.SetJunctionMagic(0, 0, 1);
        source.Records[0] = source.Records[0] with { HitPoints = new NativeHpWord(90, 90) };
        source.Records[0].Junction.MagicJunctions[19] = 56;
        return source;
    }

    [Fact]
    public void FinalMappingClampsOnlyOnceAndPreviewOwnsItsArrays()
    {
        NativeCharacterVitals source = Source();
        string original = JsonSerializer.Serialize(source);
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[0].Junction.MagicJunctions[0] = 2;
        intended.Records[0].Junction.MagicJunctions[1] = 1;
        intended.Records[0] = intended.Records[0] with { HitPoints = new NativeHpWord(0, 0) };
        NativeJunctionEditPlan plan = NativeJunctionEdits.Create(source, intended);
        NativeCharacterVitals next = NativeJunctionEdits.Apply(source, plan);
        Assert.Equal(new NativeHpWord(90, 90), next.Records[0].HitPoints);
        Assert.Equal((byte)56, next.Records[0].Junction.MagicJunctions[19]);
        Assert.Equal(JsonSerializer.Serialize(source.MagicSystem), JsonSerializer.Serialize(next.MagicSystem));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        next.Records[0].Junction.MagicJunctions[0] = 0;
        plan.Rows[0].Junction.MagicJunctions[0] = 0;
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Theory]
    [InlineData("hp")]
    [InlineData("cast")]
    [InlineData("stock")]
    [InlineData("rules")]
    [InlineData("owner")]
    public void ConflictingChangeRejectsApplyAndUndoWithoutMutation(string change)
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[0].Junction.MagicJunctions[1] = 2;
        NativeJunctionEditPlan plan = NativeJunctionEdits.Create(source, intended);
        NativeCharacterVitals applied = NativeJunctionEdits.Apply(source, plan);
        NativeJunctionEditPlan undo = NativeJunctionEdits.CreateUndo(source, applied);
        NativeCharacterVitals changed = applied.DeepCopy();
        switch (change)
        {
            case "hp":
                changed.Records[0] = changed.Records[0] with { HitPoints = new NativeHpWord(89, 89) };
                break;
            case "cast":
                changed.MagicSystem.PersonalFieldMasks[0][1] = 1;
                break;
            case "stock":
                changed.Records[0].MagicInventory[0] = new NativeMagicStock(1, 99);
                break;
            case "rules":
                changed = changed with { MagicSystem = changed.MagicSystem with { SmartAttack = true } };
                break;
            case "owner":
                changed.Records[1] = changed.Records[1] with { Junction = changed.Records[1].Junction with { GuardianForces = 0 } };
                break;
        }
        string original = JsonSerializer.Serialize(changed);
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(changed, plan));
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(changed, undo));
        Assert.Equal(original, JsonSerializer.Serialize(changed));
    }

    [Fact]
    public void UndoRestoresOnlyAssignmentsWithoutHealingOrRefillingSpentCasts()
    {
        NativeCharacterVitals source = Source();
        source.MagicSystem.PersonalFieldMasks[0][1] = 15;
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[0].Junction.MagicJunctions[0] = 0;
        intended.Records[0].Junction.MagicJunctions[1] = 1;
        NativeCharacterVitals applied = NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended));
        Assert.Equal(new NativeHpWord(10, 10), applied.Records[0].HitPoints);
        NativeCharacterVitals undone = NativeJunctionEdits.Apply(applied, NativeJunctionEdits.CreateUndo(source, applied));
        Assert.Equal(source.Records[0].Junction, undone.Records[0].Junction);
        Assert.Equal(applied.Records[0].HitPoints, undone.Records[0].HitPoints);
        Assert.Equal(JsonSerializer.Serialize(applied.MagicSystem), JsonSerializer.Serialize(undone.MagicSystem));
        Assert.Equal(source.Records[0].MagicInventory, undone.Records[0].MagicInventory);
    }

    [Fact]
    public void SharedTransferMustClearTheDonorAndReservedFieldsAreProtected()
    {
        NativeCharacterVitals source = Source(shared: true);
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[3].Junction.MagicJunctions[0] = 1;
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended)));
        intended.Records[0].Junction.MagicJunctions[0] = 0;
        NativeCharacterVitals next = NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended));
        Assert.Equal(3, MagicJunctionPolicy.GetOwner(next, 1));
        Assert.Equal(new NativeHpWord(10, 10), next.Records[0].HitPoints);
        intended.Records[0].Junction.MagicJunctions[19] = 0;
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended)));
    }
    [Fact]
    public void FinalGfCapabilitiesValidateCommandsAndMagicTogether()
    {
        NativeCharacterVitals source = Source();
        source.GuardianForces[0] = source.GuardianForces[0] with { LearnedBank = new NativeGuardianForceLearnedBank((1UL << 1) | (1UL << 2) | (1UL << 21) | (1UL << 39), 0) };
        source.Records[0] = source.Records[0] with { Junction = source.Records[0].Junction with { Commands = 21, Abilities = 39 } };
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[0] = intended.Records[0] with { Junction = intended.Records[0].Junction with { GuardianForces = 0 } };
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended)));
        intended.Records[0] = intended.Records[0] with { Junction = intended.Records[0].Junction with { Commands = 0, Abilities = 0 } };
        intended.Records[0].Junction.MagicJunctions[0] = 0;
        intended.Records[3] = intended.Records[3] with { Junction = intended.Records[3].Junction with { GuardianForces = (ushort)(intended.Records[3].Junction.GuardianForces | 1) } };
        NativeCharacterVitals next = NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended));
        Assert.Equal((ushort)0, next.Records[0].Junction.GuardianForces);
        Assert.Contains(0, next.Records[3].Junction.GuardianForceAssignments);
        Assert.Equal(source.GuardianForces, next.GuardianForces);
        Assert.Equal(source.Records[4], next.Records[4]);
    }    [Theory]
    [InlineData(NativeJunctionEditOperation.ClearCharacterMagic)]
    [InlineData(NativeJunctionEditOperation.RemoveCharacterJunctions)]
    [InlineData(NativeJunctionEditOperation.ClearPartyMagic)]
    public void ExplicitOffKeepsNativeReservedByteClearingAndItsScopedUndo(NativeJunctionEditOperation operation)
    {
        NativeCharacterVitals source = Source();
        source.Records[0] = source.Records[0] with { Junction = source.Records[0].Junction with { Commands = 0xAA000000 } };
        source.Records[7].Junction.MagicJunctions[19] = 42;
        NativeCharacterVitals intended = operation switch
        {
            NativeJunctionEditOperation.ClearCharacterMagic => source.RemoveJunctions(0, false),
            NativeJunctionEditOperation.RemoveCharacterJunctions => source.RemoveJunctions(0, true),
            _ => source.RemoveAllMagicJunctions()
        };
        NativeJunctionEditPlan plan = NativeJunctionEdits.Create(source, intended, operation);
        NativeCharacterVitals applied = NativeJunctionEdits.Apply(source, plan);
        Assert.All(applied.Records[0].Junction.MagicJunctions, spell => Assert.Equal(0, spell));
        Assert.Equal(operation == NativeJunctionEditOperation.RemoveCharacterJunctions ? 0u : source.Records[0].Junction.Commands, applied.Records[0].Junction.Commands);
        Assert.Equal(operation == NativeJunctionEditOperation.ClearPartyMagic ? (byte)0 : (byte)42, applied.Records[7].Junction.MagicJunctions[19]);
        NativeJunctionEditPlan undo = NativeJunctionEdits.CreateUndo(source, applied, operation);
        NativeCharacterVitals restored = NativeJunctionEdits.Apply(applied, undo);
        Assert.Equal(source.Records.Select(record => record.Junction), restored.Records.Select(record => record.Junction));
        Assert.Equal(applied.Records[0].HitPoints, restored.Records[0].HitPoints);
        Assert.Equal(JsonSerializer.Serialize(source.MagicSystem), JsonSerializer.Serialize(restored.MagicSystem));
        NativeCharacterVitals changed = applied.DeepCopy();
        changed.MagicSystem.PersonalFieldMasks[0][1] = 1;
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(changed, undo));
    }

    [Fact]
    public void OffIntentCannotSmugglePartialClearUnrelatedAssignmentsOrIncompletePartyScope()
    {
        NativeCharacterVitals source = Source();
        NativeCharacterVitals intended = source.DeepCopy();
        intended.Records[0].Junction.MagicJunctions[19] = 0;
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended, NativeJunctionEditOperation.ClearCharacterMagic)));
        intended = source.RemoveJunctions(0, true);
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended, NativeJunctionEditOperation.ClearCharacterMagic)));
        source.Records[7].Junction.MagicJunctions[19] = 42;
        intended = source.RemoveJunctions(0, false);
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended, NativeJunctionEditOperation.ClearPartyMagic)));
        NativeCharacterVitals applied = NativeJunctionEdits.Apply(source, NativeJunctionEdits.Create(source, intended, NativeJunctionEditOperation.ClearCharacterMagic));
        NativeCharacterVitals restored = applied.DeepCopy();
        restored.Records[0] = restored.Records[0] with { Junction = source.Records[0].Junction };
        Assert.Throws<InvalidOperationException>(() => NativeJunctionEdits.Apply(applied, NativeJunctionEdits.Create(applied, restored)));
    }}
