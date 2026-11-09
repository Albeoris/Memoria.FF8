namespace Memoria.FF8;

internal static class Ff7DialogueButtons
{
    internal static string Label(byte code, Func<ushort, string> bindingPrompt)
    {
        ushort mask = code switch
        {
            0xF6 => 0x20,
            0xF7 => 0x10,
            0xF8 => 0x80,
            0xF9 => 0x40,
            _ => throw new ArgumentOutOfRangeException(nameof(code), "The token is not a native FFVII face button.")
        };
        return bindingPrompt(mask);
    }
}
