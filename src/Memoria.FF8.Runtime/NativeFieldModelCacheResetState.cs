using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Exact5310F0 zero ranges while resource registration is pending; adjacent native memory remains unobserved.</summary>
public sealed record NativeFieldModelCacheResetState([property: JsonRequired] int Version, [property: JsonRequired] string ExecutableHash)
{
    public static NativeFieldModelCacheResetState Execute5310F0(NativeFieldRuntimeProfile profile)
    {
        if (!profile.IsVerified)
        {
            throw new NotSupportedException("Native5310F0 requires its authenticated original clear-range contract.");
        }
        return new(1, profile.ExecutableHash);
    }

    public void Validate()
    {
        if (Version != 1 || !NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash))
        {
            throw new InvalidDataException("Native model cache reset lacks executed5310F0 provenance.");
        }
    }

    public ImmutableArray<byte> Read(uint address, int length)
    {
        Validate();
        bool known = InRange(address, length, 0x1DCA328, 0xC00) || InRange(address, length, 0x1DCB0F0, 0x100) || InRange(address, length, 0x1DCB340, 0x80);
        if (!known)
        {
            throw new NotSupportedException("Native5310F0 does not establish bytes outside its three exact model-cache clear ranges.");
        }
        return new byte[length].ToImmutableArray();
    }

    private static bool InRange(uint address, int length, uint start, int count)
    {
        return length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)count;
    }
}
