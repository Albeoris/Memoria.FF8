using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceLocalOwnershipTests
{
    [Fact]
    public void ColdProcessProvidesAllSevenPhysicalLocalBanksExactlyOnce()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        cold.Validate();
        ImmutableArray<ImmutableArray<short?>> locals = cold.ModelSequenceLocals!.Value;
        Assert.Equal(7, locals.Length);
        NativeBattleGraphicsModel[] bank = NativeBattleGraphicsModels.ClearBank(retainedSequenceLocals: locals);
        foreach (NativeBattleGraphicsModel model in bank)
        {
            Assert.Equal(8, model.SequenceState!.Locals.Length);
            Assert.All(model.SequenceState.Locals, value => Assert.Equal((short)0, value));
            Assert.All(model.SequenceState.Scratch, Assert.Null);
        }
        Assert.All(NativeBattleGraphicsModels.ClearBank(), model => Assert.Null(model.SequenceState));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-32768)]
    [InlineData(32767)]
    public void InitializationAndSubsequentLoadPreserveKnownAndUnknownLocals(int? input)
    {
        short? retained = input is int value ? checked((short)value) : null;
        NativeBattleSequenceState prior = new() { Locals = NativeBattleSequenceState.ColdProcessLocals().SetItem(7, retained), Offset46 = 123, Wait1 = 9, Alternate43 = 17, Pose = 6 };
        NativeBattleSequenceState initialized = prior.InitializeControl5027D0(2, 3);
        Assert.Equal(prior.Locals, initialized.Locals);
        Assert.Equal((byte)2, initialized.Default2);
        Assert.Equal((byte)3, initialized.Pending3);
        Assert.Equal((ushort)0, initialized.Offset46);
        Assert.Equal((byte)0, initialized.Wait1);
        Assert.Equal((byte)0, initialized.Alternate43);
        Assert.Null(initialized.Pose);
        NativeBattleSequenceState restored = JsonSerializer.Deserialize<NativeBattleSequenceState>(JsonSerializer.Serialize(initialized))!;
        Assert.Equal(retained, restored.Locals[7]);
        NativeBattleSequenceState reloaded = restored.InitializeControl5027D0(1, 0);
        Assert.Equal(retained, reloaded.Locals[7]);
        Assert.Equal(initialized.Locals, reloaded.Locals);
    }

    [Fact]
    public void RetainedBankJsonPreservesValuesAndUnknownsWithoutColdFallback()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        ImmutableArray<ImmutableArray<short?>> rows = cold.ModelSequenceLocals!.Value;
        rows = rows.SetItem(4, rows[4].SetItem(6, null).SetItem(7, -123));
        NativeBattleRetainedMemory observed = cold with { ModelSequenceLocals = rows };
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(observed))!;
        restored.Validate();
        NativeBattleGraphicsModel[] bank = NativeBattleGraphicsModels.ClearBank(retainedSequenceLocals: restored.ModelSequenceLocals);
        Assert.Null(bank[4].SequenceState!.Locals[6]);
        Assert.Equal((short)-123, bank[4].SequenceState!.Locals[7]);
        Assert.All(bank[3].SequenceState!.Locals, value => Assert.Equal((short)0, value));
        JsonObject legacy = JsonNode.Parse(JsonSerializer.Serialize(cold))!.AsObject();
        legacy.Remove(nameof(NativeBattleRetainedMemory.ModelSequenceLocals));
        NativeBattleRetainedMemory old = legacy.Deserialize<NativeBattleRetainedMemory>()!;
        Assert.Null(old.ModelSequenceLocals);
        Assert.All(new NativeBattleSequenceState().InitializeControl5027D0(1, 0).Locals, Assert.Null);
    }

    [Fact]
    public void MalformedRetainedLocalBanksFailBeforeGraphicsInitialization()
    {
        ImmutableArray<ImmutableArray<short?>> malformed = [ImmutableArray.CreateRange(new short?[8])];
        Assert.Throws<InvalidDataException>(() => NativeBattleGraphicsModels.ClearBank(retainedSequenceLocals: malformed));
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ModelSequenceLocals = malformed };
        Assert.Throws<InvalidDataException>(retained.Validate);
    }
}
