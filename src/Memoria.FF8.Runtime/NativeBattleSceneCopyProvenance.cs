using System.Collections.Immutable;
using System.Buffers;

namespace Memoria.FF8.Runtime;

/// <summary>Presentation ownership of words copied from a proved scene framebuffer source.</summary>
public sealed record NativeBattleSceneCopyRegion(NativeBattleSceneCapture Capture, int X, int Y, int Width, int Height, int SourceX, int SourceY)
{
    public void Validate()
    {
        Capture.Validate();
        if (Capture.Kind != NativeBattleSceneCaptureKind.Copy || Capture.ReplaySourceId is null || X < 0 || Y < 0 || Width <= 0 || Height <= 0 || X > 1024 - Width || Y > 512 - Height || SourceX < 0 || SourceY < 0 || SourceX > Capture.Width - Width || SourceY > Capture.Height - Height)
        {
            throw new InvalidDataException("Scene copy provenance requires a complete bounded destination and source region.");
        }
    }
}

public static class NativeBattleSceneCopyProvenance
{
    public static ImmutableArray<NativeBattleSceneCopyRegion>? OverwriteBytes(ImmutableArray<NativeBattleSceneCopyRegion>? regions, int offset, int length)
    {
        if (regions is null || length == 0)
        {
            return regions;
        }
        int first = offset / 2;
        int end = checked((offset + length + 1) / 2);
        while (first < end)
        {
            int count = Math.Min(end - first, 1024 - first % 1024);
            regions = Overwrite(regions, first % 1024, first / 1024, count, 1);
            first += count;
        }
        return regions;
    }

    public static ImmutableArray<NativeBattleSceneCopyRegion>? Copy(ImmutableArray<NativeBattleSceneCopyRegion>? regions, NativeBattleEffectVramCopy copy)
    {
        if (regions is null || copy.Width <= 0 || copy.Height <= 0)
        {
            return regions;
        }
        if (Intersect(regions, copy.SourceX, copy.SourceY, copy.Width, copy.Height) is null)
        {
            return Overwrite(regions, copy.DestinationX, copy.DestinationY, copy.Width, copy.Height);
        }
        const int words = 1024 * 512;
        int[] owners = ArrayPool<int>.Shared.Rent(words);
        int[] sourceX = ArrayPool<int>.Shared.Rent(words);
        int[] sourceY = ArrayPool<int>.Shared.Rent(words);
        Array.Clear(owners, 0, words);
        List<NativeBattleSceneCapture> captures = [];
        Dictionary<NativeBattleSceneCapture, int> captureIndices = [];
        try
        {
            foreach (NativeBattleSceneCopyRegion region in regions.Value)
            {
                region.Validate();
                if (!captureIndices.TryGetValue(region.Capture, out int capture))
                {
                    capture = captures.Count + 1;
                    captureIndices.Add(region.Capture, capture);
                    captures.Add(region.Capture);
                }
                for (int y = 0; y < region.Height; y++)
                {
                    for (int x = 0; x < region.Width; x++)
                    {
                        int word = (region.Y + y) * 1024 + region.X + x;
                        owners[word] = capture;
                        sourceX[word] = region.SourceX + x;
                        sourceY[word] = region.SourceY + y;
                    }
                }
            }
            for (int y = 0; y < copy.Height; y++)
            {
                for (int x = 0; x < copy.Width; x += 2)
                {
                    int count = Math.Min(2, copy.Width - x);
                    int source = (copy.SourceY + y) * 1024 + copy.SourceX + x;
                    int destination = (copy.DestinationY + y) * 1024 + copy.DestinationX + x;
                    int firstOwner = owners[source];
                    int firstX = sourceX[source];
                    int firstY = sourceY[source];
                    int secondOwner = count == 2 ? owners[source + 1] : 0;
                    int secondX = count == 2 ? sourceX[source + 1] : 0;
                    int secondY = count == 2 ? sourceY[source + 1] : 0;
                    owners[destination] = firstOwner;
                    sourceX[destination] = firstX;
                    sourceY[destination] = firstY;
                    if (count == 2)
                    {
                        owners[destination + 1] = secondOwner;
                        sourceX[destination + 1] = secondX;
                        sourceY[destination + 1] = secondY;
                    }
                }
            }
            List<NativeBattleSceneCopyRegion> result = [];
            Dictionary<(int Owner, int X, int Width, int Dx, int Dy), int> previous = [];
            for (int y = 0; y < 512; y++)
            {
                Dictionary<(int Owner, int X, int Width, int Dx, int Dy), int> current = [];
                for (int x = 0; x < 1024;)
                {
                    int word = y * 1024 + x;
                    int owner = owners[word];
                    if (owner == 0)
                    {
                        x++;
                        continue;
                    }
                    int width = 1;
                    while (x + width < 1024 && owners[word + width] == owner && sourceX[word + width] == sourceX[word] + width && sourceY[word + width] == sourceY[word])
                    {
                        width++;
                    }
                    var key = (owner, x, width, sourceX[word] - x, sourceY[word] - y);
                    int index;
                    if (previous.TryGetValue(key, out index))
                    {
                        result[index] = result[index] with { Height = result[index].Height + 1 };
                    }
                    else
                    {
                        index = result.Count;
                        result.Add(new(captures[owner - 1], x, y, width, 1, sourceX[word], sourceY[word]));
                    }
                    current[key] = index;
                    x += width;
                }
                previous = current;
            }
            return result.Count == 0 ? null : result.ToImmutableArray();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(owners);
            ArrayPool<int>.Shared.Return(sourceX);
            ArrayPool<int>.Shared.Return(sourceY);
        }
    }

