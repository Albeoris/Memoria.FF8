using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    private static (NativeBattleElectrocute275Frame Frame, int Result) Act(NativeBattleElectrocute275Frame frame, uint address, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        if (Read(frame, address).Callback != ActCallback)
        {
            throw new NotSupportedException("Electrocute reached an unsupported ACT callback.");
        }
        NativeBattleElectrocute275TaskStep advanced = inputs.AdvanceAct(frame, address);
        return (advanced.Frame, advanced.Result);
    }

    private static (NativeBattleElectrocute275Frame Frame, int Result) Sprite(NativeBattleElectrocute275Frame frame, uint address, NativeBattleElectrocute275Resources resources, NativeBattleElectrocute275Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x688C60)
        {
            throw new NotSupportedException("Electrocute reached an unsupported sprite callback.");
        }
        int age = Signed(task, 12);
        byte actor = task.ReadByte(15);
        if (actor != 255 && age <= 4)
        {
            if (actor >= 7)
            {
                throw new InvalidDataException("Electrocute sprite actor leaves the original model bank.");
            }
            frame = inputs.Execute(frame, new NativeBattleElectrocute275ActorFlags(actor, 0, 0x800));
            short ratio = unchecked((short)NativeBattleCameraMath.Sine(age << 8));
            frame = inputs.Execute(frame, new NativeBattleElectrocute275BlendActorColor(actor, frame.State.ReadDword(SavedColorsGlobal + (uint)actor * 4), resources.ActorColor, unchecked((short)(4096 - ratio)), ratio));
        }
        short scale = Signed(task, 0x16);
        NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, scale, scale, unchecked((short)-(scale >> 2))) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Electrocute requires its actual temporary memory."), 180);
        uint source = task.ReadByte(14) == 0 ? resources.SpriteA.Address : resources.SpriteB.Address;
        ushort phase = task.ReadWord(12);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, 0).Write(allocation.Address + 4, BitConverter.GetBytes(phase));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleElectrocute275DrawSprite180(allocation.Address, source, phase));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Electrocute lost its physical sprite temporary."), 180) };
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        return (Write(frame, address, Read(frame, address).WriteWord(12, next)), unchecked((short)next) < 8 ? 0 : 2);
    }
}
