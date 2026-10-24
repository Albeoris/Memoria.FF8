using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    public static NativeBattleEffect210Step AdvanceRoot6F6890(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool alternate = root.ReadByte(15) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (alternate ? 0xB1B8u : 0x1B1B8u)).WriteDword(SecondCursorGlobal, arena + (alternate ? 0x1B1B8u : 0x2B1B8u)).WriteDword(BrightnessGlobal, 0) };
        frame = Write(frame, address, root.WriteByte(15, alternate ? (byte)0 : (byte)1));
        if (root.ReadWord(12) == 1 && frame.State.ReadDword(FrozenGlobal) == 0 && root.ReadByte(14) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(14, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect210IoInitialize534110(arena + 0xA708));
            frame = InitializeCommonPool(frame, MainDescriptor);
            frame = InitializeCommonPool(frame, EarlyDescriptor);
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect210 requires its native main task allocation.");
            }
            frame = ClearDwords701200(frame, main + 12, 0xED);
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect210 requires its current header.");
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
            int count = header.ReadByte(16);
            frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
            int[] minimum = [32767, 32767, 32767];
            int[] maximum = [-32767, -32767, -32767];
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int index = 0; index < count; index++)
            {
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect210 requires its actual current contact pointer.");
                int actor = packets.ReadContact(first + index).Primary.ActorIndex;
                uint model = unchecked(0x1D972C0u + (uint)(actor * 156));
                uint record = main + 16u + (uint)(index * 24);
                frame = WriteBytes(frame, record, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(model)), 4);
                frame = WriteBytes(frame, record + 4, CopyBytes(inputs.ReadModelArena9C(frame, actor), 28, NativeBattleEffectArena.Clear(), 0, 8), 8);
                frame = WriteBytes(frame, record + 12, frame.State.ReadView(RetainedPairAddress, 8), 8);
                NativeBattleEffect210PointStep point = inputs.Anchor571400(frame, actor, retained);
                frame = point.Frame;
                retained = point.Output8;
                NativeBattleVector vector = Vector(retained);
                int[] coordinates = [vector.X, vector.Y, vector.Z];
                for (int axis = 0; axis < 3; axis++)
                {
                    minimum[axis] = Math.Min(minimum[axis], coordinates[axis]);
                    maximum[axis] = Math.Max(maximum[axis], coordinates[axis]);
                }
                frame = Birth6F6BB0(frame, model);
            }
            for (int axis = 0; axis < 3; axis++)
            {
                ushort value = unchecked((ushort)((minimum[axis] + maximum[axis]) / 2));
                frame = frame with { State = frame.State.WriteWord(CenterAddress + (uint)(axis * 2), value) };
                frame = Write(frame, main, Read(frame, main).WriteWord(0x58 + axis * 2, value));
            }
            frame = frame with { State = frame.State.WriteView(SavedPositionAddress, CopyBytes(inputs.ReadModelArena9C(frame, Actor(frame.State.ReadDword(CasterGlobal))), 28, NativeBattleEffectArena.Clear(), 0, 8), 8) };
        }
        int mainSurvivors = 1;
        int finalSurvivors = 1;
        if (Read(frame, address).ReadByte(14) != 0)
        {
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(inputs.ReadView(frame).ReadMatrix());
            NativeBattleEffectArena matrix = PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null));
            matrix = CopyBytes(frame.State.ReadView(ViewAddress, 32), 18, matrix, 18, 2);
            frame = frame with { State = frame.State.WriteView(ViewAddress, matrix, 32) };
            (frame, mainSurvivors) = AdvancePool(frame, MainDescriptor, resources, inputs);
            foreach (uint descriptor in new uint[] { EarlyDescriptor, SpriteDescriptor, MeshDescriptor, EchoDescriptor })
            {
                (frame, _) = AdvancePool(frame, descriptor, resources, inputs);
            }
            (frame, finalSurvivors) = AdvancePool(frame, ActDescriptor, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect210Stage5713E0(unchecked((int)frame.State.ReadDword(BrightnessGlobal)), 0));
        if (frame.State.ReadDword(FrozenGlobal) == 0)
        {
            if (Read(frame, address).ReadByte(14) != 0 && mainSurvivors == 0 && finalSurvivors == 0)
            {
                return new(inputs.Execute(frame, new NativeBattleEffect210Stage5713E0(0, 0)), 2);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect210Frame Birth6F6BB0(NativeBattleEffect210Frame frame, uint model)
    {
        (frame, uint address) = Append(frame, EarlyDescriptor, 0x6F6C00);
        if (address != 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteWord(32, unchecked((ushort)Actor(model))).WriteWord(34, 0));
        }
        return frame;
    }
}
