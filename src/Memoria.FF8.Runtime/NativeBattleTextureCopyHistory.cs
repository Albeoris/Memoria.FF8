using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureOwnedSpan(int X, int Y, int WidthWords, int Height);

/// <summary>An original upload owner and its observed source-copy rectangle order.</summary>
public sealed record NativeBattleTextureRegistration(NativeBattleTextureOrigin Origin, int X, int Y, int WidthWords, int Height, ImmutableArray<NativeBattleEffectTextureRectangle> CopyFrames, int CurrentFrame = -1)
{
    public int OwnerId => X + Y * 1024;
    public bool Active { get; init; } = true;
    public bool CopyHistoryKnown { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<int>? CopyOrder { get; init; }
    public int ExactFrame => CopyHistoryKnown && PaletteOwnedSpans is not null ? CurrentFrame : -1;
    public ImmutableArray<NativeBattleTextureOwnedSpan>? PaletteOwnedSpans { get; init; }
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
        if (CopyOrder is { } order && (order.IsDefault || order.Length != CopyFrames.Length || order.Any(index => index < 0 || index >= CopyFrames.Length) || order.Distinct().Count() != order.Length))
        {
            throw new InvalidDataException("Native texture copy order requires every stable frame index exactly once.");
        }
        if (OwnedSpans.IsDefault)
        {
            throw new InvalidDataException("Native texture ownership requires explicit surviving word spans.");
        }
        if (PaletteOwnedSpans is { } paletteSpans)
        {
            if (paletteSpans.IsDefault)
            {
                throw new InvalidDataException("Native texture palette ownership requires explicit surviving word spans.");
            }
            foreach (NativeBattleTextureOwnedSpan span in paletteSpans)
            {
                NativeBattleTextureCopyHistory.ValidateRectangle(span.X, span.Y, span.WidthWords, span.Height);
                if (span.WidthWords == 0 || span.Height == 0)
                {
                    throw new InvalidDataException("Native texture palette ownership cannot contain empty spans.");
                }
            }
        }
        if (!Active && !OwnedSpans.IsEmpty)
        {
            throw new InvalidDataException("Retired native texture metadata cannot retain pixel ownership.");
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
    private static readonly ConditionalWeakTable<NativeBattleTextureRegistration[], object> ValidatedCollections = new();
    private static readonly object ValidationReceipt = new();
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
        ImmutableArray<NativeBattleTextureRegistration> owners = registrations.Value;
        bool unknownPaletteOwnership = false;
        foreach (NativeBattleTextureRegistration owner in owners)
        {
            if (owner.PaletteOwnedSpans is null)
            {
                unknownPaletteOwnership = true;
                break;
            }
        }
        if (unknownPaletteOwnership)
        {
            ImmutableArray<NativeBattleTextureRegistration>.Builder? changed = null;
            for (int index = 0; index < owners.Length; index++)
            {
                if (owners[index].CopyHistoryKnown || owners[index].CopyOrder is not null)
                {
                    changed ??= owners.ToBuilder();
                    changed[index] = owners[index] with { CopyHistoryKnown = false, CopyOrder = null };
                }
            }
            return changed?.ToImmutable() ?? registrations;
        }
        (int source, bool sourcePalette) = FindRole(registrations.Value, copy.SourceX, copy.SourceY);
        (int target, bool targetPalette) = FindRole(registrations.Value, copy.DestinationX, copy.DestinationY);
        bool paletteCopy = sourcePalette && targetPalette;
        if (source < 0 || !registrations.Value[source].Active || sourcePalette && !paletteCopy)
        {
            return registrations;
        }
        NativeBattleTextureRegistration observedSource = Observe(owners[source], copy);
        bool observeTarget = target >= 0 && target != source && owners[target].Active && (!targetPalette || paletteCopy);
        NativeBattleTextureRegistration? observedTarget = observeTarget ? Observe(owners[target], copy) : null;
        if (ReferenceEquals(observedSource, owners[source]) && (!observeTarget || ReferenceEquals(observedTarget, owners[target])))
        {
            return registrations;
        }
        var result = owners.ToBuilder();
        result[source] = observedSource;
        if (observeTarget)
        {
            result[target] = observedTarget!;
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
        NativeBattleTextureRegistration[] values = ImmutableCollectionsMarshal.AsArray(known)!;
        ValidatedCollections.GetValue(values, static registrations =>
        {
            ValidateCollection(registrations);
            return ValidationReceipt;
        });
    }

    private static void ValidateCollection(ReadOnlySpan<NativeBattleTextureRegistration> registrations)
    {
        HashSet<int> owners = [];
        List<NativeBattleTextureOwnedSpan> occupied = [];
        foreach (NativeBattleTextureRegistration registration in registrations)
        {
            registration.Validate();
            if (!owners.Add(registration.OwnerId))
            {
                throw new InvalidDataException("Native texture registrations contain duplicate upload owners.");
            }
            foreach (NativeBattleTextureOwnedSpan span in registration.OwnedSpans.Concat(registration.PaletteOwnedSpans ?? []))
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
            if (registration.Active && registration.OwnedSpans.Any(span => Contains(span, x, y)))
            {
                return index;
            }
        }
        return -1;
    }

    private static NativeBattleTextureRegistration Observe(NativeBattleTextureRegistration registration, NativeBattleEffectVramCopy copy)
    {
        for (int index = 0; index < registration.CopyFrames.Length; index++)
        {
            NativeBattleEffectTextureRectangle known = registration.CopyFrames[index];
            if (known.X == copy.SourceX && known.Y == copy.SourceY && known.Width == copy.Width && known.Height == copy.Height)
            {
                if (registration.CurrentFrame == index && (registration.CopyOrder is not { } currentOrder || currentOrder[^1] == index))
                {
                    return registration;
                }
                return registration with { CurrentFrame = index, CopyOrder = registration.CopyOrder is { } order ? order.Remove(index).Add(index) : null };
            }
        }
        NativeBattleEffectTextureRectangle frame = new(copy.SourceX, copy.SourceY, copy.Width, copy.Height);
        return registration with { CopyFrames = registration.CopyFrames.Add(frame), CurrentFrame = registration.CopyFrames.Length, CopyOrder = registration.CopyOrder is { } previous ? previous.Add(registration.CopyFrames.Length) : null };
    }

    private const int PaletteFlag = 1 << 30;
    private const int WordCount = 1024 * 512;

    private static bool Contains(NativeBattleTextureOwnedSpan span, int x, int y) => x >= span.X && x < span.X + span.WidthWords && y >= span.Y && y < span.Y + span.Height;

    private static (int Index, bool Palette) FindRole(ImmutableArray<NativeBattleTextureRegistration> registrations, int x, int y)
    {
        for (int index = registrations.Length - 1; index >= 0; index--)
        {
            NativeBattleTextureRegistration registration = registrations[index];
            if (registration.Active && ContainsAny(registration.OwnedSpans, x, y))
            {
                return (index, false);
            }
            if (registration.PaletteOwnedSpans is { } palette && ContainsAny(palette, x, y))
            {
                return (index, true);
            }
        }
        return (-1, false);
    }

    private static bool ContainsAny(ImmutableArray<NativeBattleTextureOwnedSpan> spans, int x, int y)
    {
        foreach (NativeBattleTextureOwnedSpan span in spans)
        {
            if (Contains(span, x, y))
            {
                return true;
            }
        }
        return false;
    }

    private static ImmutableArray<NativeBattleTextureRegistration> UploadSingle(ImmutableArray<NativeBattleTextureRegistration>? registrations, int x, int y, int width, int height, NativeBattleTextureOrigin? origin)
    {
        List<NativeBattleTextureRegistration> owners = (registrations ?? []).ToList();
        int[] words = ArrayPool<int>.Shared.Rent(WordCount);
        try
        {
            Array.Fill(words, -1, 0, WordCount);
            foreach (NativeBattleTextureRegistration owner in owners)
            {
                foreach (NativeBattleTextureOwnedSpan span in owner.OwnedSpans)
                {
                    Fill(span, owner.OwnerId);
                }
                foreach (NativeBattleTextureOwnedSpan span in owner.PaletteOwnedSpans ?? [])
                {
                    Fill(span, owner.OwnerId | PaletteFlag);
                }
            }
            int newId = x + y * 1024;
            Map(new(x, y, width, height), newId);
            if (origin?.PaletteRectangle is { } palette)
            {
                Map(new(palette.X, palette.Y, palette.Width, palette.Height), newId | PaletteFlag);
            }
            Dictionary<int, ImmutableArray<NativeBattleTextureOwnedSpan>.Builder> pixelSpans = [];
            Dictionary<int, ImmutableArray<NativeBattleTextureOwnedSpan>.Builder> paletteSpans = [];
            for (int row = 0; row < 512; row++)
            {
                int column = 0;
                while (column < 1024)
                {
                    int id = words[row * 1024 + column];
                    int start = column++;
                    while (column < 1024 && words[row * 1024 + column] == id)
                    {
                        column++;
                    }
                    if (id >= 0)
                    {
                        bool isPalette = (id & PaletteFlag) != 0;
                        AddSpan(isPalette ? paletteSpans : pixelSpans, id & ~PaletteFlag, new(start, row, column - start, 1));
                    }
                }
            }
            var result = ImmutableArray.CreateBuilder<NativeBattleTextureRegistration>();
            foreach (NativeBattleTextureRegistration owner in owners)
            {
                if (owner.OwnerId == newId)
                {
                    if (origin is null && owner.PaletteOwnedSpans is not null && !Spans(paletteSpans, owner.OwnerId).IsEmpty)
                    {
                        result.Add(owner with { Active = false, OwnedSpans = [], PaletteOwnedSpans = Spans(paletteSpans, owner.OwnerId) });
                    }
                    continue;
                }
                ImmutableArray<NativeBattleTextureOwnedSpan> pixels = Spans(pixelSpans, owner.OwnerId);
                ImmutableArray<NativeBattleTextureOwnedSpan>? palettes = owner.PaletteOwnedSpans is null ? null : Spans(paletteSpans, owner.OwnerId);
                if (owner.Active || palettes is null || !palettes.Value.IsEmpty)
                {
                    result.Add(owner with { OwnedSpans = pixels, PaletteOwnedSpans = palettes });
                }
            }
            if (origin is not null)
            {
                result.Add(new(origin, x, y, width, height, [])
                {
                    OwnedSpans = Spans(pixelSpans, newId),
                    PaletteOwnedSpans = Spans(paletteSpans, newId),
                    CopyHistoryKnown = true,
                    CopyOrder = []
                });
            }
            return result.ToImmutable();

            void Fill(NativeBattleTextureOwnedSpan span, int id)
            {
                for (int row = span.Y; row < span.Y + span.Height; row++)
                {
                    Array.Fill(words, id, row * 1024 + span.X, span.WidthWords);
                }
            }

            void Map(NativeBattleTextureOwnedSpan rectangle, int id)
            {
                for (int row = rectangle.Y; row < rectangle.Y + rectangle.Height; row++)
                {
                    for (int column = rectangle.X; column < rectangle.X + rectangle.WidthWords; column++)
                    {
                        int index = row * 1024 + column;
                        int previousId = words[index];
                        int previous = previousId < 0 || (previousId & PaletteFlag) != 0 ? -1 : owners.FindIndex(owner => owner.Active && owner.OwnerId == previousId);
                        if (previous >= 0)
                        {
                            NativeBattleTextureRegistration old = owners[previous];
                            // The loader retains metadata for uploads starting on the old rectangle's final word column or row.
                            bool retainedEdge = old.WidthWords >= 10 && rectangle.X + 1 == old.X + old.WidthWords || old.Height >= 10 && rectangle.Y + 1 == old.Y + old.Height;
                            if (!retainedEdge)
                            {
                                owners[previous] = old with { Active = false, OwnedSpans = [] };
                                Fill(new(old.X, old.Y, old.WidthWords, old.Height), -1);
                            }
                        }
                        words[index] = id;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(words);
        }
    }

    private static ImmutableArray<NativeBattleTextureOwnedSpan> Spans(Dictionary<int, ImmutableArray<NativeBattleTextureOwnedSpan>.Builder> spans, int ownerId) => spans.TryGetValue(ownerId, out var value) ? value.ToImmutable() : [];

    private static void AddSpan(Dictionary<int, ImmutableArray<NativeBattleTextureOwnedSpan>.Builder> spans, int ownerId, NativeBattleTextureOwnedSpan span)
    {
        if (!spans.TryGetValue(ownerId, out var owned))
        {
            owned = ImmutableArray.CreateBuilder<NativeBattleTextureOwnedSpan>();
            spans.Add(ownerId, owned);
        }
        if (owned.Count > 0 && owned[^1] is { } previous && previous.X == span.X && previous.WidthWords == span.WidthWords && previous.Y + previous.Height == span.Y)
        {
            owned[^1] = previous with { Height = previous.Height + span.Height };
        }
        else
        {
            owned.Add(span);
        }
    }
}
