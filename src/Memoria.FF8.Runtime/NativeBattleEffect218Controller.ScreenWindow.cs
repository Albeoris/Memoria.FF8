namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffectArena ScreenWindowContext637200(NativeBattleEffectArena retained, short age)
    {
        NativeBattleEffectArena context = retained.Write(0, BitConverter.GetBytes(0xE24FC8u)).Write(8, new byte[4]).Write(0x10, new byte[4]).Write(0x14, BitConverter.GetBytes(unchecked((-3 - age) * 28))).Write(0x18, BitConverter.GetBytes((short)64)).Write(0x1A, BitConverter.GetBytes((short)128)).Write(0x1C, BitConverter.GetBytes((short)64)).Write(0x1E, BitConverter.GetBytes((short)128)).Write(0x20, BitConverter.GetBytes(0x33u)).Write(0x24, BitConverter.GetBytes(0x00808080u));
        return age < 6 ? context : context.Write(0x20, BitConverter.GetBytes(0xF3u)).Write(0x0C, BitConverter.GetBytes(unchecked((age - 6) << 10)));
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceScreenWindow637200(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short normalDepth, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if (mesh.Address != 0xE24FC8)
        {
            throw new InvalidDataException("Effect218 screen window requires its actual selected mesh.");
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(rotation, 3584, 3584, 3584) with { X = 0, Y = 0, Z = normalDepth };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        short age = Signed(Read(frame, address), 12);
        frame = DrawWindowed218(frame, mesh, projection, colors, context => ScreenWindowContext637200(context, age), deliver, 14);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 10 ? 2 : 0);
    }
}
