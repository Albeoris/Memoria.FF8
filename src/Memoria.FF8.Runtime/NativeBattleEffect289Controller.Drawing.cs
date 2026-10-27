using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect289Controller
{
    public static NativeBattleEffect289Frame DrawCustom678510(NativeBattleEffect289Frame frame, NativeBattlePackedMatrix32 matrix, uint source, int angle, int amplitudeA, int amplitudeB, int referenceW, int textureOffset, int scale, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        frame = WarpVertices6790E0(frame, source, angle, amplitudeA, amplitudeB, referenceW, scale, resources);
        (frame, uint temporary) = Allocate(frame, 108);
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + ScratchOffset))).Write(8, new byte[4]).Write(0x20, BitConverter.GetBytes((ushort)((byte)textureOffset & 127))).Write(0x24, new byte[4]).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((ushort)128)).Write(0x32, BitConverter.GetBytes((ushort)256));
        frame = PublishContext(frame, temporary, context, 108);
        frame = LoadMatrix(frame, matrix);
        frame = inputs.Execute(frame, new NativeBattleEffect289DrawCustom108(temporary, source, checked(frame.State.ReadDword(ArenaGlobal) + ScratchOffset)));
        return Release(frame, 108);
    }

    private static NativeBattleEffectArena ProgrammeCaller289(NativeBattleEffect289Frame frame, NativeBattleVector anchor, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null);
        NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 40), 0, 32);
        return caller.Write(0x20, BitConverter.GetBytes(-128)).Write(0x24, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + ScratchOffset)));
    }

    public static (NativeBattleEffect289Frame Frame, int Remaining) DrawProgramme289(NativeBattleEffect289Frame frame, uint taskAddress, int workspaceOffset, int workspaceBytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller40, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, taskAddress);
            task.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (workspaceOffset - 12), workspaceBytes);
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, taskAddress + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), workspaceBytes);
            frame = inputs.DrawProgrammeNode(frame, source, node, caller40);
            return Workspace();
        });
        frame = WriteBytes(frame, taskAddress + (uint)workspaceOffset, advanced.Workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), workspaceBytes);
        return (frame, advanced.Remaining);
    }

    private static (NativeBattleEffect289Frame Frame, int Result) AdvanceChildProgramme678490(NativeBattleEffect289Frame frame, uint address, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleEffectArena point = TaskView(Read(frame, address), 0xC, 8);
        (frame, int remaining) = DrawProgramme289(frame, address, ChildProgrammeWorkspaceOffset, ChildProgrammeWorkspaceLength, resources.ChildProgramme, ProgrammeCaller289(frame, Vector289(point), inputs), inputs);
        return (frame, remaining == 0 ? 2 : 0);
    }

    private static NativeBattleEffect289Frame TransformParticleVelocity289(NativeBattleEffect289Frame frame, uint address)
    {
        NativeBattleEffectArena velocity = TaskView(Read(frame, address), 0x1C, 8);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers289(frame), 0, Vector289(velocity), W289(velocity));
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = Publish289(frame, registers);
        NativeBattleModulePoolTask task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)registers.Colors.Ir[axis]!.Value));
        }
        return Write(frame, address, task);
    }

    public static (NativeBattleEffect289Frame Frame, int Result) AdvanceParticle6783F0(NativeBattleEffect289Frame frame, uint address, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int scale = unchecked((int)task.ReadDword(0x10));
        NativeBattleEffectArena point = TaskView(task, 0x14, 8);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector289(point), W289(point), unchecked((short)scale), unchecked((short)-(scale >> 2))) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(0xC))).Write(4, BitConverter.GetBytes(task.ReadWord(0x1A))).Write(0x24, new byte[2]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect289DrawSprite180(temporary, task.ReadDword(0xC)));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(task.ReadWord(0x14 + axis * 2) + task.ReadWord(0x1C + axis * 2))));
        }
        short age = unchecked((short)(Signed(task, 0x1A) + 1));
        task = task.WriteWord(0x1A, unchecked((ushort)age));
        return (Write(frame, address, task), age >= Signed(task, 0x22) ? 2 : 0);
    }
}
