using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun150Controller
{
    public static (NativeBattleGatlingGun150Frame Frame, int Result) AdvanceSpawner(NativeBattleGatlingGun150Frame frame, uint address, NativeBattleGatlingGun150Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask source = Read(frame, address);
        short delay = Signed(source, 14);
        if (delay > 0)
        {
            return (Write(frame, address, source.WriteWord(14, unchecked((ushort)(delay - 1)))), 0);
        }
        bool second = source.Callback == 0x60EFF0;
        if (!second && source.Callback != 0x60EAE0)
        {
            throw new NotSupportedException("Gatling Gun reached an unsupported delayed producer.");
        }
        short x = Signed(source, 16);
        short z = Signed(source, 20);
        short yaw = 0;
        if (second)
        {
            frame = DrawRandom(frame, out ushort randomX);
            frame = DrawRandom(frame, out ushort randomZ);
            int target = checked((int)frame.State.ReadDword(FirstTargetGlobal));
            NativeBattleActionMatrix world = inputs.ReadModel(frame, target).GeometryWorldMatrix ?? throw new NotSupportedException("Gatling Gun requires its target world rotation.");
            (int vx, int vy, int vz) = NativeBattleGfMatrices.RotateLong56C600(world, (randomX & 4095) - 2048, 0, -(randomZ & 2047));
            (vx, vy, vz) = NativeBattleActionGeometryRules.NormalizeVector56BC50(vx, vy, vz);
            frame = DrawRandom(frame, out ushort randomRadius);
            int radius = randomRadius % 210 + 60;
            x = unchecked((short)(x + (unchecked(radius * vx) >> 12)));
            z = unchecked((short)(z + (unchecked(radius * vz) >> 12)));
            int caster = checked((int)frame.State.ReadDword(CasterGlobal));
            yaw = unchecked((short)((inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Gatling Gun requires its caster yaw.")) + 2048));
        }
        (frame, uint mesh) = RequireAppend(frame, second ? 0x60F1C0u : 0x60EBD0u);
        NativeBattleModulePoolTask meshTask = CopyTask(Read(frame, mesh).WriteWord(12, 0), 16, TaskView(source, 16, 8), 8);
        if (second)
        {
            meshTask = meshTask.WriteWord(16, unchecked((ushort)x)).WriteWord(20, unchecked((ushort)z)).WriteWord(24, unchecked((ushort)yaw));
        }
        frame = DrawRandom(frame, out ushort angle);
        meshTask = meshTask.WriteWord(second ? 26 : 24, (ushort)(angle & 2047));
        if (second)
        {
            frame = DrawRandom(frame, out ushort randomScale);
            short scale = (short)(randomScale % 864 + 1088);
            meshTask = meshTask.WriteWord(28, unchecked((ushort)scale)).WriteWord(30, unchecked((ushort)(scale / 4)));
        }
        else
        {
            meshTask = meshTask.WriteWord(28, 1024).WriteWord(30, 256);
        }
        frame = Write(frame, mesh, meshTask);
        (frame, uint sprite) = RequireAppend(frame, 0x60ED10);
        NativeBattleModulePoolTask spriteTask = CopyTask(Read(frame, sprite).WriteWord(12, 0), 16, TaskView(Read(frame, mesh), 16, 8), 8);
        if (!second)
        {
            spriteTask = spriteTask.WriteWord(18, unchecked((ushort)-200));
        }
        frame = DrawRandom(frame, out ushort spriteScale);
        frame = Write(frame, sprite, spriteTask.WriteWord(28, (ushort)(spriteScale % (second ? 3072 : 2304) + (second ? 4352 : 3072))));
        (frame, uint slots) = RequireAppend(frame, second ? 0x60F330u : 0x60EDA0u);
        NativeBattleModulePoolTask slotsTask = CopyTask(Read(frame, slots).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(1 << source.ReadByte(24)))), 16, TaskView(Read(frame, mesh), 16, 8), 8);
        return (Write(frame, slots, slotsTask), 2);
    }
}
