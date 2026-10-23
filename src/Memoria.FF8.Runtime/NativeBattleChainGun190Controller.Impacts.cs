namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceImpact(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool directional = task.Callback == 0x5BBEC0;
        if (!directional && task.Callback != 0x5BB9B0)
        {
            throw new NotSupportedException("Chain Gun impact callback is unsupported.");
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if (Signed(task, 14) > 0)
        {
            return (Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1)))), 0);
        }
        uint first;
        uint sprite;
        uint particles;
        if (!directional)
        {
            (frame, first) = RequireAppend(frame, 0x5BBAA0);
            frame = CopyImpactPoint190(frame, first, TaskView(task, 0x10, 8));
            frame = DrawRandom(frame, out ushort angle);
            frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x18, (ushort)(angle & 2047)).WriteWord(0x1C, 1024).WriteWord(0x1E, 256));
            (frame, sprite) = RequireAppend(frame, 0x5BBBE0);
            frame = CopyImpactPoint190(frame, sprite, TaskView(task, 0x10, 8));
            frame = DrawRandom(frame, out ushort size);
            frame = Write(frame, sprite, Read(frame, sprite).WriteWord(12, 0).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x1C, (ushort)(size % 2304 + 3072)));
            (frame, particles) = RequireAppend(frame, 0x5BBC70);
            frame = CopyImpactPoint190(frame, particles, TaskView(task, 0x10, 8));
        }
        else
        {
            frame = DrawRandom(frame, out ushort x);
            frame = DrawRandom(frame, out ushort z);
            NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, checked((int)frame.State.ReadDword(0x2281B18))).ReadMatrix();
            (int dx, int dy, int dz) = NativeBattleGfMatrices.RotateLong56C600(world, (x & 4095) - 2048, 0, -(z & 2047));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
            frame = DrawRandom(frame, out ushort radiusDraw);
            int radius = radiusDraw % 210 + 60;
            (frame, first) = RequireAppend(frame, 0x5BC080);
            frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(0x10, unchecked((ushort)(Signed(task, 0x10) + (unchecked(radius * direction.X) >> 12)))).WriteWord(0x12, task.ReadWord(0x12)).WriteWord(0x14, unchecked((ushort)(Signed(task, 0x14) + (unchecked(radius * direction.Z) >> 12)))).WriteWord(0x18, unchecked((ushort)(inputs.ReadModel(frame, checked((int)frame.State.ReadDword(0x2283F2C))).Rotation14!.Value + 2048))));
            frame = DrawRandom(frame, out ushort angle);
            frame = DrawRandom(frame, out ushort sizeDraw);
            short size = (short)(sizeDraw % 864 + 1088);
            frame = Write(frame, first, Read(frame, first).WriteWord(0x1A, (ushort)(angle & 2047)).WriteWord(0x1C, unchecked((ushort)size)).WriteWord(0x1E, unchecked((ushort)(size / 4))));
            (frame, sprite) = RequireAppend(frame, 0x5BBBE0);
            frame = CopyImpactPoint190(frame, sprite, TaskView(Read(frame, first), 0x10, 8));
            frame = DrawRandom(frame, out ushort spriteSize);
            frame = Write(frame, sprite, Read(frame, sprite).WriteWord(12, 0).WriteWord(0x1C, (ushort)(spriteSize % 3072 + 4352)));
            (frame, particles) = RequireAppend(frame, 0x5BC1F0);
            frame = CopyImpactPoint190(frame, particles, TaskView(Read(frame, first), 0x10, 8));
        }
        frame = Write(frame, particles, Read(frame, particles).WriteWord(12, 0).WriteWord(14, task.ReadWord(0x18)));
        return (frame, 2);
    }

    private static NativeBattleChainGun190Frame CopyImpactPoint190(NativeBattleChainGun190Frame frame, uint address, NativeBattleEffectArena point) => WriteBytes(frame, address + 0x10, point, 8);
}
