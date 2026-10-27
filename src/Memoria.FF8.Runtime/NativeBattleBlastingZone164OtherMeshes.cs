namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep AdvanceOtherMesh(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view, short focalLength)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if ((callback is 0x5F1500 or 0x5F4180) && Word(task, 14) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool.WriteSlot(slot, task), 0);
        }
        short counter = Word(task, 12);
        switch (callback)
        {
            case 0x5F1500:
            case 0x5F4180:
                NativeBattleActionMatrix delayed = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Word(task, 28), Word(task, 32), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
                bool ringFade = counter < 6 || counter >= 24;
                int ringFactor = counter < 6 ? 4096 - 682 * counter : (counter - 24) << 8;
                bool rising = callback == 0x5F4180;
                Emit(rising ? 0xD8A048u : 0xD94FACu, NativeBattlePoseMatrices.Compose56C2F0(view, delayed), rising ? counter >= 12 ? 0xF3u : 0x33u : ringFade ? 0xF3u : 0x33u, rising ? counter >= 12 ? (counter - 12) << 9 : null : ringFade ? ringFactor : null, rising);
                break;
            case 0x5F3DF0:
                NativeBattleActionMatrix outer = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Word(task, 28), Word(task, 32), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
                outer = NativeBattlePoseMatrices.Compose56C2F0(view, outer);
                Emit(0xD9339C, outer, 0xCC, 2560, true, false, 4122, 6);
                NativeBattleActionMatrix inner = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 26), 0)), Word(task, 28) - 256, Word(task, 32), Word(task, 28) - 256) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
                inner = AtCamera(NativeBattlePoseMatrices.Compose56C2F0(view, inner), outer);
                Emit(0xD9339C, inner, 0xC3, 1536, true, false, 4122, 7);
                break;
            case 0x5F44F0:
                NativeBattleActionMatrix disk = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), Word(task, 28), Word(task, 28), Word(task, 28)) with { X = 0, Y = Word(task, 18), Z = focalLength };
                Emit(0xD8B880, disk, 0xCC, 1024, false, false, 4123, 12);
                int diskScale = Word(task, 28) + 96;
                NativeBattleActionMatrix border = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), diskScale, diskScale, diskScale) with { X = 0, Y = Word(task, 18), Z = focalLength };
                Emit(0xD8B880, border, 0xC3, 32 * (80 - counter), false, false, 4122, 12);
                break;
            case 0x5F49B0:
            case 0x5F4B70:
                NativeBattleActionMatrix ring = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(1024, Word(task, 24), 0)), Word(task, 28), Word(task, 28), 0) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
                bool early = callback == 0x5F49B0;
                bool fading = early ? counter < 4 : counter >= 2;
                int fade = early ? (4 - counter) << 10 : (counter - 2) << 8;
                Emit(early ? 0xD8B610u : 0xD95B7Cu, NativeBattlePoseMatrices.Compose56C2F0(view, ring), fading ? 0xF3u : 0x33u, fading ? fade : null);
                break;
            case 0x5F4FA0:
                NativeBattleRenzokuken160WeaponTip tip = inputs.ReadWeaponTip(frame.Parameters.Actor78);
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, tip.DirectionX, tip.DirectionY, tip.DirectionZ);
                NativeBattleActionMatrix aligned = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                aligned = NativeBattleGfMatrices.Multiply56C090(aligned, ReadMatrix(frame.Staging, 0x23C8F20)) with { X = tip.Position.X, Y = tip.Position.Y, Z = tip.Position.Z };
                aligned = NativeBattlePoseMatrices.Scale56BEF0(aligned, Word(task, 28), Word(task, 32), Word(task, 28));
                Emit(0xD8EAA8, NativeBattlePoseMatrices.Compose56C2F0(view, aligned), counter >= 57 ? 0xF3u : 0x33u, counter >= 57 ? (counter - 57) << 9 : null, false, true);
                break;
            case 0x5F5430:
                NativeBattleActionMatrix eruption = NativeBattlePoseMatrices.Compose56C2F0(view, World(task, Word(task, 28), Word(task, 32), Word(task, 28)));
                Scroll(0xD97D6C, eruption, counter >= 14 ? 0xF3u : 0x33u, counter >= 14 ? 341 * (counter - 14) : null, 24 * counter);
                break;
            case 0x5F55D0:
                NativeBattleActionMatrix source = ReadMatrix(frame.Staging, 0x23C9D68);
                NativeBattleActionMatrix end = NativeBattlePoseMatrices.Compose56C2F0(source, World(task, 14336, Word(task, 32), 4096));
                uint endFlags = counter >= 18 ? 0xF3u : 0x33u;
                int? endFade = counter >= 18 ? (counter - 18) << 9 : null;
                Scroll(0xD95DEC, end, endFlags, endFade, Word(task, 28));
                NativeBattleActionMatrix second = AtCamera(NativeBattlePoseMatrices.Compose56C2F0(source, World(task, 14464, Word(task, 32), 4288)), end);
                Scroll(0xD95DEC, second, endFlags, endFade, Word(task, 28));
                break;
            default:
                throw new NotSupportedException($"Native Blasting Zone mesh callback0x{callback:X} is unsupported.");
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        int lifetime;
        switch (callback)
        {
            case 0x5F1500:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                task = Damp(task, 32, 34, 32);
                lifetime = 40;
                break;
            case 0x5F4180:
                short angularSpeed = Word(task, 26);
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + angularSpeed))).WriteWord(26, unchecked((ushort)(angularSpeed - angularSpeed / 5)));
                task = Damp(Damp(task, 28, 30, 6), 32, 34, 12);
                lifetime = 20;
                break;
            case 0x5F3DF0:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + 4))).WriteWord(26, unchecked((ushort)(Word(task, 26) - 2)));
                lifetime = 40;
                break;
            case 0x5F44F0:
                if (counter < 18)
                {
                    short speed = Word(task, 34);
                    task = task.WriteWord(18, unchecked((ushort)(Word(task, 18) + speed)));
                    if (speed > 0)
                    {
                        task = task.WriteWord(34, unchecked((ushort)(speed - 1)));
                    }
                }
                lifetime = 60;
                break;
            case 0x5F49B0:
            case 0x5F4B70:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                task = Damp(task, 28, 30, callback == 0x5F49B0 ? 16 : 12);
                lifetime = callback == 0x5F49B0 ? 39 : 18;
                break;
            case 0x5F4FA0:
                lifetime = 65;
                break;
            case 0x5F5430:
                lifetime = 26;
                break;
            case 0x5F55D0:
                short vSpeed = Word(task, 30);
                short expansion = Word(task, 34);
                task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + vSpeed))).WriteWord(30, unchecked((ushort)(vSpeed - 1))).WriteWord(32, unchecked((ushort)(Word(task, 32) + expansion))).WriteWord(34, unchecked((ushort)(expansion - expansion / 4)));
                lifetime = 26;
                break;
            default:
                throw new InvalidOperationException();
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = callback == 0x5F44F0 ? Word(task, 12) >= lifetime : task.ReadWord(12) >= lifetime;
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        void Emit(uint address, NativeBattleActionMatrix matrix, uint flags, int? fade, bool common = false, bool tinted = false, int table = 17, byte shift = 2)
        {
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(address, matrix, new(flags, 0, fade, null, null, null, table, shift), common, tinted));
        }
        void Scroll(uint address, NativeBattleActionMatrix matrix, uint flags, int? fade, int v)
        {
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(address, matrix, new(flags, 0, fade, null, null, null, 17, 2), false) { Scrolling = new(flags, fade, 0, v, 17, 2, new(0, 0, 64, 128)) });
        }
    }
}
