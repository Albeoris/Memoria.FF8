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
    [JsonRequired]
    public required ushort KillBaseline18 { get; init; }
    [JsonRequired]
    public required ushort SalaryDisplayCountdown184 { get; init; }
    [JsonRequired]
    public required ushort PreviousRank186 { get; init; }
    [JsonRequired]
    public required NativeSalaryTable? SalaryTable { get; init; }
    public byte TestLevel { get; init; }

    public static NativeSeeDState CreateNewGame(NativeSeeDState? previousProcessState = null)
    {
        previousProcessState?.Validate();
        // 52CA90(1) clears248 event bytes, ORs DWORD104 with18 and writes WORD16=500.
        // Its final4B86C0((~DWORD104>>3)&1) writes process UIfalse for actual new flags18.
        return new NativeSeeDState { Version = 1, Rule = InitializationRule, RankPoints = 500, SalaryUiEnabled = false, SalaryMovementCounter = 0, KillBaseline18 = 0, SalaryDisplayCountdown184 = 0, PreviousRank186 = 0, SalaryTable = previousProcessState?.SalaryTable };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != InitializationRule)
        {
            throw new InvalidDataException("Native SeeD state lacks its executed current initialization.");
        }
        SalaryTable?.Validate();
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

    public int ReadEventWords(int address, int width)
    {
        ValidateEventWordRange(address, width);
        uint words = address < 12 ? SalaryMovementCounter : address < 20 ? RankPoints | (uint)KillBaseline18 << 16 : SalaryDisplayCountdown184 | (uint)PreviousRank186 << 16;
        int start = address < 12 ? 8 : address < 20 ? 16 : 184;
        uint mask = width == 4 ? uint.MaxValue : (1u << (8 * width)) - 1;
        return unchecked((int)((words >> (8 * (address - start))) & mask));
    }

    public NativeSeeDState WriteEventWords(int address, int width, int value)
    {
        ValidateEventWordRange(address, width);
        int start = address < 12 ? 8 : address < 20 ? 16 : 184;
        int shift = 8 * (address - start);
        uint mask = width == 4 ? uint.MaxValue : ((1u << (8 * width)) - 1) << shift;
        uint words = start == 8 ? SalaryMovementCounter : start == 16 ? RankPoints | (uint)KillBaseline18 << 16 : SalaryDisplayCountdown184 | (uint)PreviousRank186 << 16;
        words = (words & ~mask) | ((unchecked((uint)value) << shift) & mask);
        return start == 8 ? this with { SalaryMovementCounter = words } : start == 16 ? this with { RankPoints = unchecked((ushort)words), KillBaseline18 = (ushort)(words >> 16) } : this with { SalaryDisplayCountdown184 = unchecked((ushort)words), PreviousRank186 = (ushort)(words >> 16) };
    }

    private void ValidateEventWordRange(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || !(address >= 8 && (long)address + width <= 12 || address >= 16 && (long)address + width <= 20 || address >= 184 && (long)address + width <= 188))
        {
            throw new NotSupportedException("SeeD projection cannot consume surrounding unknown native event-bank bytes.");
        }
    }

}
