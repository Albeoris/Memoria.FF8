using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native event WORD16 and salary masks, created only by the executed New Game initialization.</summary>
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
    public required bool SalarySuppressed { get; init; }
    [JsonRequired]
    public required bool SalaryDisplaySuppressed { get; init; }
    [JsonRequired]
    public required bool? SalaryUiEnabled { get; init; }
    [JsonRequired]
    public required uint SalaryMovementCounter { get; init; }

    public static NativeSeeDState CreateNewGame(NativeSeeDState? previousProcessState = null)
    {
        previousProcessState?.Validate();
        // 52CA90(1) clears248 event bytes, ORs DWORD104 with18 and writes WORD16=500.
        // Its saved-state initialization does not call the process UI setter4B86C0.
        return new NativeSeeDState { Version = 1, Rule = InitializationRule, RankPoints = 500, SalarySuppressed = true, SalaryDisplaySuppressed = true, SalaryUiEnabled = previousProcessState?.SalaryUiEnabled, SalaryMovementCounter = 0 };
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

    public NativeSeeDState WithSalaryEnabled(bool enabled)
    {
        Validate();
        // 51DB90/51DBC0 write both mask8 and mask10, then4B86C0 writes the UI BYTE.
        return this with { SalarySuppressed = !enabled, SalaryDisplaySuppressed = !enabled, SalaryUiEnabled = enabled };
    }

    public NativeSeeDState WithSalaryDisplayEnabled(bool enabled)
    {
        Validate();
        // 51DBF0/51DC10 alter mask10 only; they do not invoke4B86C0.
        return this with { SalaryDisplaySuppressed = !enabled };
    }

    public NativeSeeDState WithMovementUnits(uint units)
    {
        Validate();
        if (SalarySuppressed)
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
