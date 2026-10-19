namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterMeshColorSetup(NativeBattleWaterExecutionFrame Frame, uint Modulation);

/// <summary>The bounded packed-channel lighting specialization and its exact temporary workspace writes.</summary>
public static class NativeBattleWaterMeshColor
{
    public static NativeBattleWaterMeshColorSetup PrepareA9E8A0(NativeBattleWaterExecutionFrame frame)
        => PrepareA9E8A0(frame, frame.ReadTemporaryDword(0x84));

    public static NativeBattleWaterMeshColorSetup PrepareA9E8A0(NativeBattleWaterExecutionFrame frame, uint modulation)
    {
        frame = frame.WriteTemporaryDword(0xE0, 0x10001000);
        frame = frame.WriteTemporaryDword(0xE4, 0x00001000);
        frame = frame.WriteTemporaryDword(0xE8, 0);
        frame = frame.WriteTemporaryDword(0xEC, (uint)(byte)(modulation >> 16) << 5);
        frame = frame.WriteTemporaryDword(0xF0, 0);
        return new(frame, modulation);
    }

    public static uint Modulate4601B0(uint material, uint modulation)
    {
        uint result = material & 0xFF000000u;
        for (int shift = 0; shift < 24; shift += 8)
        {
            int channel = (byte)(material >> shift);
            int multiplier = (byte)(modulation >> shift);
            int rounded = NativeBattlePoseMatrices.RoundNative(channel * multiplier / 8d);
            result |= (uint)Math.Clamp(rounded >> 4, 0, 255) << shift;
        }
        return result;
    }
}
