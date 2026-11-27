namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    public const string SquareLogoPath = "layout_pc/logo/square-enix-logo.png";

    public Stream? OpenStockSquareLogo()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return FindStockFile(SquareLogoPath)?.OpenRead();
    }
}
