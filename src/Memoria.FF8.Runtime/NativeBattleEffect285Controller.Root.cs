using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect285Controller
{
    public static NativeBattleEffect285Step AdvanceRoot67A900(NativeBattleEffect285Frame frame, uint address, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool side = root.ReadDword(16) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (uint)(side ? FirstPacketOffset : SecondPacketOffset)).WriteDword(IntensityGlobal, 0) };
        frame = Write(frame, address, root.WriteDword(16, side ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = CaptureModelFlags67ABD0(frame, inputs);
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = CreatePool(frame, shape, true);
                frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
            }
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect285 requires its mandatory main task allocation.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x56B);
            frame = CaptureTargetCentroid(frame, inputs);
            frame = NativeBattleEffect285Programme.Initialize(frame, main + FirstWorkspaceOffset, FirstProgrammeAddress, FirstWorkspaceLength, resources);
            frame = NativeBattleEffect285Programme.Initialize(frame, main + SecondWorkspaceOffset, SecondProgrammeAddress, SecondWorkspaceLength, resources);
            frame = inputs.Execute(frame, new NativeBattleEffect285IoInitialize534110(arena + IoOffset));
        }
        bool initialized = Read(frame, address).ReadByte(15) != 0;
        int mainSurvivors = 0;
        if (initialized)
        {
            frame = NormalizeView67AAE0(frame, inputs.ReadView(frame));
            (frame, mainSurvivors) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect285Stage5713E0(unchecked((int)frame.State.ReadDword(IntensityGlobal)), 0));
        if (initialized && mainSurvivors == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect285IoAdvance534210());
            frame = inputs.Execute(frame, new NativeBattleEffect285Stage5713E0(0, 0));
            NativeBattleEffect285IoCompletionStep complete = inputs.Select5342C0(frame, 0);
            return new(complete.Frame, complete.Complete ? 2 : 0);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleEffect285Frame CaptureModelFlags67ABD0(NativeBattleEffect285Frame frame, NativeBattleEffect285Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(inputs.ReadModelArena9C(frame, ModelAddress(actor)).Read(0, 2));
            frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)(actor * 4), flags) };
        }
        return frame;
    }

    public static NativeBattleEffect285Frame CaptureTargetCentroid(NativeBattleEffect285Frame frame, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = CurrentHeader(frame, packets);
        int count = header.ReadByte(16);
        if (count == 0)
        {
            throw new NotSupportedException("Effect285 requires a nonempty native contact group for its signed centroid division.");
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect285 requires its actual contact pointer.");
        short sum = 0;
        frame = frame with { State = frame.State.WriteWord(CentroidGlobal, 0) };
        for (int index = 0; index < count; index++)
        {
            NativeBattleContactPacket contact = packets.ReadContact(first + index);
            short z = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, ModelAddress(contact.Primary.ActorIndex)).Read(32, 2));
            sum = unchecked((short)(sum + z));
            frame = frame with { State = frame.State.WriteWord(CentroidGlobal, unchecked((ushort)sum)) };
        }
        return frame with { State = frame.State.WriteWord(CentroidGlobal, unchecked((ushort)(sum / count))) };
    }

    public static NativeBattleEffect285Frame NormalizeView67AAE0(NativeBattleEffect285Frame frame, NativeBattlePackedMatrix32 view)
    {
        NativeBattleEffectArena source = PackedArena(view);
        int x = BinaryPrimitives.ReadInt16LittleEndian(source.Read(2, 2));
        int y = BinaryPrimitives.ReadInt16LittleEndian(source.Read(8, 2));
        int z = BinaryPrimitives.ReadInt16LittleEndian(source.Read(14, 2));
        var columnY = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        var columnX = NativeBattleActionGeometryRules.NormalizeVector56BC50(columnY.Y, -columnY.X, 0);
        var cross = NativeBattleRayBomb10Geometry.Cross56BBF0(columnX, columnY);
        var columnZ = NativeBattleActionGeometryRules.NormalizeVector56BC50(cross.X, cross.Y, cross.Z);
        NativeBattleEffectArena result = frame.State.ReadView(ViewAddress, 32);
        for (int row = 0; row < 3; row++)
        {
            int[] values = row switch
            {
                0 => [columnX.X, columnY.X, columnZ.X],
                1 => [columnX.Y, columnY.Y, columnZ.Y],
                _ => [columnX.Z, columnY.Z, columnZ.Z]
            };
            for (int column = 0; column < 3; column++)
            {
                result = result.Write(row * 6 + column * 2, BitConverter.GetBytes(unchecked((short)values[column])));
            }
        }
        result = CopyBytes(source, 20, result, 20, 12);
        return frame with { State = frame.State.WriteView(ViewAddress, result, 32) };
    }

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect285Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect285 requires its current action header.");
        return packets.Headers[parameters.HeaderIndex7C];
    }
}
