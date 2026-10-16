using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    public NativeKnownMagic ReadNewGameKnownMagic()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native magic knowledge initialization requires the authenticated original executable.");
        }
        //56D9F0/47E410 clear1CFDC58..1CFEFF8;56DA10 init.out ends1CFE7A4 before the two DWORDs1CFE95C..963.
        return new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
    }
}
