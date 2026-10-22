using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleReflectBeam135Controller
{
    public static (NativeBattleReflectBeam135Frame Frame, int Result) AdvanceMain(NativeBattleReflectBeam135Frame frame, uint address, NativeBattleReflectBeam135Resources resources, NativeBattleReflectBeam135Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age >= 15)
        {
            int depth = age < 19 ? (age - 15) * 2200 / 4 : age >= 45 ? (57 - age) * 2200 / 12 : 2200;
            frame = inputs.Execute(frame, new NativeBattleReflectBeam135Stage(unchecked((short)depth), 0));
        }
        int phase = age - 15;
        if ((uint)phase < 20)
        {
            NativeBattleReflectBeam135PointStep bone = inputs.ReadBone(frame, Actor(frame.State.ReadDword(CasterGlobal)), 31, 4096);
            frame = bone.Frame;
            NativeBattleVector mouth = bone.Position;
            (frame, uint temporaryAddress) = Allocate(frame, 88);
            frame = frame with { Staging = RequireStaging(frame).Write(temporaryAddress + 8, new byte[3]) };
            for (int pass = 2; pass >= 0; pass--)
            {
                int elapsed = phase - pass;
                if ((uint)elapsed >= 10)
                {
                    continue;
                }
                int factor = elapsed * 4096 / 10;
                NativeBattleModulePoolTask task = Read(frame, address);
                short endpointX = unchecked((short)(Signed(task, 0x20) - (factor * 5000 >> 12) + 2500));
                short endpointY = Signed(task, 0x22);
                short endpointZ = Signed(task, 0x24);
                NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 88);
                if (pass == 0)
                {
                    temporary = temporary.Write(0x1C, BitConverter.GetBytes(0x30u));
                    int selected = task.ReadByte(0x13);
                    if (selected < Signed(task, 14))
                    {
                        uint modelAddress = task.ReadDword(0x14 + selected * 4);
                        int actor = Actor(modelAddress);
                        if (endpointX < Transform(inputs.ReadModel(actor)).Position28.X)
                        {
                            (frame, uint targetAddress) = Append(frame, 3, TargetCallback);
                            if (targetAddress != 0)
                            {
                                NativeBattleModulePoolTask target = Read(frame, targetAddress).WriteWord(12, 0).WriteWord(14, task.ReadByte(0x10 + selected)).WriteDword(16, modelAddress);
                                frame = Write(frame, targetAddress, target);
                                NativeBattleReflectBeam135PointStep anchor = inputs.ReadBone(frame, actor, 0xF1, 0);
                                frame = anchor.Frame;
                                target = Vector(Read(frame, targetAddress), 0x14, anchor.Position);
                                target = target.WriteWord(0x16, unchecked((ushort)(target.ReadWord(0x16) + 128)));
                                target = InitializeWorkspace(target, 0x1C, 0x310, resources.TargetAct);
                                frame = Write(frame, targetAddress, target);
                            }
                            frame = Write(frame, address, Read(frame, address).WriteByte(0x13, unchecked((byte)(selected + 1))));
                        }
                    }
                }
                else
                {
                    temporary = temporary.Write(0x1C, BitConverter.GetBytes(0xF0u)).Write(0x0C, BitConverter.GetBytes((pass + 4) << 9));
                }
                (NativeBattleActionMatrix basis, int length) = NativeBattleFunguarLaser277.Basis50CBA0(endpointX - mouth.X, mouth.Y - endpointY, mouth.Z - endpointZ);
                basis = basis with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
                int sine = NativeBattleCameraMath.Sine((factor >> 2) + 512);
                short width = unchecked((short)((sine + 2048) >> 1));
                short scaleLength = unchecked((short)(unchecked(sine * length) >> 11));
                NativeBattleReflectBeam135State state = frame.State.WriteWord(ScaleMatrixGlobal, unchecked((ushort)width)).WriteWord(ScaleMatrixGlobal + 8, unchecked((ushort)width)).WriteWord(ScaleMatrixGlobal + 16, unchecked((ushort)scaleLength));
                frame = frame with { State = state };
                NativeBattleActionMatrix right = new(Enumerable.Range(0, 9).Select(index => unchecked((short)state.ReadWord(ScaleMatrixGlobal + (uint)(index * 2)))).ToImmutableArray(), 0, 0, 0, 4096);
                NativeBattleActionMatrix scaled = basis with { Rotation = NativeBattleGfMatrices.Multiply56C090(basis, right).Rotation };
                NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), scaled);
                frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(combined, null));
                temporary = temporary.Write(0, BitConverter.GetBytes(StaticMeshAddress));
                frame = PublishContext(frame, temporaryAddress, temporary, 88);
                frame = inputs.Execute(frame, new NativeBattleReflectBeam135DrawStatic88(temporaryAddress, StaticMeshAddress));
            }
            frame = Release(frame, 88);
            NativeBattleReflectBeam135ActStep advanced = inputs.AdvanceAct(frame, address, resources.MainAct, 0x28, 0x2B8, ActContext(frame, PointView(mouth), -128));
            frame = advanced.Frame;
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleReflectBeam135Sound(resources.Sound, 0, 128));
        }
        if ((uint)age < 20 && (age & 3) == 0)
        {
            (frame, uint secondaryAddress) = Append(frame, 2, SecondaryCallback);
            if (secondaryAddress != 0)
            {
                NativeBattleModulePoolTask secondary = Read(frame, secondaryAddress).WriteWord(12, 0).WriteWord(14, 0).WriteDword(16, BoneSelectorsAddress + (uint)(((age >> 2) % 3) * 2));
                secondary = InitializeWorkspace(secondary, 0x1C, 0x1F8, resources.SecondaryAct);
                secondary = InitializeWorkspace(secondary, 0x214, 0x58, resources.ImpactAct);
                frame = Write(frame, secondaryAddress, secondary);
            }
        }
        if (age >= 57)
        {
            frame = inputs.Execute(frame, new NativeBattleReflectBeam135Stage(0, 0));
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
