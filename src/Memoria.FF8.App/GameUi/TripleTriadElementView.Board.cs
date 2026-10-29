using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class TripleTriadElementView
{
    private TripleTriadLayout Layout => new(controller.Match?.Width ?? controller.Options.Width, controller.Match?.Height ?? controller.Options.Height, controller.Match?.HandSize ?? controller.Options.HandSize);
    private static readonly UiColor Ink = new(69, 37, 19);
    private static readonly UiColor Gold = new(236, 196, 122);

    private void DrawBoard(IUiCanvas canvas)
    {
        canvas.Paper(Bounds, new UiColor(186, 139, 89));
        // Nested translucent washes create a warm parchment centre and dark table edges.
        for (int edge = 0; edge < 24; edge++)
        {
            float inset = edge * 5;
            canvas.Line(inset, 0, inset, 720, new UiColor(57, 30, 16, (byte)(30 - edge)), 5);
            canvas.Line(960 - inset, 0, 960 - inset, 720, new UiColor(57, 30, 16, (byte)(30 - edge)), 5);
        }
        UiRect board = Layout.Board;
        canvas.Fill(board, new UiColor(249, 215, 157, 55));
        for (int row = 0; row < Layout.Height; row++)
        {
            for (int column = 0; column < Layout.Width; column++)
            {
                UiRect cell = Layout.Cell(row * Layout.Width + column);
                canvas.Fill(cell.Inset(3), new UiColor(255, 226, 175, (byte)((row + column) % 2 == 0 ? 28 : 8)));
                Frame(canvas, cell.Inset(2), Ink, 1.7f);
                Frame(canvas, cell.Inset(5), Gold, 0.8f);
                float ornament = Math.Min(cell.Width, cell.Height) * 0.19f;
                Flourish(canvas, cell.X + 7, cell.Y + 7, ornament, 1, 1);
                Flourish(canvas, cell.Right - 7, cell.Y + 7, ornament, -1, 1);
                Flourish(canvas, cell.X + 7, cell.Bottom - 7, ornament, 1, -1);
                Flourish(canvas, cell.Right - 7, cell.Bottom - 7, ornament, -1, -1);
            }
        }
        Frame(canvas, new UiRect(board.X - 8, board.Y - 8, board.Width + 16, board.Height + 16), Ink, 3);
        Frame(canvas, new UiRect(board.X - 12, board.Y - 12, board.Width + 24, board.Height + 24), Gold, 1);
        float titleWidth = canvas.MeasureText("Triple Triad", 25, float.PositiveInfinity).Width;
        canvas.OutlinedText("Triple Triad", 25, new UiRect(480 - titleWidth / 2, 8, titleWidth, 30), Ink, new UiColor(221, 180, 116), 0.7f);
    }

    private static void Frame(IUiCanvas canvas, UiRect bounds, UiColor color, float thickness)
    {
        canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y, color, thickness);
        canvas.Line(bounds.Right, bounds.Y, bounds.Right, bounds.Bottom, color, thickness);
        canvas.Line(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom, color, thickness);
        canvas.Line(bounds.X, bounds.Bottom, bounds.X, bounds.Y, color, thickness);
    }

    private static void Flourish(IUiCanvas canvas, float x, float y, float size, int sx, int sy)
    {
        // Parametric tapered leaves, redrawn at the output resolution for every cell size.
        for (int leaf = 0; leaf < 2; leaf++)
        {
            float previousX = x;
            float previousY = y;
            for (int step = 1; step <= 16; step++)
            {
                float t = step / 16f;
                float along = size * t;
                float bend = size * (0.22f * MathF.Sin(t * MathF.PI) + 0.08f * t);
                float nextX = x + sx * (leaf == 0 ? along : bend);
                float nextY = y + sy * (leaf == 0 ? bend : along);
                canvas.Line(previousX, previousY, nextX, nextY, Ink, Math.Max(0.6f, size * 0.12f * (1 - t)));
                previousX = nextX;
                previousY = nextY;
            }
        }
    }
}
