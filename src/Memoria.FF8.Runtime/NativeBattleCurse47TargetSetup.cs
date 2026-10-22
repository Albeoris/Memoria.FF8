using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCurse47TargetSetup
{
    public static NativeBattleCurse47Frame Initialize82B0F0(NativeBattleCurse47Frame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(frame, address);
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != NativeBattleCurse47Controller.Entry || task.Callback != NativeBattleCurse47Controller.TargetCallback)
        {
            throw new InvalidDataException("Curse target initialization requires its actual callback.");
        }
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Curse target initialization requires its retained shared pointer.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(0x15B8028, 4)) != NativeBattleCurse47State.SharedAddress)
        {
            throw new NotSupportedException("Curse target initialization requires its actual shared allocation.");
        }
        int target = task.ReadByte(0x2D);
        if ((uint)target >= 7)
        {
            throw new InvalidDataException("Curse target initialization exceeds its physical model rows.");
        }
        NativeBattleModelTransform transform = readModel(target).Transform ?? throw new NotSupportedException("Curse target initialization requires its actual packed position.");
        NativeBattleCurse47State state = frame.State.Write(NativeBattleCurse47State.SharedAddress, new byte[NativeBattleCurse47State.SharedLength]);
        state = state.WithSharedWord(8, unchecked((ushort)transform.Position28.X)).WithSharedWord(10, unchecked((ushort)transform.Position28.Y)).WithSharedWord(12, unchecked((ushort)transform.Position28.Z)).WithSharedWord(14, unchecked((ushort)transform.Vertical34));
        short angle = unchecked((short)((NativeWorldScriptedPathMath.NativeAngle(transform.Position28.X, transform.Position28.Z) - 0x800) & 0xFFF));
        state = state.WithSharedWord(0x4A, unchecked((ushort)angle));
        return frame with { State = state };
    }
}
