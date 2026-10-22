using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoom8TargetSetup
{
    public static NativeBattleDoom8Frame Initialize8C52C0(NativeBattleDoom8Frame frame, uint address, Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback != 0x8C4D90)
        {
            throw new InvalidDataException("Doom target initialization requires its actual callback.");
        }
        int target = task.ReadByte(0x2D);
        if ((uint)target >= 7)
        {
            throw new InvalidDataException("Doom target initialization exceeds its physical model rows.");
        }
        NativeBattleModelTransform transform = readModel(target).Transform ?? throw new NotSupportedException("Doom target initialization requires its actual packed position.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Doom target initialization requires its physical temporary memory.");
        NativeBattleDoom8State state = frame.State.Write(NativeBattleDoom8State.SharedAddress, new byte[NativeBattleDoom8State.SharedLength]);
        state = state.WithSharedWord(8, unchecked((ushort)transform.Position28.X)).WithSharedWord(10, unchecked((ushort)transform.Position28.Y)).WithSharedWord(12, unchecked((ushort)transform.Position28.Z)).WithSharedWord(14, unchecked((ushort)transform.Vertical34));
        short angle = unchecked((short)((NativeWorldScriptedPathMath.NativeAngle(transform.Position28.X, transform.Position28.Z) - 0x800) & 0xFFF));
        state = state.WithSharedWord(0x4A, unchecked((ushort)angle));
        NativeBattlePackedMatrix32 light = frame.Memory.CaptureEffectLight1D99AB0();
        byte[] bytes = light.Bytes.ToArray();
        bool[] known = light.Known.ToArray();
        short[] identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        for (int index = 0; index < identity.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), identity[index]);
            known[index * 2] = true;
            known[index * 2 + 1] = true;
        }
        bytes.AsSpan(20, 12).Clear();
        known.AsSpan(20, 12).Fill(true);
        light = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleEffectMemory memory = frame.Memory.PublishEffectLight1D99AB0(light);
        NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(memory, staging, light, 'Y', angle);
        return frame with { Memory = rotated.Memory.PublishEffectLight1D99AB0(rotated.Matrix), Staging = rotated.Staging, State = state };
    }
}
