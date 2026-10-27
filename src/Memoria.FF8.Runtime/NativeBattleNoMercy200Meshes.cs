namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200MeshDraw(uint Address, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context) : NativeBattleNoMercy200Operation
{
    public NativeBattleShivaScrollingContext? Scrolling { get; init; }
    public uint? Tint { get; init; }
    public bool Common { get; init; }
}

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Begin63ACB0(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleModuleAnchor anchor)
    {
        (pool, int slot) = pool.Append(0x63AD20);
        frame = frame with { Random = frame.Random.Draw(out ushort random) };
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W)).WriteWord(24, (ushort)(random % 2048)).WriteWord(28, 3584).WriteWord(32, 6912);
        return new(frame, pool.WriteSlot(slot, task), 0);
    }

    public static NativeBattleNoMercy200ChildStep Begin63AE80(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool)
    {
        frame = PlaceImpact400(frame);
        (pool, int slot) = pool.Append(0x63AF40);
        frame = frame with { Random = frame.Random.Draw(out ushort random) };
        NativeBattleModulePoolTask task = TaskDword(TaskDword(pool.Slots[slot], 16, unchecked((uint)ReadDword(frame.Staging, 0x24FBCD8))), 20, unchecked((uint)ReadDword(frame.Staging, 0x24FBCDC))).WriteWord(12, 0).WriteWord(24, (ushort)(random % 1024)).WriteWord(26, 40).WriteWord(28, 384).WriteWord(30, 384);
        return new(frame, pool.WriteSlot(slot, task), 0);
    }

    public static NativeBattleNoMercy200ChildStep Begin63CBD0(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool)
    {
        for (ushort index = 0; index < 2; index++)
        {
            (pool, int slot) = pool.Append(0x63CD00);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, index).WriteWord(24, (ushort)(Rand() % 2048));
            int speed = Rand() % 60 + 40;
            task = task.WriteWord(26, unchecked((ushort)((speed & 1) != 0 ? -speed : speed))).WriteWord(28, 56).WriteWord(30, 56).WriteWord(32, (ushort)(Rand() % 4096));
            speed = Rand() % 70 + 30;
            task = task.WriteWord(34, unchecked((ushort)((speed & 1) != 0 ? -speed : speed)));
            pool = pool.WriteSlot(slot, task);
        }
        return new(frame, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }

    public static NativeBattleNoMercy200ChildStep AdvanceSimpleMesh(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x63AD20 or 0x63AF40 or 0x63CD00 or 0x63D230 or 0x63DBD0 or 0x63E4E0))
        {
            throw new InvalidDataException("Native No Mercy mesh requires its original callback.");
        }
        short tick = Word(task, 12);
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleVector rotation;
        NativeBattleVector scale;
        NativeBattleVector point;
        uint address;
        uint flags = 0x33;
        int? fade = null;
        if (callback == 0x63AD20)
        {
            rotation = new(0, Word(task, 24), 0);
            scale = new(Word(task, 28), Word(task, 32), Word(task, 28));
            point = Point(task);
            address = 0xE316B4;
            if (tick < 8 || tick >= 32)
            {
                flags = 0xF3;
                fade = (tick < 8 ? 8 - tick : tick - 32) << 9;
            }
        }
        else if (callback == 0x63AF40)
        {
            rotation = new(0, 64, Word(task, 24));
            scale = new(Word(task, 28), Word(task, 28), Word(task, 28));
            point = Point(task);
            address = 0xE2E15C;
            if (tick >= 10)
            {
                flags = 0xF3;
                fade = 682 * (tick - 10);
            }
        }
        else if (callback == 0x63CD00)
        {
            if (!paused && tick is >= 30 and <= 35)
            {
                frame = PlaceImpact400(frame);
            }
            rotation = new(Word(task, 24), 0, Word(task, 32));
            scale = new(Word(task, 28), Word(task, 28), Word(task, 28));
            point = new(ReadWord(frame.Staging, 0x24FBCD8), ReadWord(frame.Staging, 0x24FBCDA), ReadWord(frame.Staging, 0x24FBCDC));
            address = (task.ReadWord(14) & 1) == 0 ? 0xE328BCu : 0xE33074u;
            if (tick < 12)
            {
                flags = 0xF3;
                fade = 4096 - 341 * tick;
            }
        }
        else
        {
            rotation = callback == 0x63D230 ? new(0, 0, 0) : callback == 0x63DBD0 ? new(Word(task, 24), Word(task, 26), 0) : new(0, Word(task, 24), 0);
            scale = new(Word(task, 28), Word(task, 28), Word(task, 28));
            point = Point(task);
            address = callback == 0x63D230 ? 0xE2FFB4u : callback == 0x63DBD0 ? 0xE2E15Cu : 0xE3102Cu;
            int start = callback == 0x63DBD0 ? 6 : 8;
            if (tick >= start)
            {
                flags = 0xF3;
                fade = (tick - start) << 9;
            }
        }
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(rotation), scale.X, scale.Y, scale.Z) with { X = point.X, Y = point.Y, Z = point.Z };
        NativeBattleActionMatrix matrix;
        if (callback == 0x63AF40)
        {
            NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(point.X, point.Y, point.Z, 0), Scale = Word(task, 28), DepthOffset = -(Word(task, 28) >> 3), SpriteFlags = 0 };
            NativeBattleActionMatrix translation = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, inputs.View, 0);
            matrix = world with { X = translation.X, Y = translation.Y, Z = translation.Z };
        }
        else
        {
            matrix = NativeBattlePoseMatrices.Compose56C2F0(callback == 0x63D230 ? ReadMatrix(frame.Staging, 0x24F9930) : inputs.View, world);
        }
        NativeBattleNoMercy200MeshDraw draw = new(address, matrix, new(flags, 0, fade, null, null, null, 17, 2));
        if (callback == 0x63AD20)
        {
            draw = draw with { Scrolling = new(flags, fade, 0, 8 * tick, 17, 2, new(0, 128, 64, 128)) };
        }
        frame = inputs.Execute(frame, draw);
        if (paused)
        {
            return new(frame, pool, 0);
        }
        if (callback is 0x63D230 or 0x63DBD0 or 0x63E4E0)
        {
            short speed = Word(task, 30);
            int divisor = callback == 0x63D230 ? 6 : callback == 0x63DBD0 ? 4 : 8;
            task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed - speed / divisor)));
            if (callback == 0x63E4E0)
            {
                short angular = Word(task, 26);
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + angular))).WriteWord(26, unchecked((ushort)(angular - (angular >> 4))));
            }
        }
        else if (callback != 0x63AD20)
        {
            short speed = Word(task, 30);
            task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26)))).WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed - speed / (callback == 0x63CD00 ? 18 : 12))));
            if (callback == 0x63CD00)
            {
                task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + Word(task, 34))));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        int end = callback == 0x63AD20 ? 40 : callback == 0x63CD00 ? 37 : callback == 0x63DBD0 ? 14 : 16;
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) >= end ? 2 : 0);
    }

    private static NativeBattleNoMercy200Frame PlaceImpact400(NativeBattleNoMercy200Frame frame)
    {
        NativeBattleFileStaging staging = WriteWord(frame.Staging, 0x24FBCD8, unchecked(ReadWord(frame.Staging, 0x24F9970) + (400 * ReadDword(frame.Staging, 0x24F9980) >> 12)));
        staging = WriteWord(staging, 0x24FBCDA, unchecked(ReadWord(staging, 0x24F9972) + (400 * ReadDword(staging, 0x24F9984) >> 12)));
        staging = WriteWord(staging, 0x24FBCDC, unchecked(ReadWord(staging, 0x24F9974) + (400 * ReadDword(staging, 0x24F9988) >> 12)));
        return frame with { Staging = staging };
    }
}
