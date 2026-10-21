namespace Memoria.FF8.Runtime;

public static class NativeBattleScriptedDpct
{
    public static NativeBattleScriptedColorRegisters LoadColors45E120(NativeBattleScriptedColorRegisters registers, uint first, uint second, uint third)
    {
        registers.Validate();
        return registers with { Rgb = [first, second, third], Material = third };
    }

    public static NativeBattleScriptedColorRegisters Transform45F4C0(NativeBattleScriptedColorRegisters registers, NativeBattleModelRendererRegisters far, NativeBattleSummonColors constants)
    {
        registers.Validate();
        uint material = registers.Material ?? throw new NotSupportedException("Native triple depth color interpolation requires its actual material register.");
        uint cue = registers.Ir0 ?? throw new NotSupportedException("Native triple depth color interpolation requires its actual full cue register.");
        uint[] sources = new uint[3];
        for (int index = 0; index < sources.Length; index++)
        {
            sources[index] = registers.Rgb[index] ?? throw new NotSupportedException("Native triple depth color interpolation requires all three actual FIFO colors.");
        }
        NativeBattleScriptedColorRegisters current = registers;
        uint rgbFlags = 0;
        for (int index = 0; index < sources.Length; index++)
        {
            uint source = (sources[index] & 0x00FFFFFF) | (material & 0xFF000000);
            current = NativeBattleScriptedDpcs.Transform45F270(current, source, cue, far, constants).Registers;
            rgbFlags |= current.Flags!.Value & 0x00380000;
        }
        return current with { Material = material, Ir0 = cue, Flags = rgbFlags | (current.Flags!.Value & 0x81C00000) };
    }
}
