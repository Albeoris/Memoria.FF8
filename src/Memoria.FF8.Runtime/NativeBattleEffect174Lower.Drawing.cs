using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Lower
{
    private static NativeBattleEffect174Step AdvanceMesh(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        int age = Signed(task, 12);
        short angle = Signed(task, 0x18);
        short scale = Signed(task, 0x1C);
        bool custom = callback == 0x5D5690;
        bool camera = callback == 0x5D5560;
        bool billboard = callback == 0x5D7090;
        NativeBattleVector angles = custom ? new(0, angle, Signed(task, 0x20)) : camera || billboard ? new(0, 0, angle) : new(0, angle, 0);
        NativeBattleActionMatrix local = custom ? NativeBattleGfMatrices.Euler56CD50(angles) : NativeBattleGfMatrices.Euler56CE30(angles);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        NativeBattleActionMatrix matrix;
        if (camera)
        {
            matrix = local with { X = 0, Y = 0, Z = inputs.ReadScalarFocal(frame) };
        }
        else
        {
            NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
            NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
            if (billboard)
            {
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, position.X, position.Y, position.Z);
                matrix = local with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
            }
            else
            {
                matrix = NativeBattleGfMatrices.Compose56C2F0(view, local with { X = position.X, Y = position.Y, Z = position.Z });
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int length = custom ? 108 : 88;
        (frame, uint temporary) = Allocate(frame, length);
        uint source;
        int? fade;
        int terminal;
        switch (callback)
        {
            case 0x5D51C0:
                source = 0xD5DF34;
                fade = age < 4 ? (4 - age) << 10 : null;
                terminal = 30;
                break;
            case 0x5D52F0:
                source = 0xD5E2CC;
                fade = age >= 2 ? (age - 2) << 9 : null;
                terminal = 10;
                break;
            case 0x5D5560:
                source = 0xD597DC;
                fade = age < 8 ? (8 - age) << 9 : age >= 50 ? (age - 50) << 9 : null;
                terminal = 58;
                break;
            case 0x5D5690:
                source = 0xD5ADCC;
                fade = age < 4 ? (4 - age) << 10 : null;
                terminal = 40;
                break;
            case 0x5D7090:
                source = 0xD5D684;
                fade = age < 6 ? 4096 - age * 682 : age >= 22 ? (age - 22) * 682 : null;
                terminal = 28;
                break;
            default:
                throw new NotSupportedException("Effect174 requires its selected mesh callback.");
        }
        NativeBattleEffectArena context = L174Put(L174Put(L174Put(Context(frame, temporary, length), 0, source), 8, 0), 0x1C, fade.HasValue ? 0xF3u : 0x33u);
        if (fade is int value)
        {
            context = L174Put(context, 12, unchecked((uint)value));
        }
        if (custom)
        {
            int index = age % 12;
            if (index < 0)
            {
                throw new InvalidDataException("Effect174 leaves its selected material cycle.");
            }
            context = L174Put(context, 0x20, resources.MaterialCycle12[index]);
            NativeBattleDegenerator306CustomMeshStep drawn = NativeBattleDegenerator306CustomMesh.Draw5A3AC0(frame.Memory, resources.ReadMesh(source), context, inputs.ReadProjection(frame), inputs.Colors, 17, 2);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, length);
            frame = inputs.Execute(frame, new NativeBattleEffect174DrawBatch(drawn.Batch, L174Cursor));
        }
        else
        {
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, camera ? 4122 : 17, camera ? (byte)14 : (byte)2);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, length);
            frame = inputs.Execute(frame, new NativeBattleEffect174DrawBatch(drawn.Batch, L174Cursor));
        }
        frame = Release(frame, length);
        if (L174Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (callback == 0x5D51C0)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - task.ReadWord(0x1E))));
        }
        else if (callback == 0x5D52F0)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 16)));
        }
        else
        {
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A))));
            if (custom)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - task.ReadWord(0x1E))));
            }
            else if (billboard)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E))));
            }
        }
        return L174Age(Write(frame, address, task), address, terminal, inputs);
    }

    private static NativeBattleEffect174Step AdvanceCameraSprite(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if ((age & 1) != 0)
        {
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), 256, 256, 256) with { X = 0, Y = 0, Z = inputs.ReadScalarFocal(frame) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
            (frame, uint temporary) = Allocate(frame, 180);
            NativeBattleEffectArena context = L174Word(L174Word(L174Put(Context(frame, temporary, 180), 0, 0xD59460), 4, 0), 0x24, 0);
            frame = L174Sprite(frame, temporary, context, resources, inputs, 14);
            frame = Release(frame, 180);
        }
        return L174Age(frame, address, 30, inputs);
    }

    private static NativeBattleEffect174Step AdvanceBody(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int actor = checked((int)frame.State.ReadDword(0x2394208));
        if (age < 30)
        {
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, actor);
            frame = L174WriteModel(frame, actor, 0, NativeBattleEffectArena.Clear().Write(0, [(byte)(model.Read(0, 1)[0] | 4)]), 1, inputs);
            (frame, uint temporary) = Allocate(frame, 136);
            NativeBattleEffectArena context = L174Put(Context(frame, temporary, 136), 0x2C, unchecked((uint)((age << 12) / 30)));
            context = CopyBytes(TaskView(task, 0x10, 8), 0, context, 0x60, 8);
            frame = PublishContext(frame, temporary, context, 136);
            frame = inputs.Execute(frame, new NativeBattleEffect174DrawBody136(temporary, actor));
            frame = Release(frame, 136);
        }
        return L174Age(frame, address, 31, inputs);
    }

    private static NativeBattleEffect174Frame L174Sprite(NativeBattleEffect174Frame frame, uint temporary, NativeBattleEffectArena context, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs, byte depthShift = 2)
    {
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, depthShift);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        return inputs.Execute(frame, new NativeBattleEffect174DrawBatch(drawn.Batch, L174Cursor));
    }
}
