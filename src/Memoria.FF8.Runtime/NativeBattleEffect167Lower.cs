using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect167Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect167Lower
{
    public static NativeBattleEffect167Step Advance(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Resources resources, NativeBattleEffect167Inputs inputs) => Read(frame, address).Callback switch
    {
        0x5E2F30 => AdvanceParticles(frame, address, resources, inputs),
        0x5E33E0 => AdvanceTrail(frame, address, resources, inputs),
        0x5E38D0 or 0x5E3A00 => AdvanceStatic(frame, address, resources, inputs),
        0x5E3C20 => AdvanceBody(frame, address, inputs),
        _ => throw new NotSupportedException("Effect167 requires its selected lower callback.")
    };

    public static NativeBattleEffect167Step AdvanceBody(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if (age < 40)
        {
            int actor = Signed(task, 14);
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, actor);
            frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(actor, 0, NativeBattleEffectArena.Clear().Write(0, [(byte)(model.Read(0, 1)[0] | 4)]), 1));
            (frame, uint temporary) = Allocate(frame, 464);
            NativeBattleEffectArena context = Context(frame, temporary, 464);
            NativeBattleActionMatrix planeRotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
            NativeBattleVector normal = NativeBattleGfMatrices.RotateShort56C4F0(planeRotation, new(0, -4096, 0));
            context = CopyBytes(TaskView(task, 0x10, 8), 0, context, 0x40, 8);
            context = LowerStore(context, 0xA0, normal);
            NativeBattleVector point = LowerVector(context, 0x40);
            int dot = unchecked(point.X * normal.X + point.Y * normal.Y + point.Z * normal.Z);
            context = context.Write(0xA6, BitConverter.GetBytes(unchecked((short)-(dot >> 12))));
            NativeBattlePackedMatrix32 world = inputs.ReadWorld(frame, actor);
            NativeBattleActionMatrix worldMatrix = world.ReadMatrix();
            NativeBattleVector direction = NativeBattleGfMatrices.RotateShort56C4F0(worldMatrix, new(4096, 0, 0));
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
            if (age >= 32)
            {
                int fade = 4096 - NativeBattleCameraMath.Sine((age - 32) * 1024 / 8);
                int value = unchecked(fade << 7) >> 12;
                uint packed = unchecked((uint)((((value | 0x3200) << 8) | value) << 8 | value));
                frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(actor, 0x2C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(packed)), 4));
                uint color = (inputs.ReadColor ?? throw new NotSupportedException("Body fading requires its current color control."))(frame, 0xB8B7D8);
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Body fading requires its current primitive controls.");
                NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Body fading requires its current far-color controls.")) with { FarRed = (uint)(byte)color << 4, FarGreen = (uint)(byte)(color >> 8) << 4, FarBlue = (uint)(byte)(color >> 16) << 4 };
                NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Body fading requires current arithmetic registers."), 0, (uint)fade, far, inputs.Colors);
                frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = faded.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                uint output = (faded.PackedRgb & 0xFFFFFF) | 0x02000000;
                frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(actor, 0x28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(output)), 4));
                frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(actor, 7, NativeBattleEffectArena.Clear().Write(0, [0]), 1));
            }
            frame = PublishContext(frame, temporary, context, 464);
            frame = inputs.Execute(frame, new NativeBattleEffect167DrawBody464(temporary, actor, offsetWorld) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(167, Read(frame, address)) : null });
            frame = Release(frame, 464);
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = Signed(task, 0x1E);
        int position = Signed(task, 0x1C) + (age < 4 ? velocity * 3 : velocity);
        if (age >= 4)
        {
            velocity = Math.Max(2, (int)unchecked((short)(velocity - velocity / 10)));
            task = task.WriteWord(0x1E, unchecked((ushort)velocity));
        }
        short next = unchecked((short)(age + 1));
        task = task.WriteWord(0x1C, unchecked((ushort)position)).WriteWord(12, unchecked((ushort)next));
        return new(Write(frame, address, task), next > 40 ? 2 : 0);
    }

    private static ushort LowerWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short LowerSigned(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint LowerDword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena LowerPut(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena LowerStore(NativeBattleEffectArena arena, int offset, NativeBattleVector value) => arena.Write(offset, BitConverter.GetBytes(value.X)).Write(offset + 2, BitConverter.GetBytes(value.Y)).Write(offset + 4, BitConverter.GetBytes(value.Z));
    private static NativeBattleVector LowerVector(NativeBattleEffectArena arena, int offset) => new(LowerSigned(arena, offset), LowerSigned(arena, offset + 2), LowerSigned(arena, offset + 4));
}
