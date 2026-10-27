namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlastingZone164MeshDraw(uint Address, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context, bool Common, bool Tinted = false) : NativeBattleBlastingZone164Operation
{
    public uint? SelectedVerticesAddress { get; init; }
    public NativeBattleShivaScrollingContext? Scrolling { get; init; }
}

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep AdvanceBasicMesh(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        uint callback = task.Callback;
        switch (callback)
        {
            case 0x5F0B70:
                NativeBattleActionMatrix firstCamera = Billboard(task, Word(task, 28), -(Word(task, 28) >> 4), view);
                NativeBattleActionMatrix first = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 24))), Word(task, 28), Word(task, 28), Word(task, 28));
                Emit(0xD8F8B8, AtCamera(first, firstCamera), counter >= 8 ? 0xF3u : 0x33u, counter >= 8 ? 682 * (counter - 8) : null);
                break;
            case 0x5F0D10:
                NativeBattleActionMatrix secondCamera = Billboard(task, Word(task, 28), -(Word(task, 28) >> 4), view);
                NativeBattleActionMatrix second = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 24))), Word(task, 28), Word(task, 28), Word(task, 28));
                uint secondFlags = counter < 8 ? 0xF3u : 0x33u;
                int? secondFade = counter < 8 ? (8 - counter) << 9 : null;
                Emit(0xD909F0, AtCamera(second, secondCamera), secondFlags, secondFade);
                NativeBattleActionMatrix reflected = NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)-Word(task, 24))));
                Emit(0xD909F0, AtCamera(reflected, secondCamera), secondFlags, secondFade);
                break;
            case 0x5F1E70:
                Emit(0xD90F00, Billboard(task, Word(task, 28), -Word(task, 22), view), counter >= 20 ? 0xF3u : 0x33u, counter >= 20 ? (counter - 20) << 9 : null);
                break;
            case 0x5F1F80:
                NativeBattleActionMatrix origin = ReadMatrix(frame.Staging, 0x23C9D68);
                NativeBattleActionMatrix marker = World(task, Word(task, 28), Word(task, 28), Word(task, 28));
                Emit(0xD92154, NativeBattlePoseMatrices.Compose56C2F0(origin, marker), counter >= 4 ? 0xF3u : 0x33u, counter >= 4 ? 682 * (counter - 4) : null);
                break;
            case 0x5F2120:
            case 0x5F3B30:
                NativeBattleActionMatrix pillar = World(task, Word(task, 28), Word(task, 32), Word(task, 28));
                Emit(0xD8C130, NativeBattlePoseMatrices.Compose56C2F0(view, pillar), 0x33, null, callback == 0x5F3B30, true, callback == 0x5F3B30 ? (byte)2 : (byte)3);
                break;
            case 0x5F3FA0:
                NativeBattleActionMatrix shardCamera = Billboard(task, Word(task, 28), 0, view);
                NativeBattleActionMatrix shard = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Word(task, 28), Word(task, 32), 0);
                Emit(0xD8B180, AtCamera(shard, shardCamera), 0x33, null, true);
                break;
            default:
                throw new NotSupportedException($"Native Blasting Zone mesh callback0x{callback:X} is unsupported.");
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        int lifetime;
        switch (callback)
        {
            case 0x5F0B70:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                task = Damp(task, 28, 30, 5);
                lifetime = 14;
                break;
            case 0x5F0D10:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                lifetime = 58;
                break;
            case 0x5F1E70:
                if (counter == 18)
                {
                    task = task.WriteWord(30, 1024);
                }
                task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + Word(task, 30))));
                lifetime = 28;
                break;
            case 0x5F1F80:
                task = Damp(task, 28, 30, 6);
                lifetime = 10;
                break;
            case 0x5F2120:
                short shrink = Word(task, 30);
                task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) - shrink))).WriteWord(30, unchecked((ushort)(shrink - shrink / 64)));
                lifetime = 17;
                break;
            case 0x5F3B30:
                if (counter < 20)
                {
                    short speed = Word(task, 34);
                    task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + speed))).WriteWord(34, unchecked((ushort)(speed + speed / 16)));
                    if (Word(task, 32) >= 28672)
                    {
                        task = task.WriteWord(32, 28672);
                    }
                }
                lifetime = 40;
                break;
            case 0x5F3FA0:
                task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + Word(task, 30))));
                lifetime = 40;
                break;
            default:
                throw new InvalidOperationException();
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = callback is 0x5F1E70 or 0x5F3B30 ? Word(task, 12) >= lifetime : task.ReadWord(12) >= lifetime;
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        void Emit(uint address, NativeBattleActionMatrix matrix, uint flags, int? fade, bool common = false, bool tinted = false, byte shift = 2)
        {
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164MeshDraw(address, matrix, new(flags, 0, fade, null, null, null, 17, shift), common, tinted));
        }
    }

    private static NativeBattleActionMatrix World(NativeBattleModulePoolTask task, int xScale, int yScale, int zScale)
    {
        return NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), xScale, yScale, zScale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
    }

    private static NativeBattleActionMatrix Billboard(NativeBattleModulePoolTask task, short scale, int depth, NativeBattleActionMatrix view)
    {
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(Word(task, 16), Word(task, 18), Word(task, 20), 0), Scale = scale, DepthOffset = depth, SpriteFlags = 0 };
        return NativeBattlePoseMatrices.ModuleSpriteCamera(draw, view, 0);
    }

    private static NativeBattleActionMatrix AtCamera(NativeBattleActionMatrix rotation, NativeBattleActionMatrix camera) => rotation with { X = camera.X, Y = camera.Y, Z = camera.Z };

    private static NativeBattleModulePoolTask Damp(NativeBattleModulePoolTask task, int scaleOffset, int speedOffset, int divisor)
    {
        short speed = Word(task, speedOffset);
        return task.WriteWord(scaleOffset, unchecked((ushort)(Word(task, scaleOffset) + speed))).WriteWord(speedOffset, unchecked((ushort)(speed - speed / divisor)));
    }
}
