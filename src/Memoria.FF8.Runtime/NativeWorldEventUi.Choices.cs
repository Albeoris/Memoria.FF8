using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The 5438D0/4A04E0 two-row selection on a physical channel.</summary>
public sealed record NativeWorldChoice([property: JsonRequired] byte Channel, [property: JsonRequired] byte Selection, [property: JsonRequired] bool Accepted)
{
    public void Validate()
    {
        if (Channel > 3 || Selection is not (1 or 2))
        {
            throw new InvalidDataException("World choice requires a physical channel and an authored choice row.");
        }
    }
}

public sealed partial record NativeWorldEventUi
{
    public ImmutableArray<NativeWorldChoice> ChoiceChannels { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? ChoiceInputChannel { get; init; }

    [JsonIgnore]
    public NativeWorldMessage? FocusedChoiceMessage => Windows.FirstOrDefault(window => window.Channel == ChoiceInputChannel);

    [JsonIgnore]
    public NativeWorldChoice? FocusedChoice => ChoiceChannels.FirstOrDefault(choice => choice.Channel == ChoiceInputChannel);

    [JsonIgnore]
    public bool HasPendingChoice => FocusedChoice is { Accepted: false };

    [JsonIgnore]
    public bool CanAcceptChoice => HasPendingChoice && FocusedChoiceMessage?.Text is { TextComplete: true, Openness: 4096 };

    public NativeWorldEventUi ShowChoice(NativeWorldMessage message)
    {
        if (message.Text is null)
        {
            throw new NotSupportedException("World choice requires its original text timing owner.");
        }
        return Show(message, true);
    }

    public int ReadChoiceResult(ushort windowId)
    {
        if (windowId > 12)
        {
            throw new NotSupportedException("World choice result requires a represented original window0..12.");
        }
        NativeWorldMessage? binding = Windows.Concat(AliasedWindows).FirstOrDefault(window => window.WindowId == windowId);
        NativeWorldChoice? choice = ChoiceChannels.FirstOrDefault(choice => choice.Channel == binding?.Channel);
        return choice is { Accepted: true } ? choice.Selection - 1 : -1;
    }

    public NativeWorldEventUi AdvanceChoice(uint currentButtons, uint previousButtons, int? selection = null)
    {
        if (selection is not (null or 1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }
        if (!CanAcceptChoice || FocusedChoice is not NativeWorldChoice choice)
        {
            return this;
        }
        uint pressed = currentButtons & ~previousButtons;
        NativeWorldChoice next = choice;
        if ((pressed & 0xC0) != 0)
        {
            next = choice with { Selection = checked((byte)(selection ?? choice.Selection)), Accepted = true };
        }
        else if ((pressed & 0x10) != 0)
        {
            next = choice with { Selection = 2 };
        }
        else if ((pressed & 0x5000) != 0)
        {
            next = choice with { Selection = choice.Selection == 1 ? (byte)2 : (byte)1 };
        }
        return next == choice ? this : this with { ChoiceChannels = ChoiceChannels.SetItem(ChoiceChannels.IndexOf(choice), next) };
    }

    private void ValidateChoices()
    {
        if (ChoiceChannels.IsDefault || ChoiceChannels.Any(choice => choice is null || !Windows.Concat(AliasedWindows).Any(window => window.Channel == choice.Channel)) || ChoiceChannels.Select(choice => choice.Channel).Distinct().Count() != ChoiceChannels.Length || ChoiceInputChannel is byte focus && (!ChoiceChannels.Any(choice => choice.Channel == focus) || FocusedChoiceMessage?.Text is null) || ChoiceChannels.Any(choice => choice.Accepted && Windows.Any(window => window.Channel == choice.Channel && window.TextComplete != true)))
        {
            throw new InvalidDataException("World choices require distinct native channels and their cached windows.");
        }
        foreach (NativeWorldChoice choice in ChoiceChannels)
        {
            choice.Validate();
        }
    }
}
