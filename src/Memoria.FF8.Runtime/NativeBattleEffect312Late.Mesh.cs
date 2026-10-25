using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Late
{
    private static NativeBattlePackedMatrix32 Matrix(NativeBattleEffect312Frame frame, NativeBattleVector position, NativeBattleVector angles, NativeBattleVector scale, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles);
        local = local with { Rotation = NativeBattlePoseMatrices.ScaleRotation56BEF0(local.Rotation, scale), X = position.X, Y = position.Y, Z = position.Z };
        return NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null);
    }

    private static NativeBattleEffect312Step Mesh(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        int age = TaskWord(task, 0x0C);
        if (callback == 0x5A03D0 && TaskWord(task, 0x0E) > 0)
        {
            if (!Paused(frame, inputs))
            {
                frame = Write(frame, address, TaskPut(task, 0x0E, TaskWord(task, 0x0E) - 1));
            }
            return new(frame, 0);
        }
        short scale = TaskWord(task, 0x1C);
        NativeBattleVector scales = callback switch
        {
            0x5A0030 or 0x5A0150 => new(4096, scale, 4096),
            0x5A0290 or 0x5A03D0 => new(scale, TaskWord(task, 0x20), scale),
            _ => new(scale, scale, scale)
        };
        NativeBattleVector angles = callback == 0x5A03D0 ? new(0, TaskWord(task, 0x18), 0) : callback == 0x5A0F50 ? new(1024, 0, 0) : new(0, 0, 0);
        NativeBattlePackedMatrix32 matrix = Matrix(frame, Vector(ReadBytes(frame, address + 0x10, 8)), angles, scales, inputs);
        frame = LoadMatrix(frame, matrix);
        int length = callback == 0x5A0030 ? 96 : callback == 0x5A03D0 ? 124 : 88;
        (frame, uint temporary) = Allocate(frame, length);
        NativeBattleEffectArena context = ReadBytes(frame, temporary, length);
        context = Put(context, 8, 0);
        uint mesh;
        if (callback == 0x5A0030)
        {
            mesh = 0xCDAF9C;
            context = Put(Put(Put(context, 0x1C, 0), 0x58, 0xE1000020), 0x5C, 0xE1000220);
        }
        else if (callback == 0x5A0150)
        {
            mesh = 0xCDCCB4;
            context = Put(Put(context, 0x0C, age < 8 ? unchecked((uint)((16 - age) * 256)) : 2048), 0x1C, 0xC3);
        }
        else if (callback == 0x5A0290)
        {
            mesh = 0xCD98F4;
            context = Put(Put(context, 0x0C, age >= 0 ? unchecked((uint)(age * 204)) : 0), 0x1C, 0xF3);
        }
        else if (callback == 0x5A03D0)
        {
            bool variant = TaskWord(task, 0x1A) != 0;
            mesh = variant ? 0xCD978Cu : 0xCD9624u;
            context = Put(Put(Put(Put(context, 0x10, 0), 0x14, unchecked((uint)(age * TaskWord(task, 0x22)))), 0x20, 0x33), 0, mesh);
            context = PutWord(PutWord(PutWord(PutWord(context, 0x18, variant ? 64 : 0), 0x1A, 0), 0x1C, 64), 0x1E, 128);
            if (age < 4)
            {
                context = Put(Put(context, 0x0C, unchecked((uint)((4 - age) * 1024))), 0x20, 0xF3);
            }
            else if (age >= 10)
            {
                context = Put(Put(context, 0x0C, unchecked((uint)((age - 10) * 512))), 0x20, 0xF3);
            }
        }
        else
        {
            mesh = 0xCDE73C;
            context = Put(context, 0x1C, 0x33);
            if (age >= 2)
            {
                context = Put(Put(context, 0x1C, 0xF3), 0x0C, unchecked((uint)((age - 2) * 409)));
            }
        }
        context = Put(context, 0, mesh);
        frame = WriteBytes(frame, temporary, context, length);
        if (callback == 0x5A0030)
        {
            NativeBattleEffect166ModeMesh96Step drawn = NativeBattleEffect166ModeMesh96.DrawLoaded(frame.Memory, resources.ReadMesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors);
            frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary, drawn.Context96, length);
            frame = inputs.Execute(frame, new NativeBattleEffect312DrawBatch(drawn.Batch));
        }
        else if (callback == 0x5A03D0)
        {
            NativeBattleScrollingMeshContext124Step drawn = NativeBattleScrollingMeshContext124.DrawLoaded(frame.Memory, resources.ReadMesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors);
            frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary, drawn.Context124, length);
            frame = inputs.Execute(frame, new NativeBattleEffect312DrawBatch(drawn.Batch));
        }
        else
        {
            frame = NativeBattleEffect312Mesh.Draw572200(frame, temporary, mesh, matrix, resources, inputs);
        }
        frame = Release(frame, length);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        if (callback is 0x5A0030 or 0x5A0150)
        {
            if (age is >= 0 and < 8)
            {
                task = TaskPut(task, 0x1C, scale + TaskWord(task, 0x1E));
            }
        }
        else if (callback == 0x5A0290)
        {
            int velocity = TaskWord(task, 0x22);
            task = TaskPut(TaskPut(task, 0x20, TaskWord(task, 0x20) + velocity), 0x22, velocity - velocity / 5);
        }
        else if (callback == 0x5A0F50)
        {
            int velocity = TaskWord(task, 0x1E);
            task = TaskPut(TaskPut(task, 0x1C, scale + velocity), 0x1E, velocity - velocity / 5);
        }
        age = unchecked((short)(age + 1));
        frame = Write(frame, address, TaskPut(task, 0x0C, age));
        int lifetime = callback is 0x5A0030 or 0x5A0150 ? 8 : callback == 0x5A0290 ? 20 : callback == 0x5A03D0 ? 18 : 12;
        return new(frame, age >= lifetime ? 2 : 0);
    }
}
