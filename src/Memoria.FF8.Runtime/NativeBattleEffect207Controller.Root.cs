using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Controller
{
    public static NativeBattleEffect207Step AdvanceRoot6A3B70(NativeBattleEffect207Frame frame, uint address, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (root.ReadDword(16) == 0 ? 0x11A2Cu : 0x1A2Cu)) };
        frame = Write(frame, address, root.WriteDword(16, root.ReadDword(16) == 0 ? 1u : 0u));
        if (root.ReadWord(12) == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect207Clear45BEE0(576, 256, 320, 256, 0, 0, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect207SetOrigin45B5E0(576, 256));
            if (Read(frame, address).ReadByte(15) == 0)
            {
                frame = NativeBattleEffect207Lower.Preprocess6070(frame, MutableMeshAddress, resources, inputs);
                frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
                frame = InitializeCommonBanks(frame);
                (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
                if (main == 0)
                {
                    throw new InvalidDataException("Effect207 required main allocation failed.");
                }
                frame = ClearDwords701200(frame, main + 12, 0x137);
                frame = NativeBattleEffect207Programme.Initialize(frame, main + ProgrammeWorkspaceOffset, ProgrammeAddress, ProgrammeWorkspaceLength, resources);
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect207 requires its current header.");
                NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
                int count = header.ReadByte(16);
                frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count).WriteWord(40, 0).WriteWord(42, 0).WriteWord(44, 0));
                NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
                for (int index = 0; index < count; index++)
                {
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect207 requires its actual contact pointer.");
                    int actor = packets.ReadContact(first + index).Primary.ActorIndex;
                    uint pointer = unchecked(0x1D972C0u + (uint)(actor * 156));
                    frame = WriteBytes(frame, main + 20u + (uint)(index * 4), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(pointer)), 4);
                    NativeBattleEffect207PointStep point = inputs.Anchor571400(frame, actor, retained);
                    frame = point.Frame;
                    retained = point.Output8;
                    NativeBattleVector vector = Vector(retained);
                    short minimumY = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, actor).Read(0x36, 2));
                    NativeBattleModulePoolTask current = Read(frame, main);
                    frame = Write(frame, main, current.WriteWord(40, unchecked((ushort)(current.ReadWord(40) + vector.X))).WriteWord(42, unchecked((ushort)(current.ReadWord(42) + minimumY - 512))).WriteWord(44, unchecked((ushort)(current.ReadWord(44) + vector.Z))));
                    retained = retained.Write(2, BitConverter.GetBytes(unchecked((short)(minimumY - 512))));
                }
                NativeBattleModulePoolTask averaged = Read(frame, main);
                frame = Write(frame, main, averaged.WriteWord(40, unchecked((ushort)(Signed(averaged, 40) / count))).WriteWord(42, unchecked((ushort)(Signed(averaged, 42) / count))).WriteWord(44, unchecked((ushort)(Signed(averaged, 44) / count))));
            }
        }
        if (Read(frame, address).ReadByte(15) != 0)
        {
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(inputs.ReadView(frame).ReadMatrix());
            NativeBattleEffectArena target = ReadBytes(frame, ViewAddress, 32);
            NativeBattleEffectArena matrix = CopyBytes(target, 18, PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null)), 18, 2);
            frame = WriteBytes(frame, ViewAddress, matrix, 32);
            int mainIndex = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == MainDescriptor);
            (frame, int survivors) = AdvancePool(frame, mainIndex, resources, inputs);
            int particles = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == ParticleDescriptor);
            (frame, _) = AdvancePool(frame, particles, resources, inputs);
            if (survivors == 0)
            {
                return new(frame, 2);
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1))));
        return new(frame, 0);
    }
}
