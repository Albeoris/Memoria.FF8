using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceTrailMesh(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Resources resources, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback is not (0x64C7F0 or 0x64D6A0 or 0x64D930 or 0x64DAD0))
        {
            throw new InvalidDataException("Effect187 trail requires its actual native callback.");
        }
        bool colored = task.Callback is 0x64C7F0 or 0x64D6A0;
        bool trailing = task.Callback is 0x64D6A0 or 0x64DAD0;
        bool paused = (inputs.ReadFlags(frame) & 0x201) != 0;
        if (Signed(task, 0x22) > 0)
        {
            if (!paused)
            {
                frame = Write(frame, address, task.WriteWord(0x22, unchecked((ushort)(task.ReadWord(0x22) - 1))));
            }
            return new(frame, 0);
        }
        int age = Signed(task, 12);
        short focal = unchecked((short)(inputs.ReadProjection(frame).FocalLength ?? throw new NotSupportedException("Effect187 trail requires its actual focal operand.")));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = focal };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        int selector = Signed(task, 14);
        uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE41EA8u + (uint)(selector * 4)), 4));
        int fade = Signed(task, 0x16);
        int distance = 4096 - fade;
        int initialFade = colored ? 8 : 6;
        if (age < initialFade)
        {
            fade = unchecked(4096 - distance / initialFade * age);
        }
        else if (age >= Signed(task, 0x20) - 8)
        {
            fade = unchecked(fade + (age - (Signed(task, 0x20) - 8)) * (distance / 8));
        }
        int temporaryLength = colored ? 108 : 88;
        (frame, uint temporary) = Allocate(frame, temporaryLength);
        NativeBattleEffectArena context = Context(frame, temporary, temporaryLength).Write(0, BitConverter.GetBytes(mesh)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(fade)).Write(0x1C, BitConverter.GetBytes(0xF3u));
        NativeBattleGfGeometryBatch batch;
        if (colored)
        {
            context = context.Write(0x20, BitConverter.GetBytes(0x2020A0u));
            NativeBattleEffect187ColoredMesh108Step drawn = NativeBattleEffect187ColoredMesh108.Draw64C9A0(frame.Memory, resources.Mesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors);
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Context108;
            batch = drawn.Batch;
        }
        else
        {
            NativeBattleDefaultActMeshStep drawn = NativeBattleEffect187MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors, 3);
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Context88;
            batch = drawn.Batch;
        }
        frame = PublishContext(frame, temporary, context, temporaryLength);
        frame = inputs.Execute(frame, new NativeBattleEffect187DeliverBatch(batch, 0x1D8E054));
        frame = Release(frame, temporaryLength);
        if (paused)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int scaleTicks = colored ? 6 : 3;
        if (age < scaleTicks)
        {
            short velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / (colored ? 3 : 2))));
        }
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, next));
        int end = trailing ? colored ? 7 : 5 : Signed(task, 0x20);
        return new(frame, unchecked((short)next) >= end ? 2 : 0);
    }
}
