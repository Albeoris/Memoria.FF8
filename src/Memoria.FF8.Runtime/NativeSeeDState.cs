using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native rank WORD16, salary counter and process salary UI; event flags live in the single DWORD104 owner.</summary>
public sealed record NativeSeeDState
{
    public const string InitializationRule = "ff8-en-newgame-52ca90-seed-word16-500-salary18-v1";

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required ushort RankPoints { get; init; }
    [JsonRequired]
    public required bool? SalaryUiEnabled { get; init; }
    [JsonRequired]
    public required uint SalaryMovementCounter { get; init; }

    public static NativeSeeDState CreateNewGame(NativeSeeDState? previousProcessState = null)
    {
        previousProcessState?.Validate();
        // 52CA90(1) clears248 event bytes, ORs DWORD104 with18 and writes WORD16=500.
        // Its saved-state initialization does not call the process UI setter4B86C0.
        return new NativeSeeDState { Version = 1, Rule = InitializationRule, RankPoints = 500, SalaryUiEnabled = previousProcessState?.SalaryUiEnabled, SalaryMovementCounter = 0 };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != InitializationRule || SalaryMovementCounter >= 0x6000)
        {
            throw new InvalidDataException("Native SeeD state lacks its executed initialization or crosses the unsupported salary payment boundary.");
        }
    }

    public NativeSeeDState AddRankPoints(int rawOperand)
    {
        Validate();
        short wrapped = unchecked((short)(RankPoints + rawOperand));
        return this with { RankPoints = (ushort)Math.Clamp((int)wrapped, 100, 3100) };
    }

    public NativeSeeDState WithSalaryUiEnabled(bool enabled)
    {
        Validate();
        // Native4B86C0 writes the process UI BYTE; DWORD104 has a separate canonical owner.
        return this with { SalaryUiEnabled = enabled };
    }

    public NativeSeeDState WithMovementUnits(uint units, uint eventFlags104, bool dreamEnabled)
    {
        Validate();
        if ((eventFlags104 & 8) != 0 || dreamEnabled)
        {
            return this;
        }
        uint counter = unchecked(SalaryMovementCounter + units);
        if (counter >= 0x6000)
        {
            throw new NotSupportedException("Native salary movement counter reached52B140: payment requires actual character kill totals, baseline WORD18, gil and salary UI history.");
        }
        return this with { SalaryMovementCounter = counter, RankPoints = (ushort)Math.Clamp((int)unchecked((short)RankPoints), 100, 3100) };
    }
}