    public static ImmutableArray<NativeBattleSceneCopyRegion>? Overwrite(ImmutableArray<NativeBattleSceneCopyRegion>? regions, int x, int y, int width, int height)
    {
        if (regions is null || width <= 0 || height <= 0)
        {
            return regions;
        }
        ImmutableArray<NativeBattleSceneCopyRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleSceneCopyRegion>();
        foreach (NativeBattleSceneCopyRegion region in regions.Value)
        {
            region.Validate();
            int left = Math.Max(x, region.X);
            int top = Math.Max(y, region.Y);
            int right = Math.Min(x + width, region.X + region.Width);
            int bottom = Math.Min(y + height, region.Y + region.Height);
            if (left >= right || top >= bottom)
            {
                result.Add(region);
                continue;
            }
            Add(region.X, region.Y, region.Width, top - region.Y);
            Add(region.X, bottom, region.Width, region.Y + region.Height - bottom);
            Add(region.X, top, left - region.X, bottom - top);
            Add(right, top, region.X + region.Width - right, bottom - top);

            void Add(int px, int py, int w, int h)
            {
                if (w > 0 && h > 0)
                {
                    result.Add(region with { X = px, Y = py, Width = w, Height = h, SourceX = region.SourceX + px - region.X, SourceY = region.SourceY + py - region.Y });
                }
            }
        }
        return result.Count == 0 ? null : result.ToImmutable();
    }

    public static ImmutableArray<NativeBattleSceneCopyRegion>? Intersect(ImmutableArray<NativeBattleSceneCopyRegion>? regions, int x, int y, int width, int height)
    {
        if (regions is null)
        {
            return null;
        }
        ImmutableArray<NativeBattleSceneCopyRegion> selected = regions.Value.Where(region => region.X < x + width && x < region.X + region.Width && region.Y < y + height && y < region.Y + region.Height).ToImmutableArray();
        return selected.IsEmpty ? null : selected;
    }

    public static ImmutableDictionary<long, NativeBattleSceneReplaySource> PruneSources(NativeBattlePrimitiveFrame frame, NativeBattleEffectTextureState textures, NativeBattlePcTextureWindowCache? textureWindows = null)
    {
        ImmutableDictionary<long, NativeBattleSceneReplaySource> pool = frame.SceneReplaySources ?? ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty;
        HashSet<long> completed = [];
        HashSet<long> active = [];
        long retainedBytes = 0;
        foreach (long? id in frame.SceneSources ?? [])
        {
            if (id is long value)
            {
                Visit(value);
            }
        }
        VisitFrame(NativeBattlePreparedPrimitiveFrame.From(frame));
        VisitRegions(textures.SceneCopies);
        foreach (long id in frame.Pending.ReferencedSceneReplaySources())
        {
            Visit(id);
        }
        foreach (NativeBattlePcTextureWindowRow row in textureWindows?.Rows ?? [])
        {
            foreach (NativeBattlePcTextureWindowRegion region in row.Regions)
            {
                VisitRegions(region.RetainedPixels?.SceneCopies);
            }
        }
        return pool.Where(pair => completed.Contains(pair.Key)).ToImmutableDictionary();

        void Visit(long id)
        {
            if (completed.Contains(id))
            {
                return;
            }
            if (!active.Add(id))
            {
                throw new InvalidDataException("Scene replay provenance contains a cycle.");
            }
            if (!pool.TryGetValue(id, out NativeBattleSceneReplaySource? source))
            {
                throw new InvalidDataException("Scene replay provenance refers to a missing retained source.");
            }
            retainedBytes = checked(retainedBytes + 1024L * 512 * 2 + (source.Frame.LastTextureSnapshots?.Sum(snapshot => (long)snapshot.Data.Length) ?? 0));
            if (active.Count + completed.Count > 64 || retainedBytes > 1024L * 1024 * 1024)
            {
                throw new NotSupportedException("Scene replay provenance exceeds its bounded retained source budget.");
            }
            VisitFrame(source.Frame);
            VisitRegions(source.Textures.SceneCopies);
            active.Remove(id);
            completed.Add(id);
        }

        void VisitFrame(NativeBattlePreparedPrimitiveFrame value)
        {
            foreach (NativeBattleLogicalResolvedCommand command in value.LastCommands)
            {
                if (command.Command.SceneCapture?.ReplaySourceId is long id)
                {
                    Visit(id);
                }
            }
            foreach (NativeBattlePcTextureWindowPixels pixels in value.LastTextureSnapshots ?? [])
            {
                VisitRegions(pixels.SceneCopies);
            }
        }

        void VisitRegions(ImmutableArray<NativeBattleSceneCopyRegion>? regions)
        {
            foreach (NativeBattleSceneCopyRegion region in regions ?? [])
            {
                region.Validate();
                Visit(region.Capture.ReplaySourceId!.Value);
            }
        }
    }
}
