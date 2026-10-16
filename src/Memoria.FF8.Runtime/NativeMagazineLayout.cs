namespace Memoria.FF8.Runtime;

/// <summary>Original mmag page coordinates, relative to the paper's top-left corner.</summary>
public sealed record NativeMagazineLayout(int X, int Y, int Width, int Height, NativeMagazineText[] Texts, NativeMagazineImage[] Images, NativeMagazineTint? Tint)
{
    public NativeMagazineColor PaperColor { get; init; } = new(0, 0, 0);
    public NativeMagazineIcon[] Icons { get; init; } = [];
    public NativeMagazineButton[] Buttons { get; init; } = [];
}

public sealed record NativeMagazineText(int X, int Y, string Text, int FontSize = 12, int LineHeight = 16, bool RightAligned = false);
public sealed record NativeMagazineImage(int X, int Y, int ImageId, int Width, int Height);
public sealed record NativeMagazineTint(int X, int Y, int Width, int Height, byte Red, byte Green, byte Blue);
public sealed record NativeMagazineIcon(int X, int Y, int IconId, int Palette = 0);
public sealed record NativeMagazineButton(int X, int Y, int ButtonBit);
public sealed record NativeMagazineColor(byte Red, byte Green, byte Blue);
