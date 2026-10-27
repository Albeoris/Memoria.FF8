using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativePocketStationInitialization
{
    public static NativePocketStationState Read(byte[] initial)
    {
        NativeInitialData.Validate(initial);
        //56D9F0/47E410 clear1CFDC58..1CFEFF8;56DA10 copies the supported initialization prefix no later than1CFE928.
        return new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.CreateRange(new byte[64])) { ChocoboName = ImmutableArray.CreateRange(new byte[12]) };
    }
}
