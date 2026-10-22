using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGatlingGun305Controller
{
    private static (NativeBattleGatlingGun305Frame Frame, int Result) Birth(NativeBattleGatlingGun305Frame frame, uint address, NativeBattleGatlingGun305Inputs inputs)
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
        bool second = source.Callback == 0x5A6580;
        short x = Signed(source, 0x10);
        short y = Signed(source, 0x12);
        short z = Signed(source, 0x14);
        short yaw;
        if (second)
        {
            frame = DrawRandom(frame, out ushort randomX);
            frame = DrawRandom(frame, out ushort randomZ);
            NativeBattleActionMatrix world = inputs.ReadModel(checked((int)frame.State.ReadDword(TargetGlobal))).GeometryWorldMatrix ?? throw new NotSupportedException("Gatling Gun requires its target world rotation.");
            (int vx, int vy, int vz) = NativeBattleGfMatrices.RotateLong56C600(world, (randomX & 0xFFF) - 2048, 0, -(randomZ & 0x7FF));
            (vx, vy, vz) = NativeBattleActionGeometryRules.NormalizeVector56BC50(vx, vy, vz);
            frame = DrawRandom(frame, out ushort radiusRandom);
            int radius = radiusRandom % 210 + 60;
            x = unchecked((short)(x + (unchecked(radius * vx) >> 12)));
            z = unchecked((short)(z + (unchecked(radius * vz) >> 12)));
            yaw = unchecked((short)((inputs.ReadModel(checked((int)frame.State.ReadDword(CasterGlobal))).Rotation14 ?? throw new NotSupportedException("Gatling Gun requires its caster yaw.")) + 2048));
        }
        else
        {
            yaw = 0;
        }
        (frame, uint mesh) = Append(frame, second ? 0x5A6750u : 0x5A6160u);
        NativeBattleModulePoolTask meshTask = CopyPacked(Read(frame, mesh).WriteWord(12, 0), 0x10, TaskView(source, 0x10, 8));
        if (second)
        {
            meshTask = meshTask.WriteWord(0x10, unchecked((ushort)x)).WriteWord(0x14, unchecked((ushort)z)).WriteWord(0x18, unchecked((ushort)yaw));
        }
        frame = DrawRandom(frame, out ushort angleRandom);
        meshTask = meshTask.WriteWord(second ? 0x1A : 0x18, (ushort)(angleRandom & 0x7FF));
        if (second)
        {
            frame = DrawRandom(frame, out ushort scaleRandom);
            short scale = (short)(scaleRandom % 864 + 1088);
            meshTask = meshTask.WriteWord(0x1C, unchecked((ushort)scale)).WriteWord(0x1E, unchecked((ushort)(scale / 4)));
        }
        else
        {
            meshTask = meshTask.WriteWord(0x1C, 1024).WriteWord(0x1E, 256);
        }
        frame = Write(frame, mesh, meshTask);
        (frame, uint sprite) = Append(frame, 0x5A62A0);
        NativeBattleModulePoolTask spriteTask = CopyPacked(Read(frame, sprite).WriteWord(12, 0), 0x10, TaskView(Read(frame, mesh), 0x10, 8));
        if (!second)
        {
            spriteTask = spriteTask.WriteWord(0x12, unchecked((ushort)-200));
        }
        frame = DrawRandom(frame, out ushort spriteScale);
        frame = Write(frame, sprite, spriteTask.WriteWord(0x1C, (ushort)(spriteScale % (second ? 3072 : 2304) + (second ? 4352 : 3072))));
        (frame, uint slots) = Append(frame, second ? 0x5A68C0u : 0x5A6330u);
        NativeBattleModulePoolTask slotsTask = CopyPacked(Read(frame, slots).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(1 << source.ReadByte(0x18)))), 0x10, TaskView(Read(frame, mesh), 0x10, 8));
        return (Write(frame, slots, slotsTask), 2);
    }
}
