using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<NativeBattleRawMesh> RequireNativeStageGeometry()
    {
        NativeBattleStageSource source = definition.NativeStageSource ?? throw new NotSupportedException("Native stage geometry requires its installed stage source.");
        if (source.Callback?.Address == NativeBattleStage137.CallbackAddress)
        {
            return NativeStage?.MutableGeometry ?? throw new NotSupportedException("Native50DFF0 requires its retained mutable stage geometry.");
        }
        return source.Geometry ?? throw new NotSupportedException("Native stage geometry requires its original four-group object directory.");
    }
}
