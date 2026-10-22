namespace Memoria.FF8.Runtime;

public static class NativeBattleModuleRenderCommands
{
    public static NativeBattleEffectMemory ClearTranslation704070(NativeBattleEffectMemory memory)
    {
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Native704070 requires its current geometry-register owner.");
        geometry.Validate();
        return memory with { GeometryRegisters = geometry with { Translation = [0, 0, 0] } };
    }

    public static int Factor700150(int age, int rise, int hold, int fall)
    {
        if (age < rise)
        {
            return unchecked((age + 1) << 12) / rise;
        }
        if (age < unchecked(rise + hold))
        {
            return 4096;
        }
        return unchecked(unchecked(((rise + hold - age) << 12) - 4096) / fall + 4096);
    }

    public static NativeBattleGfGeometryBatch Emit700150(int age, int red, int green, int blue, int rise, int hold, int fall)
    {
        return NativeBattleRinoa89FadeDrawing.Draw7040B0(red, green, blue, Factor700150(age, rise, hold, fall));
    }
}
