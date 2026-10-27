using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureOwnedSpan(int X, int Y, int WidthWords, int Height);

/// <summary>An original upload owner and its observed source-copy rectangle order.</summary>
public sealed record NativeBattleTextureRegistration(NativeBattleTextureOrigin Origin, int X, int Y, int WidthWords, int Height, ImmutableArray<NativeBattleEffectTextureRectangle> CopyFrames, int CurrentFrame = -1)
{
    public int OwnerId => X + Y * 1024;
    public ImmutableArray<NativeBattleTextureOwnedSpan> OwnedSpans { get; init; } = [new(X, Y, WidthWords, Height)];

    public void Validate()
    {
        Origin.Validate();
        NativeBattleTextureCopyHistory.ValidateRectangle(X, Y, WidthWords, Height);
        if (Origin.Role != NativeBattleTextureRole.Pixels || Origin.WidthWords != WidthWords || Origin.Height != Height || WidthWords == 0 || Height == 0 || CopyFrames.IsDefault || CurrentFrame < -1 || CurrentFrame >= CopyFrames.Length || CopyFrames.IsEmpty && CurrentFrame != -1 || CopyFrames.Distinct().Count() != CopyFrames.Length)
        {
            throw new InvalidDataException("Native texture copy history requires a bounded pixel upload owner and unique observed frames.");
        }
        foreach (NativeBattleEffectTextureRectangle frame in CopyFrames)
        {
            NativeBattleTextureCopyHistory.ValidateRectangle(frame.X, frame.Y, frame.Width, frame.Height);
            if (frame.Width == 0 || frame.Height == 0)
            {
                throw new InvalidDataException("Native texture copy history cannot contain empty copy frames.");
            }
        }
        if (OwnedSpans.IsDefault)
        {
            throw new InvalidDataException("Native texture ownership requires explicit surviving word spans.");
        }
        foreach (NativeBattleTextureOwnedSpan span in OwnedSpans)
        {
            NativeBattleTextureCopyHistory.ValidateRectangle(span.X, span.Y, span.WidthWords, span.Height);
            if (span.WidthWords == 0 || span.Height == 0 || span.X < X || span.Y < Y || span.X + span.WidthWords > X + WidthWords || span.Y + span.Height > Y + Height)
            {
                throw new InvalidDataException("Native texture ownership span leaves its original upload rectangle.");
            }
        }
    }
}

/// <summary>Tracks upload ownership independently of copied pixel provenance.</summary>
public static class NativeBattleTextureCopyHistory
{
    public static ImmutableArray<NativeBattleTextureRegistration>? Upload(ImmutableArray<NativeBattleTextureRegistration>? registrations, int x, int y, int width, int height, NativeBattleTextureOrigin? origin = null)
    {
        Validate(registrations);
        ValidateRectangle(x, y, width, height);
        origin?.Validate();
        if (origin is not null && (origin.Role != NativeBattleTextureRole.Pixels || origin.WidthWords != width || origin.Height != height))
        {
            throw new InvalidDataException("Native texture copy registration differs from its pixel upload canvas.");
        }
        if (width == 0 || height == 0)
        {
            return registrations;
        }
        if (registrations is null && origin is null)
        {
            return null;
        }
        if (origin is not null && NativeBattleSharedEffectTexture.IsCommonImage(origin))
        {
            for (int page = 0; page < 3; page++)
            {
                NativeBattleTextureOrigin part = origin with { Owner = $"a8def{page}.tim", WidthWords = 64, Kind = NativeBattleTextureSourceKind.Image };
                registrations = UploadSingle(registrations, x + page * 64, y, 64, height, part);
            }
            return registrations;
        }
        return UploadSingle(registrations, x, y, width, height, origin);
    }

