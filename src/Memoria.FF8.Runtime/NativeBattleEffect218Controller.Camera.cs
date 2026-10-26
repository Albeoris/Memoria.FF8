namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218CameraWord1D97712(short Value) : NativeBattleEffect218Operation;

public static partial class NativeBattleEffect218Controller
{
    public static short CameraDisplacement634B90(short age, short amplitude)
    {
        int angle = unchecked(age << 10) / 8 + 1024;
        int displacement = unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 12;
        return unchecked((short)((age & 1) == 0 ? -displacement : displacement));
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceCamera634B90(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short value = CameraDisplacement634B90(Signed(task, 12), Signed(task, 14));
        frame = inputs.Execute(frame, new NativeBattleEffect218CameraWord1D97712(value));
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 8 ? 2 : 0);
    }
}
