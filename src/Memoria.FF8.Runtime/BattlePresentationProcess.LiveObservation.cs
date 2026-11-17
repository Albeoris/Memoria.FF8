using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public NativeCrtRandomState? CurrentCrtRandom => nativeCrtRandom;
}
