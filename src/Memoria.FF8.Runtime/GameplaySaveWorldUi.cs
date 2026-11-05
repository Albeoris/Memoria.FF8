using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical world message progress. Message bytes are obtained from the selected game on loading.</summary>
public sealed record GameplaySaveWorldText([property: JsonRequired] int Position, [property: JsonRequired] short Speed, [property: JsonRequired] short Accumulator, [property: JsonRequired] short Openness, [property: JsonRequired] int Phase, [property: JsonRequired] byte PauseTicks, [property: JsonRequired] byte PageCounter, [property: JsonRequired] bool Complete)
{
    internal static GameplaySaveWorldText Capture(NativeWorldTextState source) => new(source.Position, source.Speed, source.Accumulator, source.Openness, source.Phase, source.PauseTicks, source.PageCounter, source.TextComplete);

    public void Validate()
    {
        if (Position < 0 || Openness is < 0 or > 4096 || Phase is not (0 or 1 or 7 or 9 or 10 or 15) || Complete != (Phase == 7))
        {
            throw new InvalidDataException("Gameplay world text has invalid channel progress.");
        }
    }
}

public sealed record GameplaySaveWorldMessage([property: JsonRequired] int WindowId, [property: JsonRequired] byte MessageId, [property: JsonRequired] string SourceSha256, [property: JsonRequired] bool? TextComplete, [property: JsonRequired] GameplaySaveWorldText? Text, [property: JsonRequired] byte? FuelRemaining)
{
    internal static GameplaySaveWorldMessage Capture(NativeWorldMessage source) => new(source.WindowId, source.MessageId, source.SourceSha256, source.TextComplete, source.Text is { } text ? GameplaySaveWorldText.Capture(text) : null, source.FuelRemaining);

    public void Validate()
    {
        if (WindowId is < 0 or > 12 || SourceSha256 is not { Length: 64 } || !SourceSha256.All(Uri.IsHexDigit) || Text is null && TextComplete == true || Text is not null && TextComplete != Text.Complete || FuelRemaining is byte fuel && (fuel == 0 || WindowId != 1 || MessageId != 254))
        {
            throw new InvalidDataException("Gameplay world message requires a logical binding and its installed source identity.");
        }
        Text?.Validate();
    }
}

/// <summary>Ordinary world windows and their close owner, independently of loader and movement state.</summary>
public sealed record GameplaySaveWorldUi([property: JsonRequired] bool CloseSuppressed, [property: JsonRequired] GameplaySaveWorldMessage[] Windows, [property: JsonRequired] GameplaySaveWorldMessage[] AliasedWindows, [property: JsonRequired] NativeWorldClosingChannel[] ClosingChannels, [property: JsonRequired] NativeWorldChannelReadiness[] RetainedChannelReadiness, [property: JsonRequired] byte? PageInputChannel, [property: JsonRequired] int FuelMovementCallbacks)
{
    internal static GameplaySaveWorldUi? Capture(GameState source)
    {
        if (source.Scene != SceneKind.WorldMap || source.World?.EventUi is not { } ui)
        {
            return null;
        }
        ui.Validate();
        if (ui.ScriptMode != 0)
        {
            throw new NotSupportedException("Gameplay world UI requires ordinary script mode.");
        }
        int fuel = source.WorldProcess?.FuelMessageMovementCallbacks2045C78 ?? 0;
        if (ui.Windows.IsEmpty && ui.AliasedWindows.IsEmpty && ui.ClosingChannels.IsEmpty && ui.RetainedChannelReadiness.IsEmpty && !ui.CloseSuppressedThisCallback && fuel == 0)
        {
            return null;
        }
        if (source.WorldProcess is null)
        {
            throw new NotSupportedException("Gameplay world UI requires its fuel message close owner.");
        }
        GameplaySaveWorldUi result = new(ui.CloseSuppressedThisCallback, ui.Windows.Select(GameplaySaveWorldMessage.Capture).ToArray(), ui.AliasedWindows.Select(GameplaySaveWorldMessage.Capture).ToArray(), ui.ClosingChannels.ToArray(), ui.RetainedChannelReadiness.ToArray(), ui.PageInputChannel, fuel);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Windows is null || AliasedWindows is null || ClosingChannels is null || RetainedChannelReadiness is null || Windows.Concat(AliasedWindows).Any(window => window is null) || Windows.Concat(AliasedWindows).Select(window => window.WindowId).Distinct().Count() != Windows.Length + AliasedWindows.Length || AliasedWindows.Any(window => window.Text is not null || window.TextComplete is not null) || PageInputChannel > 3 || FuelMovementCallbacks is < 0 or > 30 || ClosingChannels.Any(channel => channel is null || channel.Channel > 3 || channel.Openness is < 0 or > 4096 || channel.TicksRemaining is < 1 or > 8 || channel.Openness is short openness && (openness + 511) / 512 != channel.TicksRemaining) || ClosingChannels.Select(channel => channel.Channel).Distinct().Count() != ClosingChannels.Length || RetainedChannelReadiness.Any(channel => channel is null || channel.Channel > 3) || RetainedChannelReadiness.Select(channel => channel.Channel).Distinct().Count() != RetainedChannelReadiness.Length)
        {
            throw new InvalidDataException("Gameplay world UI has invalid window, focus or close ownership.");
        }
        foreach (GameplaySaveWorldMessage message in Windows.Concat(AliasedWindows))
        {
            message.Validate();
        }
    }
}
