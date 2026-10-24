using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    public static NativeBattleEffect166Step AdvanceStatic(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        int age = Signed(task, 12);
        if (callback == 0x5E86D0 && Signed(task, 14) > 0)
        {
            if ((inputs.ReadFlags(frame) & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(Signed(task, 14) - 1))));
            }
            return new(frame, 0);
        }
        NativeBattleActionMatrix local;
        bool anchored = callback is 0x5E5980 or 0x5E5AB0 or 0x5E5C30;
        if (anchored)
        {
            NativeBattleEffectArena raw = ReadBytes(frame, MatrixAddress, 32);
            local = new NativeBattlePackedMatrix32(raw.ReadSnapshot(0, 32).Data.ToImmutableArray(), raw.ReadSnapshot(0, 32).Known.ToImmutableArray()).ReadMatrix();
            if (callback == 0x5E5AB0)
            {
                NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
                local = NativeBattleGfMatrices.Multiply56C090(local, spin) with { X = local.X, Y = local.Y, Z = local.Z };
            }
        }
        else
        {
            NativeBattleVector angle = callback == 0x5E86D0 ? new(0, Signed(task, 0x18), 0) : callback == 0x5E9250 ? new(1024, 0, 0) : new(0, 0, 0);
            local = NativeBattleGfMatrices.Euler56CE30(angle) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        }
        int scale = Signed(task, 0x1C);
        int sx = scale;
        int sy = scale;
        int sz = scale;
        if (callback is 0x5E8330 or 0x5E8450)
        {
            sx = sz = 4096;
        }
        else if (callback is 0x5E8590 or 0x5E86D0)
        {
            sy = Signed(task, 0x20);
        }
        local = NativeBattlePoseMatrices.Scale56BEF0(local, sx, sy, sz);
        NativeBattleActionMatrix viewed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewed, null));
        int length = callback == 0x5E8330 ? 96 : callback == 0x5E86D0 ? 124 : 88;
        (frame, uint temporary) = Allocate(frame, length);
        NativeBattleEffectArena context = Context(frame, temporary, length).Write(8, BitConverter.GetBytes(0u));
        if (callback == 0x5E8330)
        {
            context = context.Write(0, BitConverter.GetBytes(0xD7398Cu)).Write(0x1C, BitConverter.GetBytes(0u)).Write(0x58, BitConverter.GetBytes(0xE1000020u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u));
            NativeBattleEffect166ModeMesh96Step drawn = NativeBattleEffect166ModeMesh96.DrawLoaded(frame.Memory, resources.ReadMesh(0xD7398C), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context96, length);
            frame = inputs.Execute(frame, new NativeBattleEffect166DrawBatch(drawn.Batch, CursorGlobal));
        }
        else if (callback == 0x5E86D0)
        {
            uint source = Signed(task, 0x1A) == 0 ? 0xD72014u : 0xD7217Cu;
            context = context.Write(0, BitConverter.GetBytes(source)).Write(0x10, BitConverter.GetBytes(0)).Write(0x14, BitConverter.GetBytes(Signed(task, 0x22) * age)).Write(0x18, BitConverter.GetBytes((ushort)(Signed(task, 0x1A) == 0 ? 0 : 64))).Write(0x1A, BitConverter.GetBytes((ushort)0)).Write(0x1C, BitConverter.GetBytes((ushort)64)).Write(0x1E, BitConverter.GetBytes((ushort)128)).Write(0x20, BitConverter.GetBytes(0x33u));
            if (age < 4 || age >= 10)
            {
                context = context.Write(12, BitConverter.GetBytes(age < 4 ? (4 - age) << 10 : (age - 10) << 9)).Write(0x20, BitConverter.GetBytes(0xF3u));
            }
            NativeBattleEffect315Scrolling124Step drawn = NativeBattleEffect315Scrolling124.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context124, length);
            frame = inputs.Execute(frame, new NativeBattleEffect166DrawBatch(drawn.Batch, CursorGlobal));
        }
        else
        {
            uint source = callback switch { 0x5E5980 => 0xD7688C, 0x5E5AB0 => 0xD767BC, 0x5E8450 => 0xD756A4, 0x5E8590 => 0xD722E4, _ => 0xD7712C };
            uint flags = callback == 0x5E8450 ? 0xC3u : callback == 0x5E8590 ? 0xF3u : 0x33u;
            context = context.Write(0, BitConverter.GetBytes(source)).Write(0x1C, BitConverter.GetBytes(flags));
            int? cue = null;
            if (callback == 0x5E5980 && (age == 0 || age >= 12))
            {
                cue = age == 0 ? 2048 : (age - 12) << 9;
                flags = 0xF3;
            }
            else if (callback == 0x5E5AB0 && age < 4)
            {
                cue = ((8 - age) << 9) | 0xC0;
            }
            else if (callback == 0x5E5AB0 && age >= 18)
            {
                cue = (age - 18) << 9;
                flags = 0xF3;
            }
            else if (callback == 0x5E5C30 && age >= 1)
            {
                cue = 372 * (age - 1);
                flags = 0xF3;
            }
            else if (callback == 0x5E8450)
            {
                cue = age < 8 ? (16 - age) << 8 : 2048;
            }
            else if (callback == 0x5E8590)
            {
                cue = age < 0 ? 0 : age * 204;
            }
            else if (callback == 0x5E9250 && age >= 2)
            {
                cue = (age - 2) * 409;
                flags = 0xF3;
            }
            context = context.Write(0x1C, BitConverter.GetBytes(flags));
            if (cue is int factor)
            {
                context = context.Write(12, BitConverter.GetBytes(factor));
            }
            byte shift = anchored ? (byte)3 : (byte)2;
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, shift);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context, length);
            frame = inputs.Execute(frame, new NativeBattleEffect166DrawBatch(drawn.Batch, CursorGlobal));
        }
        frame = Release(frame, length);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (callback == 0x5E5980 && age < 8)
        {
            int velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) - velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 6)));
        }
        else if (callback == 0x5E5AB0)
        {
            int velocity = Signed(task, 0x1A);
            task = task.WriteWord(0x18, unchecked((ushort)(Signed(task, 0x18) + velocity))).WriteWord(0x1A, unchecked((ushort)(velocity - velocity / 8)));
        }
        else if (callback is 0x5E5C30 or 0x5E9250)
        {
            int velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / (callback == 0x5E5C30 ? 6 : 5))));
        }
        else if ((callback is 0x5E8330 or 0x5E8450) && age >= 0 && age < 8)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) + Signed(task, 0x1E))));
        }
        else if (callback == 0x5E8590)
        {
            int velocity = Signed(task, 0x22);
            task = task.WriteWord(0x20, unchecked((ushort)(Signed(task, 0x20) + velocity))).WriteWord(0x22, unchecked((ushort)(velocity - velocity / 5)));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        int end = callback switch { 0x5E5980 => 20, 0x5E5AB0 => 26, 0x5E8330 or 0x5E8450 => 8, 0x5E8590 => 20, 0x5E86D0 => 18, _ => 12 };
        return new(frame, next >= end ? 2 : 0);
    }
}
