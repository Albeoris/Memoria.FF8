namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSpriteFilterState(uint? Current, uint? Saved)
{
    public static NativeBattleSpriteFilterState FromRendererStartup(uint selectedControl) => new(selectedControl, uint.MaxValue);

    public NativeBattleSpriteFilterState Enable45B5A0(bool backend0) => this with { Current = backend0 ? 2u : 0u };

    public NativeBattleSpriteFilterState Disable45B5C0() => this with { Current = 0 };

    public NativeBattleSpriteFilterState ApplyViewport45B4C0(int width, int height)
    {
        if (width < 540 && height < 380)
        {
            return new(0, Current);
        }
        if (Saved is null)
        {
            return this with { Current = null };
        }
        return Saved == uint.MaxValue ? this : this with { Current = Saved };
    }
}
