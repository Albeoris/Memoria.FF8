using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public enum Ff7ShopPhase
{
    Opening,
    Interactive,
    Closing,
    Completed
}

public enum Ff7ShopTransactionKind
{
    Buy,
    Sell
}

public sealed record Ff7ShopTransaction(long Id, Ff7ShopTransactionKind Kind, Ff7ShopStockKind StockKind, int NativeId, int Quantity, int NativeSlot = -1);
public sealed record Ff7ShopTransactionReceipt(Ff7ShopTransaction Transaction, bool Applied, string? Refusal, uint UnitPrice, uint Total, uint GilAfter, int QuantityAfter);
public sealed record Ff7ShopMateriaEntry(int NativeSlot, int MateriaId, uint AbilityPoints, string Name, string Description, uint SalePrice);

public sealed record Ff7ShopState(long RequestId, int ShopId, Ff7ShopPhase Phase = Ff7ShopPhase.Opening, int Alpha = 255, int DisplayedAlpha = 255, long Tick = 0, Ff7ShopTransactionReceipt? LastTransaction = null)
{
    public long NextTransactionId => checked((LastTransaction?.Transaction.Id ?? 0) + 1);

    public void Validate()
    {
        if (RequestId < 1 || ShopId is < 0 or >= 80 || Phase is < Ff7ShopPhase.Opening or > Ff7ShopPhase.Completed || Alpha is < 0 or > 255 || DisplayedAlpha is < 0 or > 255 || Tick < 0 || Phase == Ff7ShopPhase.Interactive && Alpha != 0 || Phase == Ff7ShopPhase.Completed && (Alpha != 255 || DisplayedAlpha != 255))
        {
            throw new InvalidDataException("The native shop continuation is invalid.");
        }
        if (LastTransaction is { } receipt && (receipt.Transaction is null || receipt.Transaction.Id < 1 || receipt.Transaction.Quantity is < 1 or > 127 || receipt.Transaction.Kind is < Ff7ShopTransactionKind.Buy or > Ff7ShopTransactionKind.Sell || receipt.Transaction.StockKind is < Ff7ShopStockKind.Item or > Ff7ShopStockKind.Special || receipt.Transaction.NativeId < 0 || receipt.Transaction.NativeSlot is < -1 or >= 200 || receipt.QuantityAfter < 0 || receipt.QuantityAfter > (receipt.Transaction.StockKind == Ff7ShopStockKind.Materia ? 200 : 127) || receipt.Applied == (receipt.Refusal is not null)))
        {
            throw new InvalidDataException("The native shop transaction receipt is invalid.");
        }
        if (LastTransaction is { } quote && (!Ff7NativeShopTransactions.Supports(quote.Transaction.StockKind, quote.Transaction.NativeId) || quote.Total != unchecked(quote.UnitPrice * (uint)quote.Transaction.Quantity)))
        {
            throw new InvalidDataException("The native shop transaction quote is inconsistent.");
        }
    }

    public Ff7ShopState Advance(bool cancel = false, bool confirmedExit = false)
    {
        Validate();
        if (Phase == Ff7ShopPhase.Completed)
        {
            return this;
        }
        Ff7ShopPhase phase = Phase;
        int alpha = Alpha;
        if (phase == Ff7ShopPhase.Opening)
        {
            if (alpha == 0)
            {
                phase = Ff7ShopPhase.Interactive;
            }
            else
            {
                alpha = Math.Max(0, alpha - 10);
            }
        }
        else if (phase == Ff7ShopPhase.Closing)
        {
            if (alpha == 255)
            {
                phase = Ff7ShopPhase.Completed;
            }
            else
            {
                alpha = Math.Min(255, alpha + 10);
            }
        }
        if (phase == Ff7ShopPhase.Interactive && (cancel || confirmedExit))
        {
            phase = Ff7ShopPhase.Closing;
        }
        return this with { Phase = phase, Alpha = alpha, DisplayedAlpha = Alpha, Tick = checked(Tick + 1) };
    }
}
