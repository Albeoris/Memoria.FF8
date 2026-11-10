namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void AdvanceNativeShop(bool cancel = false)
    {
        if (Process.Menu is not { Mode: 8, Completed: false } menu)
        {
            throw new InvalidOperationException("No native shop is active.");
        }
        Ff7ShopState state = menu.Shop ?? new(menu.RequestId, menu.Parameter);
        Ff7ShopState next = state.Advance(cancel);
        if (cancel && next.Phase == Ff7ShopPhase.Closing && state.Phase != Ff7ShopPhase.Closing)
        {
            SubmitNativeSound(new(5, 4));
        }
        PublishShop(next);
        if (next.Phase == Ff7ShopPhase.Completed)
        {
            CompleteMenu(next.RequestId);
        }
    }

    internal void PublishShop(Ff7ShopState state)
    {
        state.Validate();
        if (Process.Menu is not { Mode: 8 } menu || menu.RequestId != state.RequestId || menu.Parameter != state.ShopId || Process.GlobalCommand != Ff7GlobalCommand.Menu || Process.GlobalRequestId != state.RequestId)
        {
            throw new InvalidOperationException("Only the current native shop owner can publish its state.");
        }
        Process = Process with { Menu = menu with { Shop = state } };
    }
}
