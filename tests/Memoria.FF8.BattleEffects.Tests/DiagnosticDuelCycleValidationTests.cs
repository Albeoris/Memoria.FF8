using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class DiagnosticDuelCycleValidationTests
{
    [Fact]
    public void Native_timed_control_keeps_raw_zero_header_effect_separate_from_retained274_module()
    {
        DiagnosticDuelCycleReceipt receipt = DuelLiveEvidenceValidationTests.Cycle(0);
        Assert.Equal((ushort)0, receipt.DispatchPackets[^1].HeaderEffect);
        Assert.Equal((ushort)274, receipt.DispatchPackets[^1].RetainedEffect);
        Assert.Equal((ushort)274, receipt.Dispatches[^1].RequestedEffect);
        Assert.Equal((byte)255, receipt.TimedOut!.Duel!.Selection1D28DEF);
        Assert.Equal((byte)254, receipt.DispatchPackets[^1].Duel!.Selection1D28DEF);
        Validate(receipt);
        DiagnosticDuelCycleValidation.ValidateChain(receipt, DuelLiveEvidenceValidationTests.Cycle(1));
    }

    [Theory]
    [InlineData(BattleAbility.LimitBreak)]
    [InlineData(BattleAbility.NativeCommand)]
    public void Initial_secondary_header_preserves_the_opening_owner_or_resolves_its_native_command(BattleAbility ability)
    {
        DiagnosticDuelCycleReceipt receipt = DuelLiveEvidenceValidationTests.Cycle(0);
        BattleDiagnosticEffectDispatch[] dispatches = receipt.Dispatches.ToArray();
        dispatches[1] = dispatches[1] with { Ability = ability };
        receipt = receipt with { Dispatches = dispatches };
        Validate(receipt);
        dispatches[1] = dispatches[1] with { Ability = BattleAbility.Magic };
        Assert.Throws<InvalidDataException>(() => Validate(receipt));
    }

    [Theory]
    [InlineData("missing_opening")]
    [InlineData("raw_effect")]
    [InlineData("retained_effect")]
    [InlineData("selector")]
    [InlineData("expired_owner")]
    [InlineData("selection_owner")]
    [InlineData("input_boundary")]
    [InlineData("raw239")]
    [InlineData("generation")]
    public void Native_cycle_rejects_forged_control_and_owner_boundaries(string fault)
    {
        DiagnosticDuelCycleReceipt receipt = DuelLiveEvidenceValidationTests.Cycle(0);
        DiagnosticDuelDispatchPackets[] packets = receipt.DispatchPackets.ToArray();
        BattleDiagnosticEffectDispatch[] dispatches = receipt.Dispatches.ToArray();
        switch (fault)
        {
            case "missing_opening":
                dispatches[0] = dispatches[0] with { NativeCommand = 241 };
                receipt = receipt with { Dispatches = dispatches };
                break;
            case "raw_effect":
                packets[^1] = packets[^1] with { HeaderEffect = 274 };
                receipt = receipt with { DispatchPackets = packets };
                break;
            case "retained_effect":
                packets[^1] = packets[^1] with { RetainedEffect = 0 };
                receipt = receipt with { DispatchPackets = packets };
                break;
            case "selector":
                packets[^1] = packets[^1] with { Parameter80 = 2 };
                receipt = receipt with { DispatchPackets = packets };
                break;
            case "expired_owner":
                packets[^1] = packets[^1] with { Duel = packets[^1].Duel! with { Selection1D28DEF = 255 } };
                receipt = receipt with { DispatchPackets = packets };
                break;
            case "selection_owner":
                receipt = receipt with { Selection = receipt.Selection with { CommandOwner = 2 } };
                break;
            case "input_boundary":
                DiagnosticDuelInputReceipt[] inputs = receipt.Inputs.ToArray();
                inputs[0] = inputs[0] with { Before = inputs[0].Before with { Callback = 9 } };
                receipt = receipt with { Inputs = inputs };
                break;
            case "raw239":
                dispatches[2] = dispatches[2] with { NativeCommand = 239 };
                receipt = receipt with { Dispatches = dispatches };
                break;
            default:
                packets[^1] = packets[^1] with { HeaderGeneration = packets[0].HeaderGeneration };
                dispatches[^1] = dispatches[^1] with { NativeHeaderGeneration = dispatches[0].NativeHeaderGeneration };
                receipt = receipt with { Dispatches = dispatches, DispatchPackets = packets };
                break;
        }
        Assert.Throws<InvalidDataException>(() => Validate(receipt));
    }

    [Fact]
    public void Second_cycle_must_rearm_after_native_closure_with_fresh_header_ownership()
    {
        DiagnosticDuelCycleReceipt first = DuelLiveEvidenceValidationTests.Cycle(0);
        DiagnosticDuelCycleReceipt second = DuelLiveEvidenceValidationTests.Cycle(1);
        Assert.Throws<InvalidDataException>(() => DiagnosticDuelCycleValidation.ValidateChain(first, second with { BeforeOpening = second.BeforeOpening with { Callback = first.Completed!.Callback } }));
        BattleDiagnosticEffectDispatch[] dispatches = second.Dispatches.ToArray();
        dispatches[0] = dispatches[0] with { NativeHeaderGeneration = first.Dispatches[0].NativeHeaderGeneration };
        Assert.Throws<InvalidDataException>(() => DiagnosticDuelCycleValidation.ValidateChain(first, second with { Dispatches = dispatches }));
    }

    private static void Validate(DiagnosticDuelCycleReceipt receipt)
    {
        DiagnosticDuelCycleValidation.ValidateReceipt(receipt, DuelLiveEvidenceValidationTests.Input, 274, receipt.Dispatches, 10, 14);
    }
}
