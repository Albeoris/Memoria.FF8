using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The original indexed/direct texture words retained by a cached PC region before VRAM invalidation.</summary>
public sealed record NativeBattlePcTextureWindowPixels([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height, [property: JsonRequired] ImmutableArray<byte> Data)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<bool>? Known { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleTextureSourceRegion>? LiveTextureSources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleTextureRegistration>? TextureRegistrations { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleSceneCopyRegion>? SceneCopies { get; init; }
    private static readonly ConditionalWeakTable<object, CaptureBucket> Captures = new();

    private sealed class CaptureBucket
    {
        private const int MaximumCaptures = 128;
        private const int MaximumBytes = 16 * 1024 * 1024;
        private readonly object gate = new();
        private readonly Dictionary<(int Row, NativeBattlePrimitiveTextureWindow Rectangle), CaptureEntry> captures = [];
        private readonly record struct CaptureEntry(NativeBattlePcTextureWindowPixels Pixels, long Generation);
        private object? publication;
        private long generation;
        private ImmutableArray<NativeBattleTextureSourceRegion>? sources;
        private ImmutableArray<NativeBattleTextureRegistration>? registrations;
        private ImmutableArray<byte>? tail;
        private ImmutableArray<NativeBattleSceneCopyRegion>? sceneCopies;
        private bool initialized;
        private int bytes;

        internal NativeBattlePcTextureWindowPixels Capture(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
        {
            lock (gate)
            {
                if (!initialized || !SameArray(sources, textures.LiveTextureSources) || !SameArray(registrations, textures.TextureRegistrations) || !SameArray(tail, textures.VramReadTail) || !SameArray(sceneCopies, textures.SceneCopies))
                {
                    captures.Clear();
                    bytes = 0;
                    sources = textures.LiveTextureSources;
                    registrations = textures.TextureRegistrations;
                    tail = textures.VramReadTail;
                    sceneCopies = textures.SceneCopies;
                    initialized = true;
                }
                object currentPublication = textures.VramPublicationIdentity;
                if (!ReferenceEquals(publication, currentPublication))
                {
                    publication = currentPublication;
                    if (generation == long.MaxValue)
                    {
                        captures.Clear();
                        bytes = 0;
                        generation = 0;
                    }
                    generation++;
                }
                (int Row, NativeBattlePrimitiveTextureWindow Rectangle) key = (row, rectangle);
                bool found = captures.TryGetValue(key, out CaptureEntry cached);
                if (found && (cached.Generation == generation || HasSameWords(cached.Pixels, textures)))
                {
                    if (cached.Generation != generation)
                    {
                        captures[key] = new(cached.Pixels, generation);
                    }
                    return cached.Pixels;
                }
                NativeBattlePcTextureWindowPixels result = CaptureUncached(row, rectangle, textures);
                int replacedBytes = found ? RetainedBytes(cached.Pixels) : 0;
                int resultBytes = RetainedBytes(result);
                if (resultBytes > MaximumBytes)
                {
                    if (found)
                    {
                        captures.Remove(key);
                        bytes -= replacedBytes;
                    }
                    return result;
                }
                // One bucket covers all96 full-page rows and a bounded set of additional windows.
                // Eviction drops only cache references; published pixel owners remain immutable.
                if ((!found && captures.Count >= MaximumCaptures) || bytes - replacedBytes > MaximumBytes - resultBytes)
                {
                    captures.Clear();
                    bytes = 0;
                    replacedBytes = 0;
                }
                captures[key] = new(result, generation);
                bytes += resultBytes - replacedBytes;
                return result;
            }
        }

        private static bool SameArray<T>(ImmutableArray<T>? first, ImmutableArray<T>? second) => first.HasValue == second.HasValue && (!first.HasValue || first.GetValueOrDefault().Equals(second.GetValueOrDefault()));

        private static int RetainedBytes(NativeBattlePcTextureWindowPixels pixels) => pixels.Data.Length + (pixels.Known?.Length ?? 0);
    }

    private static readonly ConditionalWeakTable<NativeBattlePcTextureWindowPixels, object> ValidatedPixels = new();
    private static readonly object ValidationReceipt = new();

    public void Validate()
    {
        ValidatedPixels.GetValue(this, static pixels =>
        {
            pixels.ValidateCore();
            return ValidationReceipt;
        });
    }

    private void ValidateCore()
    {
        NativeBattleTextureCopyHistory.Validate(TextureRegistrations);
        if (Known is { } known && (known.IsDefault || known.Length != Data.Length))
        {
            throw new InvalidDataException("Native cached texture knownness must cover its exact retained bytes.");
        }
        if (X is < 0 or >= 1024 || Y is < 0 or >= 512 || Width is < 1 or > 256 || Height is < 1 or > 256 || Y + Height > 512 || 2 * ((Y + Height - 1) * 1024 + X + Width) > NativeBattleEffectArena.Length + NativeBattleEffectTextureState.PcPageReadTailLength || Data.IsDefault || Data.Length != 2 * Width * Height)
        {
            throw new InvalidDataException("Native cached texture words require their exact bounded physical VRAM rectangle and bytes.");
        }
        if (LiveTextureSources is { } sources)
        {
            if (sources.IsDefault)
            {
                throw new InvalidDataException("Native cached texture provenance requires explicit retained source regions.");
            }
            foreach (NativeBattleTextureSourceRegion source in sources)
            {
                source.Validate();
            }
        }
    }

    public ushort ReadWord(int x, int y)
    {
        if (x < X || x >= X + Width || y < Y || y >= Y + Height)
        {
            throw new InvalidDataException("Native cached texture sample leaves its retained pixel-word rectangle.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(ReadKnownSpan(2 * ((y - Y) * Width + x - X), 2));
    }

    public ReadOnlySpan<byte> ReadKnownSpan(int offset, int length)
    {
        Validate();
        ReadOnlySpan<byte> bytes = Data.AsSpan(offset, length);
        if (Known is { } known && known.AsSpan(offset, length).Contains(false))
        {
            throw new NotSupportedException("Native cached texture sample intersects unknown physical bytes.");
        }
        return bytes;
    }

    internal bool HasSameStoredPixels(NativeBattlePcTextureWindowPixels other)
    {
        Validate();
        other.Validate();
        if (X != other.X || Y != other.Y || Width != other.Width || Height != other.Height || !Data.AsSpan().SequenceEqual(other.Data.AsSpan()))
        {
            return false;
        }
        if (Known is { } known)
        {
            return other.Known is { } otherKnown ? known.AsSpan().SequenceEqual(otherKnown.AsSpan()) : !known.AsSpan().Contains(false);
        }
        return other.Known is not { } mask || !mask.AsSpan().Contains(false);
    }

    public void ValidateMapping(int row, NativeBattlePrimitiveTextureWindow rectangle)
    {
        Validate();
        int pixelsPerWord = 4 >> (row / 32);
        if (X != (row & 15) * 64 + rectangle.X / pixelsPerWord || Y != ((row & 31) >> 4) * 256 + rectangle.Y || Width != (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord || Height != rectangle.Height)
        {
            throw new InvalidDataException("Native cached pixel words disagree with their page-depth row and crop.");
        }
    }

    public static NativeBattlePcTextureWindowPixels Capture(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(textures);
        CaptureBucket captures = Captures.GetValue(textures.VramCaptureIdentity, static _ => new());
        return captures.Capture(row, rectangle, textures);
    }

    private static bool HasSameWords(NativeBattlePcTextureWindowPixels captured, NativeBattleEffectTextureState textures)
    {
        Span<byte> row = stackalloc byte[512];
        Span<bool> known = stackalloc bool[512];
        int count = 2 * captured.Width;
        for (int line = 0; line < captured.Height; line++)
        {
            int address = 2 * ((captured.Y + line) * 1024 + captured.X);
            ReadRow(textures, address, row[..count], known[..count]);
            if (!row[..count].SequenceEqual(captured.Data.AsSpan(line * count, count)))
            {
                return false;
            }
            if (captured.Known is { } mask ? !known[..count].SequenceEqual(mask.AsSpan(line * count, count)) : known[..count].Contains(false))
            {
                return false;
            }
        }
        return true;
    }

    private static void ReadRow(NativeBattleEffectTextureState textures, int address, Span<byte> destination, Span<bool> known)
    {
        int count = Math.Min(destination.Length, NativeBattleEffectArena.Length - address);
        if (count > 0)
        {
            textures.ReadVramWithKnownness(address, destination[..count], known[..count]);
        }
        if (count < destination.Length)
        {
            ImmutableArray<byte> tail = textures.VramReadTail ?? throw new NotSupportedException("Native full-page capture reaches its unobserved trailing read window; explicitly migrate the standalone state.");
            tail.AsSpan().Slice(address + count - NativeBattleEffectArena.Length, destination.Length - count).CopyTo(destination[count..]);
            known[count..].Fill(true);
        }
    }

    private static NativeBattlePcTextureWindowPixels CaptureUncached(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        if (row is < 0 or >= 96)
        {
            throw new InvalidDataException("Native cached texture capture requires its actual page-depth row.");
        }
        int pixelsPerWord = 4 >> (row / 32);
        int x = (row & 15) * 64 + rectangle.X / pixelsPerWord;
        int y = ((row & 31) >> 4) * 256 + rectangle.Y;
        int width = (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord;
        byte[] data = new byte[2 * width * rectangle.Height];
        bool[] known = new bool[data.Length];
        for (int line = 0; line < rectangle.Height; line++)
        {
            int address = 2 * ((y + line) * 1024 + x);
            int count = 2 * width;
            ReadRow(textures, address, data.AsSpan(2 * line * width, count), known.AsSpan(2 * line * width, count));
        }
        NativeBattlePcTextureWindowPixels result = new(x, y, width, rectangle.Height, ImmutableCollectionsMarshal.AsImmutableArray(data)) { Known = known.AsSpan().Contains(false) ? ImmutableCollectionsMarshal.AsImmutableArray(known) : null, LiveTextureSources = textures.LiveTextureSources, TextureRegistrations = textures.TextureRegistrations, SceneCopies = NativeBattleSceneCopyProvenance.Intersect(textures.SceneCopies, x, y, width, rectangle.Height) };
        result.Validate();
        return result;
    }
}
