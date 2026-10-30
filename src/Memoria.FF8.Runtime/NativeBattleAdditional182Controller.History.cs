using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    private static (NativeBattleAdditional182Frame Frame, int Result) AdvanceHistory182(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        int scale = Signed(task, 0x1C);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), scale, scale, scale) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        uint slot = unchecked((uint)(0x2306518 + age % 5 * 36));
        NativeBattlePackedMatrix32 localPacked = NativeBattlePackedMatrix32.FromMatrix(local, null);
        NativeBattleEffectArena localView = NativeBattleEffectArena.Clear().Write(0, localPacked.Bytes.AsSpan()).MarkUnknown(18, 2);
        frame = frame with { State = frame.State.WriteDword(slot, 1).WriteView(slot + 4, localView, 32) };
        if (inputs.CapturePresentation)
        {
            NativeBattleAdditional182Visuals.Recorded(frame, task, slot, age);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xD3AE78u)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0u));
        if (age < 30)
        {
            if (age >= 22)
            {
                context = context.Write(0x1C, BitConverter.GetBytes(0xC3u)).Write(12, BitConverter.GetBytes((age - 22) << 9));
            }
            frame = PublishContext(frame, temporary, context, 88);
            frame = inputs.Execute(frame, new NativeBattleAdditional182DrawStatic88(temporary, 0xD3AE78) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(182, task) with { ResourceGeneration = 0xD3AE78 } : null });
            context = Context(frame, temporary, 88);
        }
        context = context.Write(0x1C, BitConverter.GetBytes(NativeBattleStaticMeshContext88.Dword(context, 0x1C) | 0xC3u)).Write(12, BitConverter.GetBytes(3072));
        frame = PublishContext(frame, temporary, context, 88);
        int index = age - 1 < 0 ? 0 : (age - 1) % 5;
        int count = age < 30 ? 5 : 35 - age;
        int bias = 0;
        for (int echo = 0; echo < count; echo++)
        {
            uint at = checked(0x2306518u + (uint)(index * 36));
            if (frame.State.ReadDword(at) == 0)
            {
                break;
            }
            (byte[] bytes, bool[] known) = frame.State.ReadView(at + 4, 32).ReadSnapshot(0, 32);
            NativeBattlePackedMatrix32 history = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), history.ReadMatrix()), null));
            context = Context(frame, temporary, 88);
            context = context.Write(12, BitConverter.GetBytes(unchecked((int)NativeBattleStaticMeshContext88.Dword(context, 12) + bias)));
            frame = PublishContext(frame, temporary, context, 88);
            frame = inputs.Execute(frame, new NativeBattleAdditional182DrawStatic88(temporary, 0xD3AE78) { PilotIdentity = inputs.CapturePresentation ? NativeBattleAdditional182Visuals.History(frame, task, at) : null });
            index = index == 0 ? 4 : index - 1;
            bias += 32;
        }
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (age >= 16)
        {
            task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) - Signed(task, 0x1E))));
        }
        short spin = Signed(task, 0x1A);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + spin))).WriteWord(0x1A, unchecked((ushort)(spin - spin / 16))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 35 ? 2 : 0);
    }
}
