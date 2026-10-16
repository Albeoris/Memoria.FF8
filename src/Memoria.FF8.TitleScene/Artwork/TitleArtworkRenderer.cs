using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Windows;
using System.Windows.Media;
using Color = Microsoft.Xna.Framework.Color;
using Geometry = System.Windows.Media.Geometry;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Memoria.FF8.TitleScene;

/// <summary>
/// The supplied silver crest traced into contour paths, with system font outlines.
/// Contours become GPU triangles once; no image asset is loaded at runtime.
/// </summary>
public sealed class TitleArtworkRenderer : IDisposable
{
    private const float DesignWidth = 1600f;
    private const float DesignHeight = 900f;
    private readonly GraphicsDevice graphicsDevice;
    private readonly BasicEffect effect;
    private readonly VertexBuffer metalBuffer;
    private readonly VertexBuffer glintBuffer;
    private readonly VertexBuffer wordmarkBuffer;
    private readonly VertexBuffer wordmarkGlintBuffer;
    private readonly VertexBuffer[] sheenBuffers;
    private readonly int[] sheenPrimitiveCounts;
    private readonly int metalPrimitiveCount;
    private readonly int glintPrimitiveCount;
    private readonly int wordmarkPrimitiveCount;
    private readonly int wordmarkGlintPrimitiveCount;
    private bool disposed;

    public TitleArtworkRenderer(GraphicsDevice graphicsDevice)
    {
        this.graphicsDevice = graphicsDevice;
        effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false
        };

