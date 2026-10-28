using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed partial class TripleTriadElementView
{
    private static void DrawStone(IUiCanvas canvas, UiRect bounds, int cell)
    {
        UiRect rock = new(bounds.X + bounds.Width * 0.12f, bounds.Y + bounds.Height * 0.12f, bounds.Width * 0.76f, bounds.Height * 0.76f);
        canvas.Fill(new UiRect(rock.X + 6, rock.Y + 10, rock.Width, rock.Height), new UiColor(32, 24, 20, 100));
        (float X, float Y)[] outline = [(0.13f, 0), (0.83f, 0.03f), (1, 0.18f), (0.94f, 0.86f), (0.77f, 1), (0.08f, 0.94f), (0, 0.73f), (0.02f, 0.16f)];
        (float X, float Y)[] face = [(0.16f, 0.16f), (0.78f, 0.16f), (0.85f, 0.25f), (0.81f, 0.78f), (0.70f, 0.86f), (0.18f, 0.81f), (0.14f, 0.69f), (0.13f, 0.27f)];
        StonePolygon(canvas, rock, outline, new UiColor(104, 101, 86));
        StonePolygon(canvas, rock, [(0.13f, 0), (0.83f, 0.03f), (1, 0.18f), (0.85f, 0.25f), (0.78f, 0.16f), (0.16f, 0.16f), (0.02f, 0.16f)], new UiColor(179, 174, 148));
        StonePolygon(canvas, rock, [(0.85f, 0.25f), (1, 0.18f), (0.94f, 0.86f), (0.77f, 1), (0.70f, 0.86f), (0.81f, 0.78f)], new UiColor(67, 68, 62));
        StonePolygon(canvas, rock, face, new UiColor(139, 137, 117));
        for (int index = 0; index < 9; index++)
        {
            float x = rock.X + rock.Width * (0.23f + (index * 17 + cell * 7) % 53 / 100f);
            float y = rock.Y + rock.Height * (0.25f + (index * 13 + cell * 3) % 50 / 100f);
            canvas.Line(x, y, x + rock.Width * 0.08f, y - rock.Height * 0.025f, new UiColor(209, 202, 171, 55), Math.Max(1, rock.Width * 0.012f));
        }
        void Crack(float x1, float y1, float x2, float y2)
        {
            canvas.Line(rock.X + rock.Width * x1, rock.Y + rock.Height * y1, rock.X + rock.Width * x2, rock.Y + rock.Height * y2, new UiColor(62, 63, 56), Math.Max(1, rock.Width * 0.014f));
        }
        Crack(0.39f, 0.17f, 0.44f, 0.37f);
        Crack(0.44f, 0.37f, 0.35f, 0.48f);
        Crack(0.44f, 0.37f, 0.57f, 0.43f);
        Crack(0.80f, 0.66f, 0.67f, 0.61f);
        Crack(0.67f, 0.61f, 0.61f, 0.80f);
    }

    private static void StonePolygon(IUiCanvas canvas, UiRect bounds, (float X, float Y)[] points, UiColor color)
    {
        for (float y = 0; y < bounds.Height; y += 1.5f)
        {
            List<float> crossings = [];
            float row = y / bounds.Height;
            for (int index = 0; index < points.Length; index++)
            {
                var a = points[index];
                var b = points[(index + 1) % points.Length];
                if ((a.Y <= row && b.Y > row) || (b.Y <= row && a.Y > row))
                {
                    crossings.Add(a.X + (row - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }
            crossings.Sort();
            for (int index = 0; index + 1 < crossings.Count; index += 2)
            {
                canvas.Fill(new UiRect(bounds.X + crossings[index] * bounds.Width, bounds.Y + y, (crossings[index + 1] - crossings[index]) * bounds.Width, Math.Min(1.5f, bounds.Height - y)), color);
            }
        }
    }

    private void DrawWallHint(IUiCanvas canvas, int cell, int direction, int wallCell, float pulse)
    {
        UiRect source = CellBounds(cell);
        float x = source.X + source.Width / 2;
        float y = source.Y + source.Height / 2;
        float dx = direction == 1 ? 1 : direction == 3 ? -1 : 0;
        float dy = direction == 2 ? 1 : direction == 0 ? -1 : 0;
        float targetX = wallCell >= 0 ? CellBounds(wallCell).X + source.Width / 2 : x + dx * source.Width * 0.48f;
        float targetY = wallCell >= 0 ? CellBounds(wallCell).Y + source.Height / 2 : y + dy * source.Height * 0.48f;
        UiColor glow = new(255, (byte)(202 + 45 * pulse), (byte)(110 + 60 * pulse));
        for (int dot = 0; dot < 5; dot++)
        {
            float start = 0.25f + dot * 0.12f;
            float end = start + 0.055f;
            canvas.Line(x + (targetX - x) * start, y + (targetY - y) * start, x + (targetX - x) * end, y + (targetY - y) * end, glow, 2 + pulse);
        }
        canvas.OutlinedText("A", source.Height * 0.14f, new UiRect(targetX - 12, targetY - 18, 24, 36), glow, Ink, 1.5f);
    }
}
