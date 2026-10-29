using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldClosingChannel(byte Channel, short? Openness, byte TicksRemaining);
public sealed record NativeWorldChannelReadiness(byte Channel, bool? TextComplete);

/// <summary>Executed543790 window binding and retained native text callback.</summary>
public sealed record NativeWorldMessage([property: JsonRequired] int WindowId, [property: JsonRequired] byte MessageId, [property: JsonRequired] byte Channel, [property: JsonRequired] byte Alignment, [property: JsonRequired] byte Mode, [property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] string SourceSha256, [property: JsonRequired] bool? TextComplete)
{
    public NativeWorldTextState? Text { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? FuelRemaining { get; init; }

    public void Validate()
    {
        if (FuelRemaining is byte remaining && (remaining == 0 || WindowId != 1 || MessageId != 254))
        {
            throw new InvalidDataException("Dynamic fuel text requires its positive remaining quantity and native window binding.");
        }
        if (WindowId is < 0 or > 12 || Channel > 3 || Alignment > 3 || Mode > 8 || SourceSha256 is not { Length: 64 } || !SourceSha256.All(Uri.IsHexDigit) || Text is null && TextComplete == true || Text is not null && TextComplete != Text.TextComplete)
        {
            throw new InvalidDataException("World message requires its original window record and authenticated message source.");
        }
        Text?.Validate();
    }
}

/// <summary>World script mode, current callback close suppression and active native window bindings.</summary>
public sealed record NativeWorldEventUi([property: JsonRequired] byte ScriptMode, [property: JsonRequired] bool CloseSuppressedThisCallback, [property: JsonRequired] ImmutableArray<NativeWorldMessage> Windows)
{
    public ImmutableArray<NativeWorldClosingChannel> ClosingChannels { get; init; } = [];
    public ImmutableArray<NativeWorldMessage> AliasedWindows { get; init; } = [];
    public ImmutableArray<NativeWorldChannelReadiness> RetainedChannelReadiness { get; init; } = [];

    private NativeWorldEventUi(NativeWorldEventUi original)
    {
        ScriptMode = original.ScriptMode;
        CloseSuppressedThisCallback = original.CloseSuppressedThisCallback;
        Windows = original.Windows;
        ClosingChannels = original.ClosingChannels;
        AliasedWindows = original.AliasedWindows;
        RetainedChannelReadiness = original.RetainedChannelReadiness;
        NativeWorldUiOccurrences.Copy(original, this);
    }

    public long PresentationOccurrenceId(int windowId) => NativeWorldUiOccurrences.Read(this, windowId);

    public static NativeWorldEventUi Create() => new(0, false, []);

    public void Validate()
    {
        if (Windows.IsDefault || Windows.Any(window => window is null) || RetainedChannelReadiness.IsDefault || RetainedChannelReadiness.Any(channel => channel is null || channel.Channel > 3 || Windows.Any(window => window.Channel == channel.Channel)) || RetainedChannelReadiness.Select(channel => channel.Channel).Distinct().Count() != RetainedChannelReadiness.Length)
        {
            throw new InvalidDataException("World channel readiness requires distinct inactive physical channels.");
        }
        if (Windows.IsDefault || Windows.Any(window => window is null) || AliasedWindows.IsDefault || AliasedWindows.Any(window => window is null || window.Text is not null || window.TextComplete is not null) || Windows.Concat(AliasedWindows).Select(window => window.WindowId).Distinct().Count() != Windows.Length + AliasedWindows.Length || Windows.Select(window => window.Channel).Distinct().Count() != Windows.Length || ClosingChannels.IsDefault || ClosingChannels.Any(channel => channel is null || channel.Channel > 3 || channel.Openness is < 0 or > 4096 || channel.TicksRemaining is < 1 or > 8 || channel.Openness is short openness && (openness + 511) / 512 != channel.TicksRemaining) || ClosingChannels.Select(channel => channel.Channel).Distinct().Count() != ClosingChannels.Length || ClosingChannels.Any(channel => Windows.Any(window => window.Channel == channel.Channel)))
        {
            throw new InvalidDataException("World UI requires distinct executed native window bindings.");
        }
        foreach (NativeWorldMessage window in Windows.Concat(AliasedWindows))
        {
            window.Validate();
        }
    }

    public NativeWorldEventUi BeginCallback()
    {
        Validate();
        return this with { CloseSuppressedThisCallback = false, Windows = Windows.Select(window => window.Text is null ? window with { TextComplete = null } : window).ToImmutableArray() };
    }

    public NativeWorldEventUi AdvanceText(int ticks)
    {
        Validate();
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        if (ticks == 0 || Windows.All(window => window.Text is null) && ClosingChannels.IsEmpty)
        {
            return this;
        }
        ImmutableArray<NativeWorldClosingChannel> closing = ClosingChannels.Where(channel => channel.TicksRemaining > ticks).Select(channel => channel with { TicksRemaining = checked((byte)(channel.TicksRemaining - ticks)), Openness = channel.Openness is short openness ? (short)(openness - 512 * ticks) : null }).ToImmutableArray();
        return this with { ClosingChannels = closing, Windows = Windows.Select(window =>
        {
            NativeWorldTextState? text = window.Text;
            for (int tick = 0; tick < ticks && text is not null; tick++)
            {
                text = text.Advance();
            }
            return text is null ? window : window with { Text = text, TextComplete = text.TextComplete };
        }).ToImmutableArray() };
    }

