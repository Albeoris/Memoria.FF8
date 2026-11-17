namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private int NativeModelResourceRowCapacity()
    {
        return NativeModelCpuResources?.Rows.Length ?? NativeBattleModelResourceRowAddress.NativeRowCount;
    }

    private uint NativeModelResourceAddress100(NativeBattleGraphicsModel model)
    {
        return NativeBattleModelResourceRow.Address100(model, NativeModelResourceRowCapacity());
    }
}
