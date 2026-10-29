using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicPackedProjectionStep(NativeBattleWaterExecutionFrame Frame, NativeBattleGeometryRegisterStep Registers, uint SourceCursor, uint ScratchCursor);

public static class NativeBattleCinematicPackedProjection50FD40
{
    public static NativeBattleCinematicPackedProjectionStep Execute(NativeBattleWaterExecutionFrame frame, uint sourceCursor, uint scratchCursor, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, short dqa, int dqb, Func<uint, int, (byte[] Data, bool[] Known)> readSource, Action<uint, NativeBattleVector, NativeBattleActionMatrix>? observeSource = null)
    {
        ArgumentNullException.ThrowIfNull(readSource);
        var countBytes = Read(sourceCursor, 2);
        RequireKnown(countBytes.Known, 2);
        int count = BinaryPrimitives.ReadInt16LittleEndian(countBytes.Data);
        if (count == 0)
        {
            return new(frame, registers, sourceCursor, scratchCursor);
        }
        sourceCursor = unchecked(sourceCursor + 2);
        for (int index = 0; index < count; index++)
        {
            var input = Read(sourceCursor, 8);
            RequireKnown(input.Known, 6);
            registers = registers with { Colors = registers.Colors with { NormalInput = input.Data.ToImmutableArray(), NormalInputKnown = input.Known.All(value => value) ? null : input.Known.ToImmutableArray() } };
            NativeBattleActionMatrix? sourceMatrix = observeSource is null ? null : NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(registers.Geometry).ReadMatrix();
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, dqa, dqb);
            uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Packed projection requires its current screen result.");
            uint depth = registers.Geometry.Sz[3] ?? throw new NotSupportedException("Packed projection requires its current depth result.");
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Packed projection requires its current flags result.");
            byte[] output = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(output, screen);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), (Math.Max(10u, depth) & 0xFFFF) | (flags & 0xFFFF0000));
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(scratchCursor, 8), output) };
            if (observeSource is not null)
            {
                observeSource(scratchCursor, new(BinaryPrimitives.ReadInt16LittleEndian(input.Data), BinaryPrimitives.ReadInt16LittleEndian(input.Data.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(input.Data.AsSpan(4))), sourceMatrix!);
            }
            sourceCursor = unchecked(sourceCursor + 6);
            scratchCursor = unchecked(scratchCursor + 8);
        }
        return new(frame, registers, sourceCursor, scratchCursor);

        (byte[] Data, bool[] Known) Read(uint address, int length)
        {
            var value = readSource(address, length);
            if (value.Data is null || value.Known is null || value.Data.Length != length || value.Known.Length != length)
            {
                throw new InvalidDataException("Packed projection requires its exact source bytes and knownness width.");
            }
            return value;
        }
    }

    private static void RequireKnown(bool[] known, int length)
    {
        if (known.Take(length).Any(value => !value))
        {
            throw new NotSupportedException("Packed projection cannot consume unknown count or XYZ bytes.");
        }
    }
}
