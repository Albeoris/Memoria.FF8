using System.Buffers.Binary;
using System.Collections.Immutable;
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
                NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
                int count = header.ReadContactCount();
                frame = Write(frame, main, Read(frame, main).WriteContactCount(14, count).WriteWord(40, 0).WriteWord(42, 0).WriteWord(44, 0));
                NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
                var recipients = ImmutableArray.CreateBuilder<(BattleModelOwner Owner, BattleContactOccurrence Occurrence)>(count);
                ImmutableArray<byte>? originalFirst = [0, 0, 0, 0];
                for (int index = 0; index < count; index++)
                {
                    BattleContactCursor first = header.FirstContact;
                    NativeBattleContactPacket contact = packets.ReadContact(first.Advance(index));
                    BattleModelOwner actor = contact.Primary.Recipient;
                    BattleContactOccurrence occurrence = contact.Occurrence ?? throw new InvalidDataException("Effect207 requires its captured contact occurrence.");
                    recipients.Add((actor, occurrence));
                    if (index < 5)
                    {
                        ImmutableArray<byte>? encoding = packets.ModelAliases.EncodeAddress(actor);
                        if (index == 0)
                        {
                            originalFirst = encoding;
                        }
                        int offset = 20 + index * 4;
                        NativeBattleModulePoolTask task = Read(frame, main);
                        task = encoding is { } encoded ? task.WriteBytes(offset, encoded.AsSpan()) : task.MarkPayloadUnknown(offset, 4);
                        frame = Write(frame, main, task);
                    }
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
                averaged.WriteExecutionReference(20, 4, new BattleExecutionReference.OrderedContactRecipients(recipients.MoveToImmutable()), originalFirst);
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