    public NativeWorldEventUi WithScriptMode(byte mode) => this with { ScriptMode = mode };

    public NativeWorldEventUi SuppressClose() => this with { CloseSuppressedThisCallback = true };

    public NativeWorldEventUi Show(NativeWorldMessage message)
    {
        message.Validate();
        NativeWorldMessage? existing = Windows.Concat(AliasedWindows).FirstOrDefault(window => window.WindowId == message.WindowId);
        if (existing?.MessageId == message.MessageId && message.FuelRemaining is null)
        {
            return this;
        }
        NativeWorldMessage? physical = Windows.FirstOrDefault(window => window.Channel == message.Channel);
        if (message.Text is not null && physical is { Text: null })
        {
            throw new NotSupportedException("Replacing a historical world message requires its retained native channel openness.");
        }
        if (message.Text is { } text && physical?.Text is { } previous)
        {
            message = message with { Text = text with { Openness = previous.Openness } };
        }
        else if (message.Text is { } opened && ClosingChannels.FirstOrDefault(channel => channel.Channel == message.Channel) is { } closing)
        {
            short openness = closing.Openness ?? throw new NotSupportedException("Reopening a historical world channel requires its retained native closing openness.");
            message = message with { Text = opened with { Openness = openness } };
        }
        ImmutableArray<NativeWorldMessage> aliases = AliasedWindows.Where(window => window.WindowId != message.WindowId).ToImmutableArray();
        if (physical is not null && physical.WindowId != message.WindowId)
        {
            aliases = aliases.Add(physical with { Text = null, TextComplete = null });
        }
        return NativeWorldUiOccurrences.Open(this with { ClosingChannels = ClosingChannels.Where(channel => channel.Channel != message.Channel).ToImmutableArray(), RetainedChannelReadiness = RetainedChannelReadiness.Where(channel => channel.Channel != message.Channel).ToImmutableArray(), AliasedWindows = aliases.OrderBy(window => window.WindowId).ToImmutableArray(), Windows = Windows.Where(window => window.WindowId != message.WindowId && window.Channel != message.Channel).Append(message with { TextComplete = false }).OrderBy(window => window.WindowId).ToImmutableArray() }, message.WindowId);
    }

    public NativeWorldEventUi Close(ushort windowId)
    {
        if (windowId > 12)
        {
            throw new NotSupportedException("World message close requires a represented original window0..12.");
        }
        NativeWorldMessage? binding = Windows.Concat(AliasedWindows).FirstOrDefault(window => window.WindowId == windowId);
        if (binding is null)
        {
            return this;
        }
        NativeWorldMessage? message = Windows.FirstOrDefault(window => window.Channel == binding.Channel);
        ImmutableArray<NativeWorldClosingChannel> closing = ClosingChannels;
        if (message is not null && message.Text?.Openness != 0)
        {
            short? openness = message.Text?.Openness;
            closing = closing.Where(channel => channel.Channel != message.Channel).Append(new NativeWorldClosingChannel(message.Channel, openness, (byte)(openness is short known ? (known + 511) / 512 : 8))).ToImmutableArray();
        }
        ImmutableArray<NativeWorldMessage> aliases = AliasedWindows.Where(window => window.WindowId != windowId).ToImmutableArray();
        if (message is not null && message.WindowId != windowId)
        {
            aliases = aliases.Add(message with { Text = null, TextComplete = null });
        }
        ImmutableArray<NativeWorldChannelReadiness> readiness = RetainedChannelReadiness;
        if (message is not null)
        {
            //543A40 closes the channel without clearing4A06E0's cached text-complete byte.
            readiness = readiness.Where(channel => channel.Channel != message.Channel).Append(new NativeWorldChannelReadiness(message.Channel, message.TextComplete)).OrderBy(channel => channel.Channel).ToImmutableArray();
        }
        return this with { ClosingChannels = closing, RetainedChannelReadiness = readiness, AliasedWindows = aliases.OrderBy(window => window.WindowId).ToImmutableArray(), Windows = Windows.Where(window => window.Channel != binding.Channel).ToImmutableArray() };
    }

    public bool? MatchesTextComplete(sbyte windowId, byte expected)
    {
        if (windowId is < 0 or > 12)
        {
            throw new NotSupportedException("World message readiness requires a represented original window0..12.");
        }
        NativeWorldMessage? binding = Windows.Concat(AliasedWindows).FirstOrDefault(window => window.WindowId == windowId);
        NativeWorldMessage? message = binding is null ? null : Windows.FirstOrDefault(window => window.Channel == binding.Channel);
        NativeWorldChannelReadiness? retained = binding is null ? null : RetainedChannelReadiness.FirstOrDefault(channel => channel.Channel == binding.Channel);
        bool? ready = binding is null ? false : message is not null ? message.TextComplete : retained is not null ? retained.TextComplete : false;
        return ready is bool known ? expected == (known ? 1 : 0) : expected > 1 ? false : null;
    }
}
