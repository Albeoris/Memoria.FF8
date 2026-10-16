using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Executed543790 window binding; text completion remains unknown once its unrepresented glyph callback runs.</summary>
public sealed record NativeWorldMessage([property: JsonRequired] int WindowId, [property: JsonRequired] byte MessageId, [property: JsonRequired] byte Channel, [property: JsonRequired] byte Alignment, [property: JsonRequired] byte Mode, [property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] string SourceSha256, [property: JsonRequired] bool? TextComplete)
{
    public void Validate()
    {
        if (WindowId is < 0 or > 12 || Channel > 3 || Alignment > 3 || Mode > 8 || SourceSha256 is not { Length: 64 } || !SourceSha256.All(Uri.IsHexDigit) || TextComplete == true)
        {
            throw new InvalidDataException("World message requires its original window record and authenticated message source.");
        }
    }
}

/// <summary>World script mode, current callback close suppression and active native window bindings.</summary>
public sealed record NativeWorldEventUi([property: JsonRequired] byte ScriptMode, [property: JsonRequired] bool CloseSuppressedThisCallback, [property: JsonRequired] ImmutableArray<NativeWorldMessage> Windows)
{
    public static NativeWorldEventUi Create() => new(0, false, []);

    public void Validate()
    {
        if (Windows.IsDefault || Windows.Any(window => window is null) || Windows.Select(window => window.WindowId).Distinct().Count() != Windows.Length)
        {
            throw new InvalidDataException("World UI requires distinct executed native window bindings.");
        }
        foreach (NativeWorldMessage window in Windows)
        {
            window.Validate();
        }
    }

    public NativeWorldEventUi BeginCallback()
    {
        Validate();
        // TODO(TD-003): — retain unknown readiness until the native49FEB0 glyph stream and speed owner are implemented.
        return this with { CloseSuppressedThisCallback = false, Windows = Windows.Select(window => window with { TextComplete = null }).ToImmutableArray() };
    }

    public NativeWorldEventUi WithScriptMode(byte mode) => this with { ScriptMode = mode };

    public NativeWorldEventUi SuppressClose() => this with { CloseSuppressedThisCallback = true };

    public NativeWorldEventUi Show(NativeWorldMessage message)
    {
        message.Validate();
        NativeWorldMessage? existing = Windows.FirstOrDefault(window => window.WindowId == message.WindowId);
        if (existing?.MessageId == message.MessageId)
        {
            return this;
        }
        return this with { Windows = Windows.Where(window => window.WindowId != message.WindowId).Append(message with { TextComplete = false }).OrderBy(window => window.WindowId).ToImmutableArray() };
    }

    public NativeWorldEventUi Close(ushort windowId)
    {
        if (windowId > 12)
        {
            throw new NotSupportedException("World message close requires a represented original window0..12.");
        }
        return this with { Windows = Windows.Where(window => window.WindowId != windowId).ToImmutableArray() };
    }

    public bool? MatchesTextComplete(sbyte windowId, byte expected)
    {
        if (windowId is < 0 or > 12)
        {
            throw new NotSupportedException("World message readiness requires a represented original window0..12.");
        }
        NativeWorldMessage? message = Windows.FirstOrDefault(window => window.WindowId == windowId);
        bool? ready = message is null ? false : message.TextComplete;
        return ready is bool known ? expected == (known ? 1 : 0) : expected > 1 ? false : null;
    }
}
