using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>54CBD0/54E730 Cactuar phase; the actual dense object owns its current pose.</summary>
public sealed record NativeCactuarWorldState([property: JsonRequired] int Phase, [property: JsonRequired] int? Counter)
{
    public static NativeCactuarWorldState CreateAfterLoad() => new(0, null);

    [JsonIgnore]
    public bool Visible => Phase is 2 or 3;

    public void Validate()
    {
        if (Phase is < 0 or > 4 || Phase == 0 && Counter is not (null or 0) || Phase != 0 && (Counter is null or < 1 || Counter >= (Phase == 4 ? 60 : 30)))
        {
            throw new InvalidDataException("Cactuar world phase lost its executed54CBD0/54E730 counter.");
        }
    }

    public NativeCactuarWorldState Advance(bool acceptedProposal, out bool emit)
    {
        Validate();
        emit = false;
        int phase = Phase;
        int counter = Counter ?? 0;
        if (phase == 0)
        {
            if (!acceptedProposal)
            {
                return this;
            }
            phase = 1;
            counter = 0;
        }
        if (phase == 1)
        {
            counter++;
            if (counter >= 30)
            {
                phase = 2;
                counter = 0;
            }
            emit = true;
        }
        if (phase == 2)
        {
            counter++;
            if (counter >= 30)
            {
                phase = 3;
                counter = 0;
            }
        }
        if (phase == 3)
        {
            counter++;
            if (counter >= 30)
            {
                phase = 4;
                counter = 0;
            }
            emit = true;
        }
        if (phase == 4)
        {
            counter++;
            if (counter >= 60)
            {
                phase = 0;
                counter = 0;
            }
        }
        var result = new NativeCactuarWorldState(phase, counter);
        result.Validate();
        return result;
    }
}
