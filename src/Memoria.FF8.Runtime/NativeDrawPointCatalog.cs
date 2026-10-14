using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated immutable inputs for native522770/52D1E0 and495530's command projection.</summary>
public sealed record NativeDrawPointCatalog(string ExecutableHash, string KernelHash, ImmutableArray<byte> PointBytes, ImmutableArray<byte> CommandIds, string NoEligibleMessage, string CommonMessageHash)
{
    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || KernelHash is not { Length: 64 } || !KernelHash.All(Uri.IsHexDigit) || PointBytes.IsDefault || PointBytes.Length != 256 || CommandIds.IsDefault || CommandIds.Length != 19 || string.IsNullOrEmpty(NoEligibleMessage) || CommonMessageHash is not { Length: 64 } || !CommonMessageHash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Native draw-point catalog lacks its original point bytes, command mapping or common message7.");
        }
    }

    public bool HasDrawCommand(NativeCharacterVital character)
    {
        for (int slot = 0; slot < 4; slot++)
        {
            byte ability = unchecked((byte)(character.Junction.Commands >> (8 * slot)));
            if (ability is >= 20 and < 39 && CommandIds[ability - 20] == 6)
            {
                return true;
            }
        }
        return false;
    }
}