    public static ImmutableArray<NativeBattleTextureRegistration>? Copy(ImmutableArray<NativeBattleTextureRegistration>? registrations, NativeBattleEffectVramCopy copy)
    {
        Validate(registrations);
        ValidateRectangle(copy.SourceX, copy.SourceY, copy.Width, copy.Height);
        ValidateRectangle(copy.DestinationX, copy.DestinationY, copy.Width, copy.Height);
        if (registrations is null || copy.Width == 0 || copy.Height == 0)
        {
            return registrations;
        }
        int source = FindIndex(registrations.Value, copy.SourceX, copy.SourceY);
        if (source < 0)
        {
            return registrations;
        }
        int target = FindIndex(registrations.Value, copy.DestinationX, copy.DestinationY);
        NativeBattleEffectTextureRectangle frame = new(copy.SourceX, copy.SourceY, copy.Width, copy.Height);
        var result = registrations.Value.ToBuilder();
        result[source] = Observe(result[source], frame);
        if (target >= 0 && target != source)
        {
            result[target] = Observe(result[target], frame);
        }
        return result.ToImmutable();
    }

    public static NativeBattleTextureRegistration? Find(ImmutableArray<NativeBattleTextureRegistration>? registrations, int x, int y)
    {
        Validate(registrations);
        ValidateRectangle(x, y, 1, 1);
        if (registrations is null)
        {
            return null;
        }
        int index = FindIndex(registrations.Value, x, y);
        return index < 0 ? null : registrations.Value[index];
    }

    public static void Validate(ImmutableArray<NativeBattleTextureRegistration>? registrations)
    {
        if (registrations is not { } known)
        {
            return;
        }
        if (known.IsDefault)
        {
            throw new InvalidDataException("Native texture registrations require an explicit owner collection.");
        }
        HashSet<int> owners = [];
        List<NativeBattleTextureOwnedSpan> occupied = [];
        foreach (NativeBattleTextureRegistration registration in known)
        {
            registration.Validate();
            if (!owners.Add(registration.OwnerId))
            {
                throw new InvalidDataException("Native texture registrations contain duplicate upload owners.");
            }
            foreach (NativeBattleTextureOwnedSpan span in registration.OwnedSpans)
            {
                if (occupied.Any(other => span.X < other.X + other.WidthWords && other.X < span.X + span.WidthWords && span.Y < other.Y + other.Height && other.Y < span.Y + span.Height))
                {
                    throw new InvalidDataException("Native texture registrations assign the same VRAM words more than once.");
                }
                occupied.Add(span);
            }
        }
    }

