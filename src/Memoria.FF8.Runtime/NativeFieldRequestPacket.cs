using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained native request bytes/words, independent of an arrival's derived placement. Null words have unknown physical provenance.</summary>
public sealed record NativeFieldRequestPacket
{
    [JsonRequired]
    public int Version { get; init; } = 1;
    public byte? Kind { get; init; }
    public ushort? FieldId { get; init; }
    public short? X { get; init; }
    public short? Y { get; init; }
    public short? ExtraWord { get; init; }
    public ushort? Triangle { get; init; }
    public ushort? DirectionWord { get; init; }

    public void Validate()
    {
        if (Version != 1)
        {
            throw new InvalidDataException("Unsupported native field request packet version.");
        }
    }

    public NativeFieldRequestPacket WithFieldRequest(FieldTransition transition, ushort direction)
    {
        Validate();
        if (transition.FieldId is < 0 or > ushort.MaxValue || !float.IsFinite(transition.X) || !float.IsFinite(transition.Y) || transition.X is < short.MinValue or > short.MaxValue || transition.Y is < short.MinValue or > short.MaxValue || transition.X != MathF.Truncate(transition.X) || transition.Y != MathF.Truncate(transition.Y) || transition.Triangle is < 0 or > ushort.MaxValue || transition.Direction != unchecked((byte)direction) || transition.DiscJump is { } discJump && discJump.NativeFacingWord != direction)
        {
            throw new InvalidDataException("A native field request must retain its exact WORD operands and derived direction byte.");
        }
        return this with { Kind = transition.DiscJump is null ? (byte)1 : (byte)9, FieldId = (ushort)transition.FieldId, X = (short)transition.X, Y = (short)transition.Y, ExtraWord = transition.NativeExtraWord ?? ExtraWord, Triangle = (ushort)transition.Triangle, DirectionWord = direction };
    }

    public NativeFieldRequestPacket WithWorldRequest(WorldTransition transition)
    {
        Validate();
        return this with { Kind = 7, FieldId = unchecked((ushort)transition.EntryIndex), Triangle = unchecked((ushort)transition.Mode), DirectionWord = unchecked((ushort)transition.Extra) };
    }

    public NativeFieldRequestPacket WithScriptMenuRequest(ushort menuId)
    {
        Validate();
        return this with { Kind = 5, FieldId = menuId };
    }

    public NativeFieldRequestPacket WithBattleRequest(ushort encounter, short? x, short? y, ushort? triangle)
    {
        Validate();
        return this with { Kind = Kind == 0 ? (byte)3 : Kind, FieldId = encounter, X = x, Y = y, Triangle = triangle };
    }

    public NativeFieldRequestPacket WithBattleRequest(ushort encounter)
    {
        Validate();
        return this with { Kind = Kind == 0 ? (byte)3 : Kind, FieldId = encounter };
    }

    public NativeFieldRequestPacket WithWorldReturn(FieldTransition transition)
    {
        Validate();
        NativeFieldRequestPacket written = WithFieldRequest(transition, checked((ushort)transition.Direction));
        // The world return loader writes placement words, not the old field-request destination word.
        return written with { Kind = Kind, FieldId = FieldId, ExtraWord = ExtraWord };
    }

    public NativeFieldRequestPacket WithGameOverRequest()
    {
        Validate();
        return this with { Kind = 4 };
    }

    public NativeFieldRequestPacket WithConsumedRequest()
    {
        Validate();
        return this with { Kind = 0 };
    }

    public NativeFieldRequestPacket WithTimerExpiry()
    {
        Validate();
        return this with { Kind = 1, FieldId = 75, Triangle = 0x7FFF };
    }

    public FieldTransition ToFieldTransition()
    {
        Validate();
        if (Kind != 1 || FieldId is not ushort field || X is not short x || Y is not short y || Triangle is not ushort triangle || DirectionWord is not ushort direction)
        {
            throw new NotSupportedException("Native field request requires its preserved original coordinate, triangle and direction words; unknown inherited words cannot be invented.");
        }
        return new(field, x, y, triangle, unchecked((byte)direction), ExtraWord);
    }
}