        List<VertexPositionColor> metal = [];
        List<VertexPositionColor> glints = [];
        Geometry crest = BuildCrest(metal, glints);
        metalBuffer = CreateBuffer(metal);
        glintBuffer = CreateBuffer(glints);
        metalPrimitiveCount = metal.Count / 3;
        glintPrimitiveCount = glints.Count / 3;
        metal.Clear();
        glints.Clear();
        BuildWordmark(metal, glints);
        wordmarkBuffer = CreateBuffer(metal);
        wordmarkGlintBuffer = CreateBuffer(glints);
        wordmarkPrimitiveCount = metal.Count / 3;
        wordmarkGlintPrimitiveCount = glints.Count / 3;
        (sheenBuffers, sheenPrimitiveCounts) = BuildSheen(crest);
    }

    public void Draw(Rectangle bounds, double seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        BlendState previousBlend = graphicsDevice.BlendState;
        DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
        RasterizerState previousRasterizer = graphicsDevice.RasterizerState;
        SamplerState previousSampler = graphicsDevice.SamplerStates[0];
        try
        {
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            float crestScale = Math.Min(bounds.Height / DesignHeight, bounds.Width * 0.6f / 300f);
            effect.World = Matrix.CreateScale(crestScale, crestScale, 1f) * Matrix.CreateTranslation(bounds.Center.X - 800f * crestScale, bounds.Y + bounds.Height * 0.08f - 72f * crestScale, 0f);
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0f, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, 0f, -1f, 1f);
            float light = (float)TitleSceneAnimation.LightIntensity(seconds);
            float pulse = Math.Clamp((light - 0.78f) / 0.44f, 0f, 1f);
            effect.DiffuseColor = new Vector3(0.94f + pulse * 0.06f);
            effect.Alpha = 1f;
            DrawBuffer(metalBuffer, metalPrimitiveCount);
            effect.DiffuseColor = Vector3.One;
            effect.Alpha = 0.26f + pulse * 0.24f;
            DrawBuffer(glintBuffer, glintPrimitiveCount);
            float sheenCenter = (float)(TitleSceneAnimation.MotionSeconds(seconds) / 16.0 % 1.0) * sheenBuffers.Length;
            for (int index = 0; index < sheenBuffers.Length; index++)
            {
                float distance = MathF.Abs(index - sheenCenter);
                distance = Math.Min(distance, sheenBuffers.Length - distance);
                float alpha = (MathF.Exp(-distance * distance * 0.16f) * 0.07f + MathF.Exp(-distance * distance * 0.65f) * 0.11f) * (0.7f + pulse * 0.8f);
                if (alpha > 0.004f)
                {
                    effect.Alpha = alpha;
                    DrawBuffer(sheenBuffers[index], sheenPrimitiveCounts[index]);
                }
            }

            float wordmarkScale = Math.Min(bounds.Width / DesignWidth, bounds.Height / DesignHeight);
            effect.World = Matrix.CreateScale(wordmarkScale, wordmarkScale, 1f) * Matrix.CreateTranslation(bounds.Center.X - 800f * wordmarkScale, bounds.Y + bounds.Height * (469f / DesignHeight) - 469f * wordmarkScale, 0f);
            effect.DiffuseColor = new Vector3(0.96f + pulse * 0.04f);
            effect.Alpha = 1f;
            DrawBuffer(wordmarkBuffer, wordmarkPrimitiveCount);
            effect.DiffuseColor = Vector3.One;
            effect.Alpha = 0.29f + pulse * 0.22f;
            DrawBuffer(wordmarkGlintBuffer, wordmarkGlintPrimitiveCount);
        }
        finally
        {
            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.DepthStencilState = previousDepth;
            graphicsDevice.RasterizerState = previousRasterizer;
            graphicsDevice.SamplerStates[0] = previousSampler;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        metalBuffer.Dispose();
        glintBuffer.Dispose();
        wordmarkBuffer.Dispose();
        wordmarkGlintBuffer.Dispose();
        foreach (VertexBuffer buffer in sheenBuffers)
        {
            buffer.Dispose();
        }

        effect.Dispose();
    }

    private VertexBuffer CreateBuffer(List<VertexPositionColor> vertices)
    {
        VertexBuffer buffer = new(graphicsDevice, VertexPositionColor.VertexDeclaration, vertices.Count, BufferUsage.WriteOnly);
        buffer.SetData(vertices.ToArray());
        return buffer;
    }

    private void DrawBuffer(VertexBuffer buffer, int primitiveCount)
    {
        graphicsDevice.SetVertexBuffer(buffer);
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, primitiveCount);
        }
    }

    private static Geometry BuildCrest(List<VertexPositionColor> metal, List<VertexPositionColor> glints)
    {
        using Stream stream = typeof(TitleArtworkRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.TitleScene.SilverCrest.contours.json") ?? throw new InvalidOperationException("The embedded silver crest vector contours are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        Geometry silhouette = ReadContours(root.GetProperty("silhouette"));
        AddGeometry(metal, silhouette, _ => new Color(16, 17, 19));
        foreach (JsonElement layer in root.GetProperty("layers").EnumerateArray())
        {
            int value = layer.GetProperty("value").GetInt32();
            Color silver = new((byte)Math.Max(0, value - 3), (byte)Math.Max(0, value - 1), (byte)value);
            AddGeometry(metal, ReadContours(layer.GetProperty("contours")), _ => silver);
        }

        // Retain one very fine vector edge light, separate from the travelling sheen.
        AddGeometry(glints, silhouette.GetWidenedPathGeometry(new Pen(Brushes.White, 0.22)), _ => new Color(230, 235, 242, 80));
        return silhouette;
    }

    private static Geometry ReadContours(JsonElement contours)
    {
        const double scale = 381.0 / 1215.0;
        StreamGeometry geometry = new() { FillRule = FillRule.EvenOdd };
        using (StreamGeometryContext context = geometry.Open())
        {
            foreach (JsonElement contour in contours.EnumerateArray())
            {
                bool first = true;
                foreach (JsonElement coordinate in contour.EnumerateArray())
                {
                    System.Windows.Point point = new(800 + (coordinate[0].GetDouble() - 620) * scale, 72 + (coordinate[1].GetDouble() - 19) * scale);
                    if (first)
                    {
                        context.BeginFigure(point, true, true);
                        first = false;
                    }
                    else
                    {
                        context.LineTo(point, true, false);
                    }
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }
    private (VertexBuffer[] Buffers, int[] PrimitiveCounts) BuildSheen(Geometry crest)
    {
        List<VertexBuffer> buffers = [];
        List<int> primitiveCounts = [];
        for (int index = 0; index < 28; index++)
        {
            double left = 720 + index * 12;
            double right = left + 12;
            Geometry slice = Geometry.Parse(FormattableString.Invariant($"M {left - 20},50 L {right - 20},50 L {right - 200},500 L {left - 200},500 Z"));
            CombinedGeometry intersection = new(GeometryCombineMode.Intersect, crest, slice);
            List<VertexPositionColor> vertices = [];
            AddGeometry(vertices, intersection, _ => new Color(255, 255, 255));
            if (vertices.Count > 0)
            {
                buffers.Add(CreateBuffer(vertices));
                primitiveCounts.Add(vertices.Count / 3);
            }
        }

        return (buffers.ToArray(), primitiveCounts.ToArray());
    }

    private static void BuildWordmark(List<VertexPositionColor> metal, List<VertexPositionColor> glints)
    {
        Typeface typeface = new(new FontFamily("Times New Roman"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        FormattedText capital = new("H", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 120, Brushes.White, 1.0);
        Rect capBounds = capital.BuildGeometry(new System.Windows.Point()).Bounds;
        double scale = 50.0 / capBounds.Height;
        List<(Geometry Outline, double GapWeight)> glyphs = [];
        double naturalWidth = 0;
        double gapWeight = 0;
        bool wordSpace = false;
        int wordIndex = 0;
        foreach (char character in "FINAL FANTASY VIII")
        {
            if (character == ' ')
            {
                wordSpace = true;
                wordIndex++;
                continue;
            }

            FormattedText letter = new(character.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 120, Brushes.White, 1.0);
            Geometry glyph = letter.BuildGeometry(new System.Windows.Point());
            double precedingGap = glyphs.Count == 0 ? 0 : wordSpace ? 2.4 : wordIndex == 2 ? 0.2 : 1.0;
            glyphs.Add((glyph, precedingGap));
            naturalWidth += glyph.Bounds.Width * scale;
            gapWeight += precedingGap;
            wordSpace = false;
        }

        double tracking = (1344 - naturalWidth) / gapWeight;
        double cursor = 128;
        GeometryGroup outline = new() { Transform = Transform.Identity };
        foreach ((Geometry glyph, double precedingGap) in glyphs)
        {
            cursor += tracking * precedingGap;
            Rect glyphBounds = glyph.Bounds;
            glyph.Transform = new MatrixTransform(scale, 0, 0, scale, cursor - glyphBounds.X * scale, 469 - capBounds.Y * scale);
            outline.Children.Add(glyph);
            cursor += glyphBounds.Width * scale;
        }
        Geometry shadow = outline.Clone();
        TransformGroup shadowTransform = new();
        shadowTransform.Children.Add(outline.Transform.Clone());
        shadowTransform.Children.Add(new TranslateTransform(0, 2.2));
        shadow.Transform = shadowTransform;
        AddGeometry(metal, shadow.GetWidenedPathGeometry(new Pen(Brushes.Black, 2.7)), _ => new Color(12, 18, 27, 155));
        AddGeometry(metal, outline.GetWidenedPathGeometry(new Pen(Brushes.White, 1.1)), point => Silver(point, 0.77f));
        AddGeometry(metal, outline, point =>
        {
            float position = Math.Clamp(((float)point.Y - 469f) / 50f, 0f, 1f);
            float brightness = 0.98f - position * 0.23f + MathF.Exp(-MathF.Pow((position - 0.51f) * 19f, 2f)) * 0.11f;
            return new Color(brightness, Math.Min(1f, brightness + 0.015f), Math.Min(1f, brightness + 0.025f));
        });
        AddStroke(glints, "M 128,530 L 1472,530", 0.55, new Color(188, 207, 224, 95));
    }

    private static void AddStroke(List<VertexPositionColor> vertices, string path, double width, Color color)
    {
        Geometry stroke = Geometry.Parse(path).GetWidenedPathGeometry(new Pen(Brushes.White, width));
        AddGeometry(vertices, stroke, _ => color);
    }

    private static Color Silver(System.Windows.Point point, float brightness)
    {
        float diagonal = (float)(point.X * 0.65 + point.Y * 0.43);
        float band = 0.47f + MathF.Sin(diagonal * 0.048f) * 0.29f + MathF.Pow(Math.Max(0f, MathF.Sin(diagonal * 0.048f + 0.8f)), 16f) * 0.33f;
        float value = Math.Clamp(band * brightness + 0.12f, 0.11f, 1f);
        return new Color(value * 0.94f, value * 0.975f, Math.Min(1f, value + 0.025f));
    }

    private static void AddGeometry(List<VertexPositionColor> vertices, Geometry geometry, Func<System.Windows.Point, Color> color, double sampleWidth = double.MaxValue)
    {
        PathGeometry flattened = geometry.GetFlattenedPathGeometry(0.08, ToleranceType.Absolute);
        Transform transform = flattened.Transform ?? Transform.Identity;
        List<Edge> edges = [];
        SortedSet<double> rows = [];
        foreach (PathFigure figure in flattened.Figures)
        {
            System.Windows.Point start = transform.Transform(figure.StartPoint);
            System.Windows.Point previous = start;
            foreach (PathSegment segment in figure.Segments)
            {
                if (segment is PolyLineSegment polyline)
                {
                    foreach (System.Windows.Point point in polyline.Points)
                    {
                        System.Windows.Point transformed = transform.Transform(point);
                        AddEdge(previous, transformed);
                        previous = transformed;
                    }
                }
                else if (segment is LineSegment line)
                {
                    System.Windows.Point transformed = transform.Transform(line.Point);
                    AddEdge(previous, transformed);
                    previous = transformed;
                }
            }

            AddEdge(previous, start);
        }

        double[] rowPositions = rows.ToArray();
        List<Edge> crossings = [];
        for (int row = 0; row < rowPositions.Length - 1; row++)
        {
            double top = rowPositions[row];
            double bottom = rowPositions[row + 1];
            if (bottom - top < 0.000001)
            {
                continue;
            }

            double middle = (top + bottom) * 0.5;
            crossings.Clear();
            foreach (Edge edge in edges)
            {
                if (middle > edge.Top && middle < edge.Bottom)
                {
                    crossings.Add(edge);
                }
            }

            crossings.Sort((left, right) => left.XAt(middle).CompareTo(right.XAt(middle)));
            for (int crossing = 0; crossing + 1 < crossings.Count; crossing += 2)
            {
                Edge left = crossings[crossing];
                Edge right = crossings[crossing + 1];
                int columns = Math.Max(1, (int)Math.Ceiling((right.XAt(middle) - left.XAt(middle)) / sampleWidth));
                for (int column = 0; column < columns; column++)
                {
                    double firstFraction = (double)column / columns;
                    double secondFraction = (double)(column + 1) / columns;
                    System.Windows.Point upperLeft = new(left.XAt(top) + (right.XAt(top) - left.XAt(top)) * firstFraction, top);
                    System.Windows.Point upperRight = new(left.XAt(top) + (right.XAt(top) - left.XAt(top)) * secondFraction, top);
                    System.Windows.Point lowerLeft = new(left.XAt(bottom) + (right.XAt(bottom) - left.XAt(bottom)) * firstFraction, bottom);
                    System.Windows.Point lowerRight = new(left.XAt(bottom) + (right.XAt(bottom) - left.XAt(bottom)) * secondFraction, bottom);
                    AddTriangle(upperLeft, upperRight, lowerLeft);
                    AddTriangle(lowerLeft, upperRight, lowerRight);
                }
            }
        }

        void AddEdge(System.Windows.Point first, System.Windows.Point second)
        {
            if (Math.Abs(first.Y - second.Y) < 0.000001)
            {
                return;
            }

            edges.Add(new Edge(first, second));
            rows.Add(first.Y);
            rows.Add(second.Y);
        }

        void AddTriangle(System.Windows.Point first, System.Windows.Point second, System.Windows.Point third)
        {
            vertices.Add(new VertexPositionColor(new Vector3((float)first.X, (float)first.Y, 0), Premultiply(color(first))));
            vertices.Add(new VertexPositionColor(new Vector3((float)second.X, (float)second.Y, 0), Premultiply(color(second))));
            vertices.Add(new VertexPositionColor(new Vector3((float)third.X, (float)third.Y, 0), Premultiply(color(third))));
        }
    }

    private static Color Premultiply(Color color)
    {
        return Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
    }

    private readonly record struct Edge(System.Windows.Point First, System.Windows.Point Second)
    {
        public double Top => Math.Min(First.Y, Second.Y);
        public double Bottom => Math.Max(First.Y, Second.Y);

        public double XAt(double y)
        {
            return First.X + (Second.X - First.X) * ((y - First.Y) / (Second.Y - First.Y));
        }
    }
}
