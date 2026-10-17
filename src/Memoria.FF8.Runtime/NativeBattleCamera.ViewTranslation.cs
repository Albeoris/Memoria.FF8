using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The separately retained signedWORDs1D97710/12/14, added to the freshly composed5033E0 view and then cleared.</summary>
public sealed record NativeBattleCameraViewTranslationScratch([property: JsonRequired] short? X, [property: JsonRequired] short? Y, [property: JsonRequired] short? Z)
{
    internal (short X, short Y, short Z) Read5033E0()
    {
        short x = X ?? throw new NotSupportedException("Native50347B requires the retained view-translation X WORD1D97710.");
        short y = Y ?? throw new NotSupportedException("Native503482 requires the retained view-translation Y WORD1D97712.");
        short z = Z ?? throw new NotSupportedException("Native503495 requires the retained view-translation Z WORD1D97714.");
        return (x, y, z);
    }

    public NativeBattleActionMatrix Apply5033E0(NativeBattleActionMatrix freshView)
    {
        (short x, short y, short z) = Read5033E0();
        return Apply5033E0(freshView, x, y, z);
    }

    internal static NativeBattleActionMatrix Apply5033E0(NativeBattleActionMatrix freshView, short x, short y, short z)
    {
        freshView.Validate();
        return freshView with { X = unchecked(freshView.X + x), Y = unchecked(freshView.Y + y), Z = unchecked(freshView.Z + z) };
    }
}

public sealed partial record NativeBattleCameraState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCameraViewTranslationScratch? ViewTranslationScratch { get; init; }

    public NativeBattleCameraState AddViewTranslationY61B8D5(short delta)
    {
        NativeBattleCameraViewTranslationScratch scratch = ViewTranslationScratch ?? throw new NotSupportedException("Native61B8D5 requires the retained camera view-translation scratch owner; old missing state is unknown.");
        short y = scratch.Y ?? throw new NotSupportedException("Native61B8D5 requires its actual prior Y WORD1D97712 before the additive write.");
        return this with { ViewTranslationScratch = scratch with { Y = unchecked((short)(y + delta)) } };
    }
}
