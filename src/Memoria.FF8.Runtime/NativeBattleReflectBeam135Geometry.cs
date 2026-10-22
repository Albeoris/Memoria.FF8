namespace Memoria.FF8.Runtime;

public static class NativeBattleReflectBeam135Geometry
{
    public static NativeBattleGeometryRegisterStep Square45F930(NativeBattleGeometryRegisterStep registers)
    {
        registers.Geometry.Validate();
        registers.Colors.Validate();
        int[] square = new int[3];
        int[] ir = new int[3];
        uint flags = 0;
        uint[] saturation = [0x81000000, 0x80800000, 0x00400000];
        for (int lane = 0; lane < 3; lane++)
        {
            short input = unchecked((short)(registers.Colors.Ir[lane] ?? throw new NotSupportedException("Reflect Beam square requires its actual signed IR input.")));
            square[lane] = input * input;
            ir[lane] = Math.Min(square[lane], 32767);
            if (square[lane] > 32767)
            {
                flags |= saturation[lane];
            }
        }
        return registers with { Colors = registers.Colors with { Mac = [square[0], square[1], square[2]], Ir = [ir[0], ir[1], ir[2]], Flags = flags } };
    }
}
