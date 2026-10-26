using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Step AdvanceActorOverlay648AF0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int actor = Signed(task, 14);
        if (age < 30)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect187WriteModelBytes(actor, 0, NativeBattleEffectArena.Clear().Write(0, [(byte)(inputs.ReadModel(frame, actor).Flags0 | 8)]), 1));
        }
        else if (age < 80)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect187WriteModelBytes(actor, 0, NativeBattleEffectArena.Clear().Write(0, [(byte)(inputs.ReadModel(frame, actor).Flags0 | 4)]), 1));
            (frame, uint temporary) = Allocate(frame, 464);
            NativeBattleEffectArena context = Context(frame, temporary, 464);
            NativeBattleActionMatrix planeRotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
            NativeBattleVector normal = NativeBattleGfMatrices.RotateShort56C4F0(planeRotation, new(0, -4096, 0));
            context = CopyBytes(TaskView(task, 0x10, 8), 0, context, 0x40, 8);
            context = StoreActorVector(context, 0xA0, normal);
            NativeBattleVector point = ReadActorVector(context, 0x40);
            int dot = unchecked(point.X * normal.X + point.Y * normal.Y + point.Z * normal.Z);
            context = context.Write(0xA6, BitConverter.GetBytes(unchecked((short)-(dot >> 12))));
            NativeBattlePackedMatrix32 world = inputs.ReadWorld(frame, actor);
            NativeBattleActionMatrix worldMatrix = world.ReadMatrix();
            NativeBattleVector direction = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Compose56C2F0(worldMatrix, planeRotation), new(4096, 0, 0));
            int distance = Signed(task, 0x1C);
            if (direction.Y < 0)
            {
                distance = -distance;
            }
            byte[] offsetBytes = world.Bytes.ToArray();
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
                int original = BinaryPrimitives.ReadInt32LittleEndian(world.Bytes.AsSpan().Slice(20 + axis * 4, 4));
                BitConverter.GetBytes(unchecked(original + ((component * distance) >> 12))).CopyTo(offsetBytes, 20 + axis * 4);
            }
            NativeBattlePackedMatrix32 offsetWorld = new(offsetBytes.ToImmutableArray(), world.Known);
            if (age >= 65)
            {
                int fade = 4096 - NativeBattleCameraMath.Sine((age - 65) * 1024 / 16);
                int value = unchecked(fade << 7) >> 12;
                uint packed = unchecked((uint)((((value | 0x3200) << 8) | value) << 8 | value));
                frame = inputs.Execute(frame, new NativeBattleEffect187WriteModelBytes(actor, 0x2C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(packed)), 4));
                uint color = (inputs.ReadColor ?? throw new NotSupportedException("Body fading requires its current color control."))(frame, 0xB8B7D8);
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Body fading requires its current primitive controls.");
                NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Body fading requires its current far-color controls.")) with { FarRed = (uint)(byte)color << 4, FarGreen = (uint)(byte)(color >> 8) << 4, FarBlue = (uint)(byte)(color >> 16) << 4 };
                NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Body fading requires current arithmetic registers."), 0, (uint)fade, far, inputs.Colors);
                frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = faded.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                uint output = (faded.PackedRgb & 0xFFFFFF) | 0x02000000;
                frame = inputs.Execute(frame, new NativeBattleEffect187WriteModelBytes(actor, 0x28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(output)), 4));
                frame = inputs.Execute(frame, new NativeBattleEffect187WriteModelBytes(actor, 7, NativeBattleEffectArena.Clear().Write(0, [0]), 1));
            }
            frame = PublishContext(frame, temporary, context, 464);
            frame = inputs.Execute(frame, new NativeBattleEffect187DrawBody464(temporary, actor, offsetWorld));
            frame = Release(frame, 464);
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 0x1E);
        if (age >= 30)
        {
            int position = Signed(task, 0x1C) + (age < 34 ? velocity * 3 : velocity);
            task = task.WriteWord(0x1C, unchecked((ushort)position));
            if (age >= 34)
            {
                velocity = Math.Max(2, (int)unchecked((short)(velocity - velocity / 10)));
                task = task.WriteWord(0x1E, unchecked((ushort)velocity));
            }
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next > 81 ? 2 : 0);
    }

    private static NativeBattleEffectArena StoreActorVector(NativeBattleEffectArena arena, int offset, NativeBattleVector value) => arena.Write(offset, BitConverter.GetBytes(value.X)).Write(offset + 2, BitConverter.GetBytes(value.Y)).Write(offset + 4, BitConverter.GetBytes(value.Z));

    private static NativeBattleVector ReadActorVector(NativeBattleEffectArena arena, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 4, 2)));
}
