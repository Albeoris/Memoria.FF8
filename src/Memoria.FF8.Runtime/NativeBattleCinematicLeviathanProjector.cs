using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicLeviathanProjection(NativeBattleWaterExecutionFrame Frame, NativeBattleScriptedGraphicsMemory Graphics, NativeBattleScriptedColorRegisters Registers);

public static class NativeBattleCinematicLeviathanProjector
{
    public static NativeBattleCinematicLeviathanProjection Project(NativeBattleWaterExecutionFrame frame, uint source, uint output, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory graphics, NativeBattleScriptedColorRegisters registers)
    {
        int count = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(source, 2), 2));
        source = unchecked(source + 2);
        frame = frame.WriteTemporaryDword(0xF0, (uint)count).WriteTemporaryDword(0xF4, source).WriteTemporaryDword(0xF8, output);
        NativeBattleEffectArena depths = graphics.Depths;
        for (int index = 0; index < Math.Max(count, 1); index++)
        {
            short y = Word(source + 2);
            short x = Word(source);
            short z = Word(source + 4);
            NativeBattleVector point = new(x, y, z);
            NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, graphics.Dqa, graphics.Dqb);
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(matrix, point);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output, 4), BitConverter.GetBytes(projected.Projected.Screen)) };
            source = unchecked(source + 6);
            output = unchecked(output + 8);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output + 4, 4), BitConverter.GetBytes((uint)projected.Projected.Depth)) };
            depths = depths.Write(checked(index * 4), BitConverter.GetBytes((uint)projected.Projected.Depth));
            byte[] input = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(input, x);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), y);
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), z);
            registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = Enumerable.Repeat(true, 8).ToImmutableArray(), Mac = [mac.X, mac.Y, mac.Z], Ir = [Math.Clamp(mac.X, short.MinValue, short.MaxValue), Math.Clamp(mac.Y, short.MinValue, short.MaxValue), Math.Clamp(mac.Z, short.MinValue, short.MaxValue)], Flags = projected.Projected.Flags };
        }
        frame = frame.WriteTemporaryDword(0xFC, output).WriteTemporaryDword(0xF8, source);
        return new(frame, graphics with { Depths = depths }, registers);

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }
}