    internal static void ValidateRectangle(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width < 0 || height < 0 || x > 1024 || y > 512 || width > 1024 - x || height > 512 - y)
        {
            throw new InvalidDataException("Native texture copy metadata leaves its 1024x512 word VRAM.");
        }
    }

    private static int FindIndex(ImmutableArray<NativeBattleTextureRegistration> registrations, int x, int y)
    {
        for (int index = registrations.Length - 1; index >= 0; index--)
        {
            NativeBattleTextureRegistration registration = registrations[index];
            if (registration.OwnedSpans.Any(span => x >= span.X && x < span.X + span.WidthWords && y >= span.Y && y < span.Y + span.Height))
            {
                return index;
            }
        }
        return -1;
    }

    private static NativeBattleTextureRegistration Observe(NativeBattleTextureRegistration registration, NativeBattleEffectTextureRectangle frame)
    {
        int index = registration.CopyFrames.IndexOf(frame);
        if (index >= 0)
        {
            return registration with { CurrentFrame = index };
        }
        return registration with { CopyFrames = registration.CopyFrames.Add(frame), CurrentFrame = registration.CopyFrames.Length };
    }

    private static ImmutableArray<NativeBattleTextureRegistration> UploadSingle(ImmutableArray<NativeBattleTextureRegistration>? registrations, int x, int y, int width, int height, NativeBattleTextureOrigin? origin)
    {
        List<NativeBattleTextureRegistration> owners = (registrations ?? []).ToList();
        int[] words = new int[checked(width * height)];
        Array.Fill(words, -1);
        foreach (NativeBattleTextureRegistration owner in owners)
        {
            foreach (NativeBattleTextureOwnedSpan span in owner.OwnedSpans)
            {
                int left = Math.Max(x, span.X);
                int right = Math.Min(x + width, span.X + span.WidthWords);
                for (int row = Math.Max(y, span.Y); row < Math.Min(y + height, span.Y + span.Height); row++)
                {
                    if (left < right)
                    {
                        Array.Fill(words, owner.OwnerId, (row - y) * width + left - x, right - left);
                    }
                }
            }
        }
        int newId = x + y * 1024;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int index = row * width + column;
                int oldId = words[index];
                int oldIndex = oldId < 0 ? -1 : owners.FindIndex(owner => owner.OwnerId == oldId);
                if (oldIndex >= 0)
                {
                    NativeBattleTextureRegistration old = owners[oldIndex];
                    // The loader retains metadata for uploads starting on the old rectangle's final word column or row.
                    bool retainedEdge = old.WidthWords >= 10 && x + 1 == old.X + old.WidthWords || old.Height >= 10 && y + 1 == old.Y + old.Height;
                    if (!retainedEdge)
                    {
                        owners.RemoveAt(oldIndex);
                        NativeBattleTextureOwnedSpan cleared = new(old.X, old.Y, old.WidthWords, old.Height);
                        for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                        {
                            owners[ownerIndex] = owners[ownerIndex] with { OwnedSpans = Subtract(owners[ownerIndex].OwnedSpans, cleared) };
                        }
                        int left = Math.Max(x, old.X);
                        int right = Math.Min(x + width, old.X + old.WidthWords);
                        for (int clearedRow = Math.Max(y, old.Y); clearedRow < Math.Min(y + height, old.Y + old.Height); clearedRow++)
                        {
                            if (left < right)
                            {
                                Array.Fill(words, -1, (clearedRow - y) * width + left - x, right - left);
                            }
                        }
                    }
                }
                words[index] = newId;
            }
        }
        NativeBattleTextureOwnedSpan uploaded = new(x, y, width, height);
        var result = ImmutableArray.CreateBuilder<NativeBattleTextureRegistration>();
        foreach (NativeBattleTextureRegistration owner in owners)
        {
            if (owner.OwnerId != newId)
            {
                result.Add(owner with { OwnedSpans = Subtract(owner.OwnedSpans, uploaded) });
            }
        }
        if (origin is not null)
        {
            var spans = ImmutableArray.CreateBuilder<NativeBattleTextureOwnedSpan>();
            for (int row = 0; row < height; row++)
            {
                int column = 0;
                while (column < width)
                {
                    if (words[row * width + column] != newId)
                    {
                        column++;
                        continue;
                    }
                    int start = column++;
                    while (column < width && words[row * width + column] == newId)
                    {
                        column++;
                    }
                    if (spans.Count > 0 && spans[^1] is { } previous && previous.X == x + start && previous.WidthWords == column - start && previous.Y + previous.Height == y + row)
                    {
                        spans[^1] = previous with { Height = previous.Height + 1 };
                    }
                    else
                    {
                        spans.Add(new(x + start, y + row, column - start, 1));
                    }
                }
            }
            result.Add(new(origin, x, y, width, height, []) { OwnedSpans = spans.ToImmutable() });
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<NativeBattleTextureOwnedSpan> Subtract(ImmutableArray<NativeBattleTextureOwnedSpan> spans, NativeBattleTextureOwnedSpan removed)
    {
        var result = ImmutableArray.CreateBuilder<NativeBattleTextureOwnedSpan>();
        foreach (NativeBattleTextureOwnedSpan span in spans)
        {
            int left = Math.Max(span.X, removed.X);
            int top = Math.Max(span.Y, removed.Y);
            int right = Math.Min(span.X + span.WidthWords, removed.X + removed.WidthWords);
            int bottom = Math.Min(span.Y + span.Height, removed.Y + removed.Height);
            if (left >= right || top >= bottom)
            {
                result.Add(span);
                continue;
            }
            Add(span.X, span.Y, span.WidthWords, top - span.Y);
            Add(span.X, bottom, span.WidthWords, span.Y + span.Height - bottom);
            Add(span.X, top, left - span.X, bottom - top);
            Add(right, top, span.X + span.WidthWords - right, bottom - top);
        }
        return result.ToImmutable();

        void Add(int x, int y, int width, int height)
        {
            if (width > 0 && height > 0)
            {
                result.Add(new(x, y, width, height));
            }
        }
    }
}
