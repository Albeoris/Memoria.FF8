using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Threading;

namespace Memoria.FF8.Runtime;

public enum NativeBattleSceneCaptureKind
{
    Source,
    Image,
    SourceEnvironment,
    ScreenSample,
    Copy
}

/// <summary>A scene copy and its screen image share a normalized domain independently of the native texture allocation.</summary>
public sealed record NativeBattleSceneCapture(BattleSceneVisibleArea Source, NativeBattleSceneCaptureKind Kind)
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; } = 320;
    public int Height { get; init; } = 216;
    public int PaddingTop { get; init; }
    public int PaddingBottom { get; init; }
    public int ProjectionOffsetY { get; init; }
    public ImmutableArray<BattleSceneScreenPoint>? SampleCoordinates { get; init; }
    public long? ReplaySourceId { get; init; }
    public int CopySourceX { get; init; }
    public int CopySourceY { get; init; }
    private static readonly AsyncLocal<NativeBattleSceneCapture?> Active = new();
    public static NativeBattleSceneCapture? Current => Active.Value;

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || !double.IsFinite(Source.Left) || !double.IsFinite(Source.Top) || !double.IsFinite(Source.Right) || !double.IsFinite(Source.Bottom) || Source.Right <= Source.Left || Source.Bottom <= Source.Top || X < 0 || Y < 0 || Width != 320 || Height > 256 || PaddingTop < 0 || PaddingBottom < 0 || Height - PaddingTop - PaddingBottom != 216 || ProjectionOffsetY != PaddingTop || X > 1024 - Width || Y > 512 - Height || SampleCoordinates is { } samples && (samples.IsDefault || samples.Length is < 3 or > 4))
        {
            throw new InvalidDataException("Scene capture requires a supported complete scene domain and bounded native texture rectangle.");
        }
    }

    public static IDisposable Begin(NativeBattleSceneCaptureKind kind, int x = 0, int y = 0, int height = 216, int paddingTop = 0, int paddingBottom = 0, int projectionOffsetY = 0)
    {
        return Begin(new(BattleSceneVisibleArea.Current, kind) { X = x, Y = y, Height = height, PaddingTop = paddingTop, PaddingBottom = paddingBottom, ProjectionOffsetY = projectionOffsetY });
    }

    public static IDisposable Begin(NativeBattleSceneCapture capture)
    {
        capture.Validate();
        NativeBattleSceneCapture? previous = Active.Value;
        Active.Value = capture;
        IDisposable? visibleArea = null;
        if (capture.Kind == NativeBattleSceneCaptureKind.Source && (capture.PaddingTop != 0 || capture.PaddingBottom != 0 || capture.ProjectionOffsetY != 0))
        {
            BattleSceneVisibleArea source = Active.Value.Source;
            double visibleHeight = source.Bottom - source.Top;
            visibleArea = BattleSceneVisibleArea.Begin(source with { Top = source.Top + capture.ProjectionOffsetY * 8 - capture.PaddingTop * visibleHeight / 216, Bottom = source.Bottom + capture.ProjectionOffsetY * 8 + capture.PaddingBottom * visibleHeight / 216 });
        }
        return new Scope(previous, visibleArea);
    }

    internal void MapSourcePacket(NativeBattleLogicalCommand command, Span<byte> packet)
    {
        Validate();
        if (Kind is not (NativeBattleSceneCaptureKind.Source or NativeBattleSceneCaptureKind.SourceEnvironment) || Source == BattleSceneVisibleArea.Native || (command.Opcode & 0xE0) != 0x20)
        {
            return;
        }
        if (Source.Right <= Source.Left || Source.Bottom <= Source.Top)
        {
            throw new InvalidDataException("Scene capture requires a positive visible source rectangle.");
        }
        bool pixels = (command.Flags20 & 1) != 0;
        int step = 4 + ((command.Opcode & 4) != 0 ? 4 : 0) + ((command.Opcode & 16) != 0 ? 4 : 0);
        int count = (command.Opcode & 8) != 0 ? 4 : 3;
        ImmutableArray<BattleSceneScreenPoint>? coordinates = command.ProjectedCoordinates;
        for (int corner = 0; corner < count; corner++)
        {
            int offset = 8 + corner * step;
            double x = coordinates is { } points ? points[corner].X : BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(offset, 2)) * (pixels ? 8d : 1d);
            double y = coordinates is { } values ? values[corner].Y : BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(offset + 2, 2)) * (pixels ? 8d : 1d);
            double scale = pixels ? 8d : 1d;
            short mappedX = checked((short)Math.Clamp(Math.Round((x - Source.Left) * 2560 / (Source.Right - Source.Left) / scale), short.MinValue, short.MaxValue));
            short mappedY = checked((short)Math.Clamp(Math.Round((PaddingTop * 8 + (y - ProjectionOffsetY * 8 - Source.Top) * 1728 / (Source.Bottom - Source.Top)) / scale), short.MinValue, short.MaxValue));
            BinaryPrimitives.WriteInt16LittleEndian(packet.Slice(offset, 2), mappedX);
            BinaryPrimitives.WriteInt16LittleEndian(packet.Slice(offset + 2, 2), mappedY);
        }
    }

    private sealed class Scope(NativeBattleSceneCapture? previous, IDisposable? visibleArea) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                Active.Value = previous;
                visibleArea?.Dispose();
                disposed = true;
            }
        }
    }
}

/// <summary>Ordered retained pages for a full scene source, including copies wider than one native texture page.</summary>
public sealed record NativeBattleSceneCaptureSample(NativeBattleSceneCapture Capture, ImmutableArray<int> PixelSnapshots)
{
    public ImmutableArray<ushort> Palette { get; init; } = [];
}
