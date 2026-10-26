using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePcUiViewport(int X, int Y, int Width, int Height);
public sealed record NativeBattlePcUiProfile(int Width, int Height, float ScaleX, float ScaleY, float TranslationX, float TranslationY);
public sealed record NativeBattlePcUiIconProfile(float TextureScaleX, float TextureScaleY, bool ViewportScaledTextureExtent, int OriginX, int OriginY, int PageWidth, int PageHeight, int PageColumns, int PageRows);
public sealed record NativeBattlePcUiVertex(float X, float Y, float Z, float U, float V);
public sealed record NativeBattlePcUiQuad(int Page, int Palette, uint DiffuseArgb, ImmutableArray<NativeBattlePcUiVertex> Vertices, NativeBattlePcUiViewport Viewport, int Epoch);
public sealed record NativeBattlePcUiBatch(NativeBattlePcUiViewport Viewport, int Epoch, ImmutableArray<NativeBattlePcUiQuad> Quads);
public sealed record NativeBattlePcUiRules(float InitialDepth, float DepthStep, float HalfPixel, float PageExtent);
public sealed record NativeBattlePcUiFrame(ImmutableArray<NativeBattlePcUiQuad> Opaque, ImmutableArray<NativeBattlePcUiBatch> Semi, ImmutableArray<byte> FormattedPackets, int SourceWidth, int SourceHeight);

public sealed record NativeBattlePcUiImmediateState
{
    [JsonRequired] public required bool? Suppressed { get; init; }
    [JsonRequired] public required bool? Grouped { get; init; }
    [JsonRequired] public required float? Depth { get; init; }
    [JsonRequired] public required int? GroupCount { get; init; }
    [JsonRequired] public required float? OffsetX { get; init; }
    [JsonRequired] public required float? OffsetY { get; init; }
    [JsonRequired] public required float? TranslationX { get; init; }
    [JsonRequired] public required float? TranslationY { get; init; }
    [JsonRequired] public required NativeBattlePcUiProfile? Profile { get; init; }
    [JsonRequired] public required NativeBattlePcUiViewport? Viewport { get; init; }
    [JsonRequired] public required int Epoch { get; init; }
    [JsonRequired] public required ImmutableArray<NativeBattlePcUiQuad> Opaque { get; init; }
    [JsonRequired] public required ImmutableArray<NativeBattlePcUiQuad> CurrentSemi { get; init; }
    [JsonRequired] public required ImmutableArray<NativeBattlePcUiBatch> ClosedSemi { get; init; }
    [JsonRequired] public required ImmutableArray<byte> FormattedPackets { get; init; }

    public static NativeBattlePcUiImmediateState FromColdProcess()
    {
        return new()
        {
            Suppressed = false,
            Grouped = false,
            Depth = 0,
            GroupCount = 0,
            OffsetX = 0,
            OffsetY = 0,
            TranslationX = 0,
            TranslationY = 0,
            Profile = null,
            Viewport = null,
            Epoch = 0,
            Opaque = [],
            CurrentSemi = [],
            ClosedSemi = [],
            FormattedPackets = []
        };
    }

    public NativeBattlePcUiImmediateState Initialize4972A0(NativeBattlePcUiProfile profile)
    {
        if (profile.Width <= 0 || profile.Height <= 0 || !float.IsFinite(profile.ScaleX) || !float.IsFinite(profile.ScaleY) || profile.ScaleX <= 0 || profile.ScaleY <= 0)
        {
            throw new InvalidDataException("Native PC UI initialization requires its selected viewport profile.");
        }
        return this with { Profile = profile, Viewport = new(0, 0, profile.Width, profile.Height) };
    }

    public NativeBattlePcUiImmediateState BeginFrame49B120(NativeBattlePcUiRules rules)
    {
        return this with { Depth = rules.InitialDepth, Opaque = [], CurrentSemi = [], ClosedSemi = [], FormattedPackets = [] };
    }

