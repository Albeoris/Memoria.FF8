using static Memoria.FF8.Runtime.NativeBattleOilBlast137Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilBlast137Controller
{
    public static (NativeBattleOilBlast137Frame Frame, int Result) AdvanceParticle(NativeBattleOilBlast137Frame frame, uint address, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = Word(task, 0x1A);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 12), OptionalWord(task, 18), scale, unchecked((short)-(scale >> 2))) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(frame.Staging!, temporary, SpriteAddress, 0).Write(temporary + 4, BitConverter.GetBytes(task.ReadWord(18)));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleOilBlast137DrawSprite180(temporary, SpriteAddress) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(137, Read(frame, address)) with { ResourceGeneration = SpriteAddress } : null });
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int index = 0; index < 3; index++)
        {
            int velocityOffset = 0x14 + index * 2;
            short velocity = unchecked((short)(Word(task, velocityOffset) + Word(task, 0x1C + index * 2)));
            task = task.WriteWord(velocityOffset, unchecked((ushort)velocity));
            task = task.WriteWord(12 + index * 2, unchecked((ushort)(Word(task, 12 + index * 2) + velocity)));
        }
        short next = unchecked((short)(Word(task, 18) + 1));
        task = task.WriteWord(18, unchecked((ushort)next));
        return (Write(frame, address, task), next < 12 ? 0 : 2);
    }

    public static (NativeBattleOilBlast137Frame Frame, int Result) AdvanceSecondary(NativeBattleOilBlast137Frame frame, uint address, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(Vector(task, 0x1C));
        int age = Word(task, 18);
        if (age > 26)
        {
            int scale = (30 - age) << 10;
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        }
        NativeBattleVector point = Vector(task, 12);
        local = local with { X = point.X, Y = point.Y, Z = point.Z };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleFileStaging staging = frame.Staging!.Write(temporary, BitConverter.GetBytes(StaticMeshAddress)).Write(temporary + 0x1C, BitConverter.GetBytes(0u));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleOilBlast137DrawStatic88(temporary, StaticMeshAddress) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(137, Read(frame, address)) with { ResourceGeneration = StaticMeshAddress } : null });
        frame = Release(frame, 88);
        task = Read(frame, address);
        short vertical = unchecked((short)(Word(task, 0x16) + 16));
        task = task.WriteWord(0x16, unchecked((ushort)vertical));
        task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) + vertical))).WriteWord(12, unchecked((ushort)(Word(task, 12) + Word(task, 0x14))));
        if (Word(task, 14) >= 0)
        {
            task = task.WriteWord(0x16, unchecked((ushort)-vertical));
        }
        task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + Word(task, 0x18))));
        for (int index = 0; index < 3; index++)
        {
            task = task.WriteWord(0x1C + index * 2, unchecked((ushort)(Word(task, 0x1C + index * 2) + Word(task, 0x24 + index * 2))));
        }
        short next = unchecked((short)(Word(task, 18) + 1));
        task = task.WriteWord(18, unchecked((ushort)next));
        return (Write(frame, address, task), next < 30 ? 0 : 2);
    }
}
