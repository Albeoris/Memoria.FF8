using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Controller
{
    public static NativeBattleEffect320Step AdvanceRoot65BCC0(NativeBattleEffect320Frame frame, uint address, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool alternate = root.ReadDword(16) == 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(arena + (uint)(alternate ? PacketBank1Offset : PacketBank0Offset))) };
        frame = Write(frame, address, root.WriteDword(16, alternate ? 1u : 0u));
        if (root.ReadWord(12) == 1)
        {
            if (Read(frame, address).ReadByte(14) == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteByte(14, 1));
                frame = InitializeCommonBanks(frame);
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect320 required main allocation failed.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x1D2);
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect320 requires its current action parameters.");
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
            int count = header.ReadByte(16);
            if (count == 0)
            {
                throw new NotSupportedException("Effect320 native target centroid has an unsupported zero divisor.");
            }
            frame = Write(frame, main, Read(frame, main).WriteWord(56, 0).WriteWord(60, 0));
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int index = 0; index < count; index++)
            {
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect320 requires its actual contact pointer.");
                int actor = packets.ReadContact(first + index).Primary.ActorIndex;
                NativeBattleEffect320PointStep point = inputs.Anchor571400(frame, actor, retained);
                frame = point.Frame;
                retained = point.Output8;
                NativeBattleVector vector = Vector(retained);
                NativeBattleModulePoolTask current = Read(frame, main);
                frame = Write(frame, main, current.WriteWord(56, unchecked((ushort)(current.ReadWord(56) + vector.X))).WriteWord(60, unchecked((ushort)(current.ReadWord(60) + vector.Z))));
            }
            NativeBattleModulePoolTask averaged = Read(frame, main);
            frame = Write(frame, main, averaged.WriteWord(56, unchecked((ushort)(Signed(averaged, 56) / count))).WriteWord(58, 0).WriteWord(60, unchecked((ushort)(Signed(averaged, 60) / count))));
            frame = NativeBattleEffect320Programme.Initialize(frame, main + 0x40, 0xFE761C, 0x1F0, resources);
            frame = NativeBattleEffect320Programme.Initialize(frame, main + 0x230, 0xFE9294, 0x110, resources);
            frame = NativeBattleEffect320Programme.Initialize(frame, main + 0x340, 0xFE9BCC, 0x414, resources);
        }
        if (Read(frame, address).ReadByte(14) != 0)
        {
            int mainIndex = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == MainDescriptor);
            (frame, int mainSurvivors) = AdvancePool(frame, mainIndex, resources, inputs);
            int particleIndex = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == ParticleDescriptor);
            (frame, int particleSurvivors) = AdvancePool(frame, particleIndex, resources, inputs);
            if (mainSurvivors == 0 && particleSurvivors == 0)
            {
                return new(frame, 2);
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, 0);
    }
}
