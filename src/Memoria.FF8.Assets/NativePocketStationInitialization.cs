using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativePocketStationInitialization
{
    public static NativePocketStationState Read(byte[] initial)
    {
        string hash = Convert.ToHexString(SHA256.HashData(initial));
        if (initial.Length != 2812 || hash != NativeInputConfiguration.OriginalInitHash)
        {
            throw new InvalidDataException("PocketStation initialization requires the authentic original New Game template.");
        }
        //56D9F0/47E410 clear1CFDC58..1CFEFF8;56DA10 copies init.out only through1CFE7A4.
        return new(hash, ImmutableArray.CreateRange(new byte[64])) { ChocoboName = ImmutableArray.CreateRange(new byte[12]) };
    }
}
