using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDefeatReturnTests
{
    [Fact]
    public void FreshDefeatOriginPreservesReasonZeroAndRequiresTheClearSaved183Bit()
    {
        NativeFieldLoadContext fresh = NativeFieldLoadContext.FromBattleDefeat(new(0x1FF));
        fresh.Validate();
        Assert.Equal((byte)0, fresh.Reason);
        Assert.Equal(75, fresh.FieldId);
        Assert.Throws<InvalidDataException>(() => NativeFieldLoadContext.FromBattleDefeat(new(0x200)).Validate());
        Assert.Equal(fresh, JsonSerializer.Deserialize<NativeFieldLoadContext>(JsonSerializer.Serialize(fresh)));
    }

    [Fact]
    public void AbandonmentRetainsTheUncompletedBattleWaitAndItsExactClockInterval()
    {
        string hash = new('A', 64);
        BattleEncounterOccurrence occurrence = new("abandoned", 12, hash, SceneKind.Field, 999, 20, hash, new(999, 0, 1, 7, 0, hash));
        NativeBattleAbandonment receipt = new(occurrence, new(0), 100, 112, 3) { ScriptWait = new(4, 8, 21) };
        receipt.Validate();
        Assert.Equal(receipt, JsonSerializer.Deserialize<NativeBattleAbandonment>(JsonSerializer.Serialize(receipt)));
        Assert.Throws<InvalidDataException>(() => (receipt with { ScriptWait = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { ScriptWait = receipt.ScriptWait! with { NextOperation = 9 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { ExitClockCalls = 116 }).Validate());
    }
}
