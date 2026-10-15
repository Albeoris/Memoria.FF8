using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The normal field movie resource selected once by51F2C0/55A140 at MOVIEREADY.</summary>
public sealed record NativeMovieSource([property: JsonRequired] byte DiscIndex, [property: JsonRequired] int MovieIndex)
{
    public void Validate()
    {
        if (DiscIndex > 3 || (uint)MovieIndex > byte.MaxValue)
        {
            throw new NotSupportedException("Normal field movie resources require their supported native disc0..3 and original BYTE-domain clip index; startup disc4 and unproved table indices are unsupported.");
        }
    }
}
