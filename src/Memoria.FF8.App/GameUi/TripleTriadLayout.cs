using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct TripleTriadLayout(int Width, int Height, int HandSize)
{
    public float CellWidth => Math.Min(480f / Width, 480f / Height);
    public float CellHeight => CellWidth * 1.2f;
    public UiRect Board => new(480 - CellWidth * Width / 2, 336 - CellHeight * Height / 2, CellWidth * Width, CellHeight * Height);

    public UiRect Cell(int index) => new(Board.X + index % Width * CellWidth, Board.Y + index / Width * CellHeight, CellWidth, CellHeight);

    public UiRect Hand(int index, int owner)
    {
        if (HandSize <= 5)
        {
            return new UiRect(owner == 0 ? 755 : 45, 24 + index * 108, 160, 192);
        }
        int rows = Math.Max(5, (int)Math.Ceiling(Math.Sqrt(HandSize * 3.6)));
        int columns = (HandSize + rows - 1) / rows;
        float width = Math.Min(170f / columns, 550f / (1.2f + (rows - 1) * 0.7f));
        float height = width * 1.2f;
        float pitch = Math.Min(height * 0.7f, (570 - height) / Math.Max(1, rows - 1));
        return new UiRect((owner == 0 ? 748 : 40) + index / rows * (174f / columns), 24 + index % rows * pitch, width, height);
    }
}
