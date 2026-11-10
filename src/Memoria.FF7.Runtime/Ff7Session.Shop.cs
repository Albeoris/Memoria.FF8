using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private Ff7ShopResources? shopResources;

    public Ff7ShopResources ShopResources => shopResources ??= Ff7ShopResources.Load(installation);
    public Ff7ShopState? Shop => Host.Process.Menu is { Mode: 8 } menu ? menu.Shop ?? new(menu.RequestId, menu.Parameter, menu.Completed ? Ff7ShopPhase.Completed : Ff7ShopPhase.Opening) : null;

    public void AdvanceShop(bool cancel = false)
    {
        Ff7ShopState state = Shop ?? throw new InvalidOperationException("No native shop is active.");
        if (state.Phase == Ff7ShopPhase.Completed)
        {
            return;
        }
        _ = ShopResources.Get(state.ShopId);
        Host.AdvanceNativeShop(cancel);
    }

    public IReadOnlyList<Ff7ShopMateriaEntry> ShopMateriaInventory => Ff7NativeMateriaInventory.Normalize(Progress.Materia).Select(materia => new Ff7ShopMateriaEntry(materia.NativeSlot!.Value, materia.MateriaId, materia.AbilityPoints, ShopResources.DescribeMateria(materia.MateriaId).Name, ShopResources.DescribeMateria(materia.MateriaId).Description, ShopResources.MateriaSalePrice(materia.MateriaId, materia.AbilityPoints))).ToArray();

    public int ShopMaximumQuantity(Ff7ShopTransactionKind kind, Ff7ShopStockKind stockKind, int nativeId, int nativeSlot = -1)
    {
        Ff7ShopState state = Shop ?? throw new InvalidOperationException("No native shop is active.");
        return Ff7NativeShopTransactions.MaximumQuantity(Progress, ShopResources, state.ShopId, kind, stockKind, nativeId, nativeSlot);
    }

    public uint ShopUnitPrice(Ff7ShopTransactionKind kind, Ff7ShopStockKind stockKind, int nativeId, int nativeSlot = -1)
    {
        _ = Shop ?? throw new InvalidOperationException("No native shop is active.");
        return Ff7NativeShopTransactions.UnitPrice(Progress, ShopResources, kind, stockKind, nativeId, nativeSlot);
    }

    public Ff7ShopTransactionReceipt ApplyShopTransaction(Ff7ShopTransaction transaction)
    {
        Ff7ShopState state = Shop ?? throw new InvalidOperationException("No native shop is active.");
        Ff7ShopTransactionResult result = Ff7NativeShopTransactions.Apply(Progress, state, ShopResources, transaction);
        if (result.State != state)
        {
            Host.SubmitNativeSound(new(5, result.Receipt.Applied ? 0x108 : 3));
            Host.PublishShop(result.State);
            if (result.MateriaFlags != 0)
            {
                byte flags = Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 75);
                Machine.Variables.WriteByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 75, (byte)(flags | result.MateriaFlags));
            }
            Progress = result.Progress;
        }
        return result.Receipt;
    }
}