    public NativeBattlePcUiImmediateState Toggle49B080(NativeBattlePcUiRules rules)
    {
        bool grouped = !(Grouped ?? throw new NotSupportedException("Native49B080 requires its retained grouping BYTE."));
        float depth = Depth ?? throw new NotSupportedException("Native49B080 requires its retained depth FLOAT.");
        return this with { Grouped = grouped, Depth = grouped ? depth : Add(depth, rules.DepthStep) };
    }

    public NativeBattlePcUiImmediateState SetTranslation49B020(int x, int y, short drawAreaX, short drawAreaY) => this with { OffsetX = x - drawAreaX, OffsetY = y - drawAreaY };
    public NativeBattlePcUiImmediateState ClearTranslation49D640() => this with { OffsetX = 0, OffsetY = 0 };

    public NativeBattlePcUiImmediateState CloseSemi499A80(NativeBattlePcUiViewport viewport)
    {
        return CurrentSemi.IsEmpty ? this : this with { ClosedSemi = ClosedSemi.Add(new(viewport, Epoch, CurrentSemi)), CurrentSemi = [] };
    }

    public NativeBattlePcUiImmediateState SetViewport49D590(NativeBattlePcUiViewport viewport)
    {
        NativeBattlePcUiViewport previous = Viewport ?? throw new NotSupportedException("Native49D590 requires its previous viewport owner.");
        return CloseSemi499A80(previous) with { Viewport = viewport, Epoch = checked(Epoch + 1) };
    }

    public (NativeBattlePcUiImmediateState State, NativeBattlePcUiFrame Frame) Publish499EA0()
    {
        NativeBattlePcUiProfile profile = Profile ?? throw new NotSupportedException("Native PC UI publication requires its initialized viewport profile.");
        NativeBattlePcUiViewport viewport = Viewport ?? throw new NotSupportedException("Native PC UI publication requires its current viewport.");
        NativeBattlePcUiImmediateState state = CloseSemi499A80(viewport);
        NativeBattlePcUiFrame frame = new(state.Opaque.OrderBy(quad => quad.Page).ToImmutableArray(), state.ClosedSemi, state.FormattedPackets, profile.Width, profile.Height);
        return (state with { Opaque = [], ClosedSemi = [], FormattedPackets = [] }, frame);
    }

    public NativeBattlePcUiImmediateState DrawString4A93D0(ImmutableArray<byte> glyphs, uint position, uint color, int iconCount, Func<byte, int, int, uint, ImmutableArray<byte>> encodeIcon, NativeBattlePcUiIconProfile opaqueProfile, NativeBattlePcUiIconProfile semiProfile, NativeBattlePcUiRules rules)
    {
        if (glyphs.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native4A93D0 requires its current terminated glyph bytes.");
        }
        int x = unchecked((short)position);
        int y = unchecked((int)position) >> 16;
        NativeBattlePcUiImmediateState state = Toggle49B080(rules);
        bool terminated = false;
        foreach (byte glyph in glyphs)
        {
            if (glyph == 0)
            {
                terminated = true;
                break;
            }
            if (glyph < iconCount)
            {
                ImmutableArray<byte> packets = encodeIcon(glyph, x, y, color);
                if (packets.IsDefault || packets.Length % 24 != 0)
                {
                    throw new InvalidDataException("Native4B76A0 requires complete formatted icon parts.");
                }
                state = state with { FormattedPackets = state.FormattedPackets.AddRange(packets) };
                for (int offset = 0; offset < packets.Length; offset += 24)
                {
                    state = NativeBattlePcUiIconDrawing.Submit49BB30(state, packets.AsSpan().Slice(offset, 24), opaqueProfile, semiProfile, rules);
                }
            }
            x = unchecked(x + 8);
        }
        if (!terminated)
        {
            throw new InvalidDataException("Native4A93D0 glyphs leave their original terminator.");
        }
        return state.Toggle49B080(rules);
    }

    internal static float Add(float left, float right) => (float)((double)left + right);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePcUiImmediateState? UiImmediate { get; init; }
}
