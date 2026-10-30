using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMorph172Controller
{
    public static NativeBattleMorph172Frame InitializeRadii5D9EE0(NativeBattleMorph172Frame frame, NativeBattleMorph172Resources resources)
    {
        for (int index = 0; index < 261; index++)
        {
            int at = 8 + index * 8;
            int x = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(at));
            int y = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(at + 2));
            int radius = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + y * y));
            frame = frame with { State = frame.State.WriteDword(0x239BA90 + (uint)(index * 4), unchecked((uint)radius)) };
        }
        return frame;
    }

    public static NativeBattleMorph172Frame DeformVertices(NativeBattleMorph172Frame frame, NativeBattleMorph172Resources resources, bool first, int amplitude)
    {
        uint destination = first ? 0x239BEB8u : 0x239B268u;
        for (int index = 0; index < 261; index++)
        {
            int at = 8 + index * 8;
            int radius = unchecked((int)frame.State.ReadDword(0x239BA90 + (uint)(index * 4)));
            int phase = unchecked(NativeBattleCameraMath.Sine(unchecked(3 * radius)) * amplitude) >> 12;
            int cosine = NativeBattleCameraMath.Cosine(phase);
            int sine = NativeBattleCameraMath.Sine(phase);
            int x = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(at));
            int y = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(at + 2));
            int outputX = unchecked((unchecked(x * cosine) >> 12) + (unchecked(y * sine) >> 12));
            int outputY = unchecked((unchecked(-x * sine) >> 12) + (unchecked(y * cosine) >> 12));
            uint output = destination + (uint)(index * 8);
            frame = frame with { State = frame.State.WriteWord(output, unchecked((ushort)outputX)).WriteWord(output + 2, unchecked((ushort)outputY)).WriteWord(output + 4, BinaryPrimitives.ReadUInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(at + 4))) };
        }
        return frame;
    }

    public static (NativeBattleMorph172Frame Frame, int Result) AdvanceMesh(NativeBattleMorph172Frame frame, uint address, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x5D9F20;
        if (!first && task.Callback != 0x5DA0F0)
        {
            throw new NotSupportedException("Morph mesh callback is unsupported.");
        }
        short size = Signed(task, 0x1C);
        frame = SetupSprite172(frame, inputs, TaskView(task, 0x10, 8), size, unchecked((short)(size >> 3)));
        NativeBattleActionMatrix basis = (frame.Memory.ModuleSpriteBillboard21DFED0 ?? throw new NotSupportedException("Morph requires its current billboard matrix.")).ReadMatrix();
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)(Signed(task, 0x18) * (first ? -2 : -3)))));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Multiply56C090(basis, euler), null), true, false);
        frame = DeformVertices(frame, resources, first, 0);
        short age = Signed(task, 12);
        int cue = age < 8 ? (32 - age) << 7 : age >= 48 ? (age - 24) << 7 : 3072;
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xD6299Cu)).Write(4, BitConverter.GetBytes(first ? 0x239BEB8u : 0x239B268u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(0x20F3u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleMorph172DrawStatic88(temporary, 0xD6299C, 17, 2) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(172, task) with { ResourceGeneration = unchecked(0xD6299Cu * 397 ^ (first ? 0x239BEB8u : 0x239B268u)) } : null });
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + Signed(task, 0x1A)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 56 ? 2 : 0);
    }

    public static (NativeBattleMorph172Frame Frame, int Result) AdvanceBody(NativeBattleMorph172Frame frame, uint address, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DA520)
        {
            throw new NotSupportedException("Morph body callback is unsupported.");
        }
        short age = Signed(task, 12);
        int actor = checked((int)frame.State.ReadDword(0x239D4F4));
        if (age < 56)
        {
            frame = inputs.Execute(frame, new NativeBattleMorph172ActorFlags(actor, 0, 4));
            (frame, uint temporary) = Allocate(frame, 220);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Morph requires its current FAR owner.");
            NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Morph requires its current FAR registers.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
            int cue = age < 8 ? (8 - age) << 9 : age >= 48 ? (age - 48) << 9 : 0;
            NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Morph requires its current arithmetic registers."), resources.Colors[age % 20], unchecked((uint)cue), far, inputs.Colors);
            frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = color.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
            NativeBattleEffectArena context = Context(frame, temporary, 220).Write(0xA8, BitConverter.GetBytes((int)Signed(task, 0x10))).Write(0xAC, BitConverter.GetBytes((int)Signed(task, 0x12))).Write(0xB0, BitConverter.GetBytes((int)Signed(task, 0x14))).Write(0xB4, BitConverter.GetBytes(1000)).Write(0xB8, BitConverter.GetBytes(color.PackedRgb));
            frame = PublishContext(frame, temporary, context, 220);
            frame = inputs.Execute(frame, new NativeBattleMorph172DrawBody220(actor, temporary) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(172, task) with { ResourceGeneration = actor } : null });
            frame = Release(frame, 220);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) >= 56)
        {
            frame = inputs.Execute(frame, new NativeBattleMorph172ActorFlags(actor, 4, 0));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleMorph172Frame SetupSprite172(NativeBattleMorph172Frame frame, NativeBattleMorph172Inputs inputs, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), xyz, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null, size, depth) };
    }
}
