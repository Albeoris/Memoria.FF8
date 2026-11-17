using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The original indexed/direct texture words retained by a cached PC region before VRAM invalidation.</summary>
public sealed record NativeBattlePcTextureWindowPixels([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height, [property: JsonRequired] ImmutableArray<byte> Data)
{
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
        private readonly ConcurrentDictionary<(int Row, NativeBattlePrimitiveTextureWindow Rectangle), CaptureEntry> captures = new();
        private readonly ConditionalWeakTable<object, CaptureProvenance> publications = new();
        private CaptureProvenance? currentProvenance;
        private int bytes;

        private sealed class CaptureEntry(NativeBattlePcTextureWindowPixels pixels, CaptureProvenance provenance)
        {
            internal NativeBattlePcTextureWindowPixels Pixels { get; } = pixels;
            internal CaptureProvenance Provenance = provenance;
        }

        private sealed class CaptureProvenance(NativeBattleEffectTextureState textures)
        {
            private readonly ImmutableArray<NativeBattleTextureSourceRegion>? sources = textures.LiveTextureSources;
            private readonly ImmutableArray<NativeBattleTextureRegistration>? registrations = textures.TextureRegistrations;
            private readonly ImmutableArray<byte>? tail = textures.VramReadTail;
            private readonly ImmutableArray<NativeBattleSceneCopyRegion>? sceneCopies = textures.SceneCopies;

            internal bool Matches(NativeBattleEffectTextureState source)
            {
                return SameArray(sources, source.LiveTextureSources) && SameArray(registrations, source.TextureRegistrations) && SameArray(tail, source.VramReadTail) && SameArray(sceneCopies, source.SceneCopies);
            }

            private static bool SameArray<T>(ImmutableArray<T>? first, ImmutableArray<T>? second) => first.HasValue == second.HasValue && (!first.HasValue || first.GetValueOrDefault().Equals(second.GetValueOrDefault()));
        }

        internal NativeBattlePcTextureWindowPixels Capture(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
        {
            object publication = textures.VramPublicationIdentity;
            var key = (row, rectangle);
            // The proof contains no publication reference. Cached captures do not hold historical VRAM versions alive.
            if (publications.TryGetValue(publication, out CaptureProvenance? proof) && proof.Matches(textures) && captures.TryGetValue(key, out CaptureEntry? exact) && ReferenceEquals(Volatile.Read(ref exact.Provenance), proof))
            {
                return exact.Pixels;
            }
            lock (gate)
            {
                if (currentProvenance is null || !currentProvenance.Matches(textures))
                {
                    captures.Clear();
                    bytes = 0;
                }
                if (!publications.TryGetValue(publication, out proof) || !proof.Matches(textures))
                {
                    proof = new(textures);
                    publications.AddOrUpdate(publication, proof);
                }
                currentProvenance = proof;
                bool found = captures.TryGetValue(key, out CaptureEntry? cached);
                if (cached is not null && (ReferenceEquals(cached.Provenance, proof) || HasSameWords(cached.Pixels, textures)))
                {
                    Volatile.Write(ref cached.Provenance, proof);
                    return cached.Pixels;
                }
                NativeBattlePcTextureWindowPixels result = CaptureUncached(row, rectangle, textures);
                int replacedBytes = cached?.Pixels.Data.Length ?? 0;
                if (result.Data.Length > MaximumBytes)
                {
                    if (found)
                    {
                        captures.TryRemove(key, out _);
                        bytes -= replacedBytes;
                    }
                    return result;
                }
                // One bucket covers all96 full-page rows and a bounded set of additional windows.
                // Eviction drops only cache references; published pixel owners remain immutable.
                if ((!found && captures.Count >= MaximumCaptures) || bytes - replacedBytes > MaximumBytes - result.Data.Length)
                {
                    captures.Clear();
                    bytes = 0;
                    replacedBytes = 0;
                }
                captures[key] = new(result, proof);
                bytes += result.Data.Length - replacedBytes;
                return result;
            }
        }
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
        return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(2 * ((y - Y) * Width + x - X), 2));
    }

    public void ValidateMapping(int row, NativeBattlePrimitiveTextureWindow rectangle)
    {
        Validate();
        RequireMappingBounds(row, rectangle);
        int pixelsPerWord = 4 >> (row / 32);
        if (X != (row & 15) * 64 + rectangle.X / pixelsPerWord || Y != ((row & 31) >> 4) * 256 + rectangle.Y || Width != (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord || Height != rectangle.Height)
        {
            throw new InvalidDataException("Native cached pixel words disagree with their page-depth row and crop.");
        }
    }

    public static NativeBattlePcTextureWindowPixels Capture(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        RequireMappingBounds(row, rectangle);
        CaptureBucket captures = Captures.GetValue(textures.VramCaptureIdentity, static _ => new());
        return captures.Capture(row, rectangle, textures);
    }

    private static bool HasSameWords(NativeBattlePcTextureWindowPixels captured, NativeBattleEffectTextureState textures)
    {
        Span<byte> row = stackalloc byte[512];
        int count = 2 * captured.Width;
        for (int line = 0; line < captured.Height; line++)
        {
            int address = 2 * ((captured.Y + line) * 1024 + captured.X);
            ReadSourceRow(textures, address, row[..count]);
            if (!row[..count].SequenceEqual(captured.Data.AsSpan(line * count, count)))
            {
                return false;
            }
        }
        return true;
    }

    public static void ReadSourceRow(NativeBattleEffectTextureState textures, int address, Span<byte> destination)
    {
        if (address < 0 || address > NativeBattleEffectArena.Length + NativeBattleEffectTextureState.PcPageReadTailLength - destination.Length)
        {
            throw new InvalidDataException("Native PC texture source read leaves its VRAM and retained trailing window.");
        }
        int known = Math.Clamp(NativeBattleEffectArena.Length - address, 0, destination.Length);
        if (known > 0)
        {
            textures.ReadVramInto(address, destination[..known]);
        }
        if (known < destination.Length)
        {
            ImmutableArray<byte> tail = textures.VramReadTail ?? throw new NotSupportedException("Native full-page capture reaches its unobserved trailing read window; explicitly migrate the standalone state.");
            tail.AsSpan().Slice(address + known - NativeBattleEffectArena.Length, destination.Length - known).CopyTo(destination[known..]);
        }
    }

    public static ushort ReadSourceWord(NativeBattleEffectTextureState textures, int x, int y)
    {
        int index = checked(y * 1024 + x);
        if (x < 0 || y < 0 || index > (NativeBattleEffectArena.Length + NativeBattleEffectTextureState.PcPageReadTailLength) / 2 - 1)
        {
            throw new InvalidDataException("Native PC texture source read leaves its VRAM and retained trailing window.");
        }
        if (index < NativeBattleEffectArena.Length / 2)
        {
            return textures.ReadVramWord(index % 1024, index / 1024);
        }
        Span<byte> bytes = stackalloc byte[2];
        ReadSourceRow(textures, index * 2, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static void RequireMappingBounds(int row, NativeBattlePrimitiveTextureWindow rectangle)
    {
        if (row is < 0 or >= 96)
        {
            throw new InvalidDataException("Native cached texture capture requires its actual page-depth row.");
        }
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            throw new InvalidDataException("Native cached texture requires a rectangle with positive dimensions.");
        }
    }

    private static NativeBattlePcTextureWindowPixels CaptureUncached(int row, NativeBattlePrimitiveTextureWindow rectangle, NativeBattleEffectTextureState textures)
    {
        int pixelsPerWord = 4 >> (row / 32);
        int x = (row & 15) * 64 + rectangle.X / pixelsPerWord;
        int y = ((row & 31) >> 4) * 256 + rectangle.Y;
        int width = (rectangle.X % pixelsPerWord + rectangle.Width + pixelsPerWord - 1) / pixelsPerWord;
        byte[] data = new byte[2 * width * rectangle.Height];
        for (int line = 0; line < rectangle.Height; line++)
        {
            int address = 2 * ((y + line) * 1024 + x);
            int count = 2 * width;
            ReadSourceRow(textures, address, data.AsSpan(2 * line * width, count));
        }
        NativeBattlePcTextureWindowPixels result = new(x, y, width, rectangle.Height, ImmutableCollectionsMarshal.AsImmutableArray(data)) { LiveTextureSources = textures.LiveTextureSources, TextureRegistrations = textures.TextureRegistrations, SceneCopies = NativeBattleSceneCopyProvenance.Intersect(textures.SceneCopies, x, y, width, rectangle.Height) };
        result.Validate();
        return result;
    }
}
