using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    public static NativeBattleEffect140Step AdvanceRoot6A6530(NativeBattleEffect140Frame frame, uint address, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint bank = frame.State.ReadDword(0x2517B28);
        uint cursor = bank + (task.ReadDword(0x10) != 0 ? 0x3758u : 0x11758u);
        task = task.WriteDword(0x10, task.ReadDword(0x10) == 0 ? 1u : 0u);
        frame = Write(frame, address, task);
        frame = frame with { State = frame.State.WriteDword(0x2517B0C, cursor).WriteDword(0x2517B20, 0) };
        if (task.ReadWord(0xC) == 2 && frame.State.ReadDword(0x2517AAC) == 0 && task.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, task.WriteByte(0xF, 1));
            frame = InitializePool(frame, 0x2517AD8, bank, 0x878, 1);
            (frame, uint main) = Append(frame, 0x2517AD8, 0x6A6750);
            if (main == 0)
            {
                throw new InvalidDataException("Effect140's required main allocation failed.");
            }
            frame = ClearDwords701200(frame, main + 0xC, 0x21B);
            frame = inputs.Execute(frame, new NativeBattleEffect140Vibration4A29A0(0x11D0048, 1, 128, main + 0x10));
            NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().Write(0, new byte[32]);
            foreach (int diagonal in new[] { 0, 8, 16 })
            {
                matrix = matrix.Write(diagonal, BitConverter.GetBytes((short)8192));
            }
            matrix = matrix.Write(28, BitConverter.GetBytes(frame.State.ReadDword(0x2517B18)));
            frame = WriteBytes(frame, main + 0xA4, matrix, 32);
            frame = NativeBattleEffect140OwnedModel.Initialize6EC060(frame, main + 0x64, main + 0x100, resources);
            frame = NativeBattleEffect140OwnedModel.Select6574D0(frame, main + 0x64, 0, resources);
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect140 requires its current recipient header.");
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
            int count = header.ReadByte(0x10);
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect140 requires its current recipient pointer.");
            frame = Write(frame, main, Read(frame, main).WriteWord(0xE, (ushort)count));
            for (int index = 0; index < count; index++)
            {
                int actor = packets.ReadContact(first + index).Primary.ActorIndex;
                frame = WriteBytes(frame, main + 0x14u + (uint)(4 * index), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(ModelAddress(actor))), 4);
                uint color = inputs.ReadModel(frame, actor).Color40 ?? throw new NotSupportedException("Effect140 requires the current recipient color.");
                frame = WriteBytes(frame, main + 0x3Cu + (uint)(4 * index), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(color)), 4);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect140InitializeIo534110(bank + 0x1F758));
        }
        int mainSurvivors = unchecked((int)address);
        int particleSurvivors = unchecked((int)address);
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(view.ReadMatrix());
            NativeBattleEffectArena existing = ReadBytes(frame, 0x2517B30, 32);
            NativeBattleEffectArena source = PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null));
            source = CopyBytes(existing, 18, source, 18, 2);
            frame = WriteBytes(frame, 0x2517B30, source, 32);
            (frame, mainSurvivors) = AdvancePool(frame, 0x2517AD8, resources, inputs);
            if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == 0x2517AC8))
            {
                (frame, particleSurvivors) = AdvancePool(frame, 0x2517AC8, resources, inputs);
            }
            else
            {
                particleSurvivors = 0;
            }
            if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == 0x2517AB8))
            {
                (frame, _) = AdvancePool(frame, 0x2517AB8, resources, inputs);
            }
        }
        frame = inputs.Execute(frame, new NativeBattleEffect140Stage5713E0(unchecked((int)frame.State.ReadDword(0x2517B20)), 0));
        if (frame.State.ReadDword(0x2517AAC) == 0)
        {
            if (Read(frame, address).ReadByte(0xF) != 0 && mainSurvivors == 0 && particleSurvivors == 0)
            {
                return new(frame, frame.State.Read(0x2517B24, 1)[0] != 0 ? 2 : 0);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(Read(frame, address).ReadWord(0xC) + 1))));
        }
        return new(frame, 0);
    }
}
