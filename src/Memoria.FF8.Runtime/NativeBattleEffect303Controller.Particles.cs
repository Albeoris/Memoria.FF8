using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static (NativeBattleEffect303Frame Frame, int Result) AdvanceSprite66C080(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs) => AdvanceSprite303(frame, address, resources.Sprite.Address, 12, false, inputs);

    public static (NativeBattleEffect303Frame Frame, int Result) AdvanceSprite66C100(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs) => AdvanceSprite303(frame, address, resources.ParticleSprite.Address, 10, true, inputs);

    private static (NativeBattleEffect303Frame Frame, int Result) AdvanceSprite303(NativeBattleEffect303Frame frame, uint address, uint source, int phases, bool moving, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 3072, -768) };
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(task.ReadWord(18))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect303DrawSprite180(temporary, source));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        task = task.WriteWord(18, unchecked((ushort)phase));
        if (phase >= phases)
        {
            return (Write(frame, address, task), 2);
        }
        if (moving)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                ushort velocity = task.ReadWord(20 + axis * 2);
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity))).WriteWord(20 + axis * 2, unchecked((ushort)(velocity + task.ReadWord(28 + axis * 2))));
            }
        }
        return (Write(frame, address, task), 0);
    }

    public static NativeBattleEffect303Frame SpawnProgramme66C1C0(NativeBattleEffect303Frame frame, NativeBattleEffectArena velocity8, uint mesh, int scale, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x2507600, 0x66C220);
        if (address == 0)
        {
            throw new NotSupportedException("Effect303 child programme requires its native successful allocation.");
        }
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[280].ToImmutableArray()).Initialize7016B0(resources.ChildProgramme, 280);
        frame = WriteBytes(frame, address + 32, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), 280);
        uint caster = frame.State.ReadDword(0x25075D8);
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, caster);
        var transform = model.Transform ?? throw new NotSupportedException("Effect303 child requires its current caster transform.");
        NativeBattleEffectArena position = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)transform.Position28.X))).Write(2, BitConverter.GetBytes(unchecked((short)transform.Position28.Y))).Write(4, BitConverter.GetBytes(unchecked((short)transform.Position28.Z))).Write(6, BitConverter.GetBytes(transform.Vertical34));
        frame = WriteBytes(frame, address + 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh)), 4);
        frame = WriteBytes(frame, address + 16, position, 8);
        frame = Write(frame, address, Read(frame, address).WriteWord(22, unchecked((ushort)scale)));
        return WriteBytes(frame, address + 24, velocity8, 8);
    }

    public static (NativeBattleEffect303Frame Frame, int Result) AdvanceProgramme66C220(NativeBattleEffect303Frame frame, uint address, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(30) != 0)
        {
            uint caster = frame.State.ReadDword(0x25075D8);
            int angle = unchecked((short)-(inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Effect303 child requires its current caster rotation WORD14.")));
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(angle) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), 0));
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(task.ReadDword(12))).Write(28, new byte[4]);
            frame = PublishContext(frame, temporary, context, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect303DrawStaticMesh88(temporary, task.ReadDword(12)));
            frame = Release(frame, 88);
            task = Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(16 + axis * 2, unchecked((ushort)(task.ReadWord(16 + axis * 2) + task.ReadWord(24 + axis * 2))));
            }
            task = task.WriteWord(26, unchecked((ushort)(task.ReadWord(26) + 10))).WriteWord(30, unchecked((ushort)(task.ReadWord(30) - 1)));
            frame = Write(frame, address, task);
            if (Signed(task, 30) > 0)
            {
                return (frame, 0);
            }
            local = NativeBattlePandemona291Restoration.Rotation701270(angle) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, 0));
            (frame, temporary) = Allocate(frame, 88);
            context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(task.ReadDword(12))).Write(4, BitConverter.GetBytes(frame.State.ReadDword(0x2507658))).Write(8, BitConverter.GetBytes(2));
            (frame, context) = BuildFragments66C3F0(frame, context, resources, inputs);
            frame = PublishContext(frame, temporary, context, 88);
            frame = Release(frame, 88);
            frame = WriteBytes(frame, address + 16, NativeBattleEffectNodeContextMemory.Copy(context, 52, NativeBattleEffectArena.Clear(), 0, 6), 6);
        }
        task = Read(frame, address);
        NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 16, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 8);
        int value = Signed(task, 22);
        caller = caller.Write(8, BitConverter.GetBytes(value)).Write(12, BitConverter.GetBytes(value)).Write(16, BitConverter.GetBytes(value)).Write(20, BitConverter.GetBytes(1)).Write(24, new byte[4]).Write(28, BitConverter.GetBytes(frame.State.ReadDword(0x2507658) + 0x3688));
        (frame, int remaining) = DrawProgramme303(frame, address, 32, 280, resources.ChildProgramme, caller, resources, inputs);
        return (frame, remaining == 0 ? 2 : 0);
    }
}
