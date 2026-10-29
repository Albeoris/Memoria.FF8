using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectricDischarge126Controller
{
    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceAct(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        NativeBattleElectricDischarge126AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
        frame = anchor.Frame;
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(anchor.Anchor.X)).Write(2, BitConverter.GetBytes(unchecked((short)(anchor.Anchor.Y + 200)))).Write(4, BitConverter.GetBytes(anchor.Anchor.Z)).Write(6, BitConverter.GetBytes(anchor.Anchor.W)).Write(8, BitConverter.GetBytes(Working(frame) + MorphOffset));
        NativeBattleElectricDischarge126TaskStep advanced = inputs.AdvanceAct(frame, address, context);
        return (advanced.Frame, advanced.Result);
    }

    public static (NativeBattleElectricDischarge126Frame Frame, int Result) AdvanceSprite(NativeBattleElectricDischarge126Frame frame, uint address, NativeBattleElectricDischarge126Resources resources, NativeBattleElectricDischarge126Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        byte actor = task.ReadByte(15);
        if (actor != 255 && age <= 4)
        {
            if (actor >= 3)
            {
                throw new NotSupportedException("Electric Discharge sprite color owner leaves its native saved three-party rows.");
            }
            frame = inputs.Execute(frame, new NativeBattleElectricDischarge126ActorFlags(actor, 0, 0x800));
            short ratio = unchecked((short)NativeBattleCameraMath.Sine(age << 8));
            frame = inputs.Execute(frame, new NativeBattleElectricDischarge126BlendActorColor(actor, frame.State.ReadDword(SavedColorsGlobal + (uint)(actor * 4)), resources.ActorColor, unchecked((short)(4096 - ratio)), ratio));
        }
        short scale = Signed(task, 0x16);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(task, 0x10), scale, scale, unchecked((short)-(scale >> 2))) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Electric Discharge requires its physical temporary memory."), 180);
        uint source = task.ReadByte(14) == 0 ? resources.SpriteA.Address : resources.SpriteB.Address;
        ushort phase = task.ReadWord(12);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, 0).Write(allocation.Address + 4, BitConverter.GetBytes(phase));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleElectricDischarge126DrawSprite180(allocation.Address, source, phase) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(126, Read(frame, address)) with { ResourceGeneration = source } });
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Electric Discharge lost its physical sprite temporary."), 180) };
        ushort next = unchecked((ushort)(Read(frame, address).ReadWord(12) + 1));
        return (Write(frame, address, Read(frame, address).WriteWord(12, next)), unchecked((short)next) < 8 ? 0 : 2);
    }
}
