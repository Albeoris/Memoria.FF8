using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Current bytes of adjacent EXE model regions1298C68/109FC and12A9664/4D9C; not a fresh copy of immutable catalog metadata.</summary>
public sealed record NativeBattleQuezacotlExecutableMemory([property: JsonRequired] ImmutableArray<byte> Data)
{
    public const uint Address = 0x1298C68;
    public const int PrimaryLength = 0x109FC;
    public const int SecondaryLength = 0x4D9C;
    public const int Length = PrimaryLength + SecondaryLength;

    public void Validate()
    {
        if (Data.IsDefault || Data.Length != Length)
        {
            throw new InvalidDataException("Native Quezacotl requires its exact two retained executable model regions.");
        }
    }

    public NativeBattleQuezacotlExecutableMemory Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Validate();
        if (address < Address || address - Address > Length || bytes.Length > Length - (address - Address))
        {
            throw new InvalidDataException("Native Quezacotl executable write leaves its owned model regions.");
        }
        byte[] updated = Data.ToArray();
        bytes.CopyTo(updated.AsSpan(checked((int)(address - Address))));
        return new(updated.ToImmutableArray());
    }

    /// <summary>6C5196..6C51C5 copies both original571B70 backup ranges back to their executable addresses.</summary>
    public NativeBattleQuezacotlExecutableMemory Restore6C5196(NativeBattleGuardianWorkBuffer backup)
    {
        Validate();
        backup.Validate();
        return new(backup.Read(0, Length).ToImmutableArray());
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleQuezacotlExecutableMemory? QuezacotlExecutableMemory { get; init; }
}
