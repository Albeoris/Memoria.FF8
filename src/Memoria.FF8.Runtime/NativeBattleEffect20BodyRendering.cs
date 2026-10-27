using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect20BodyPreparation(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary8C, NativeBattleActionMatrix FirstBone);
public sealed record NativeBattleEffect20BodyInputs(NativeBattleActionMatrix ModelWorld, short? ModelWorldPadding18, NativeBattleActionMatrix FirstBone, NativeBattlePackedMatrix32 Camera, uint ProjectedScratch, NativeBattleEffectArena CopiedState4, uint ModelColor28, byte Fade7, Func<NativeBattleEffectMemory, NativeBattleDrawStock15Rotate, NativeBattleEffectMemory> Rotate);

public static class NativeBattleEffect20BodyRendering
{
    public const int TemporaryLength = 0x8C;

    public static NativeBattleEffect20BodyPreparation Prepare(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleEffectArena temporary, NativeBattleEffect20BodyInputs inputs)
    {
        temporary.Validate();
        inputs.Camera.Validate();
        inputs.CopiedState4.Validate();
        temporary.ValidateExtent(TemporaryLength);
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = Signed(task, 0x58), Y = Signed(task, 0x5A) - 0x1140, Z = Signed(task, 0x5C) };
        temporary = Matrix(temporary, 0x40, local, Padding(temporary, 0x52), true);
        Rotate('Y', Signed(task, 0x6A));
        Rotate('X', Signed(task, 0x68));
        Rotate('Z', Signed(task, 0x6C));
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Compose56C2F0(local, inputs.FirstBone with { Y = unchecked(inputs.FirstBone.Y + 0x1140) });
        temporary = Matrix(temporary, 0, inputs.ModelWorld, inputs.ModelWorldPadding18, true);
        NativeBattleActionMatrix camera = inputs.Camera.ReadMatrix();
        short? cameraPadding = inputs.Camera.Known[18] && inputs.Camera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(inputs.Camera.Bytes.AsSpan().Slice(18)) : null;
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect body preparation requires its retained geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect body preparation requires its retained data registers."));
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, true, false, cameraPadding) };
        for (int column = 0; column < 3; column++)
        {
            step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)inputs.ModelWorld.Rotation[column]), unchecked((ushort)inputs.ModelWorld.Rotation[column + 3]), unchecked((ushort)inputs.ModelWorld.Rotation[column + 6]));
            step = NativeBattleGeometry.Transform(step, false, true);
            for (int row = 0; row < 3; row++)
            {
                temporary = Word(temporary, 0x20 + row * 6 + column * 2, unchecked((ushort)step.Colors.Ir[row]!.Value));
            }
        }
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, false, true, null) };
        NativeBattleVector position = new(unchecked((short)inputs.ModelWorld.X), unchecked((short)inputs.ModelWorld.Y), unchecked((short)inputs.ModelWorld.Z));
        step = NativeBattleGeometry.LoadVector(step, 0, position, position.Z < 0 ? (short)-1 : (short)0);
        step = NativeBattleGeometry.Transform(step);
        NativeBattleActionMatrix composed = new(Enumerable.Range(0, 9).Select(lane => unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(0x20 + lane * 2, 2)))).ToImmutableArray(), step.Colors.Mac[0]!.Value, step.Colors.Mac[1]!.Value, step.Colors.Mac[2]!.Value, 4096);
        temporary = Dword(temporary, 0x34, unchecked((uint)composed.X));
        temporary = Dword(temporary, 0x38, unchecked((uint)composed.Y));
        temporary = Dword(temporary, 0x3C, unchecked((uint)composed.Z));
        first = NativeBattleGfMatrices.Compose56C2F0(composed, first);
        temporary = Dword(temporary, 0x64, inputs.ProjectedScratch);
        temporary = NativeBattleEffectNodeContextMemory.Copy(inputs.CopiedState4, 0, temporary, 0x70, 4);
        temporary = Dword(temporary, 0x7C, inputs.ModelColor28);
        temporary = Dword(temporary, 0x80, 2);
        foreach (int offset in new[] { 0x74, 0x76, 0x84 })
        {
            temporary = Word(temporary, offset, 0);
        }
        temporary = Word(temporary, 0x78, 320);
        temporary = Word(temporary, 0x7A, 216);
        temporary = temporary.Write(0x88, [inputs.Fade7, inputs.Fade7, inputs.Fade7]);
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, temporary, first);

        void Rotate(char axis, short angle)
        {
            memory = inputs.Rotate(memory, new(local, axis, angle) { Padding18 = Padding(temporary, 0x52) is short value ? unchecked((ushort)value) : null });
            local = axis switch
            {
                'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(local, angle),
                'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(local, angle),
                _ => NativeBattleCuragaMatrices.RotateZ8DD960(local, angle)
            };
            temporary = Matrix(temporary, 0x40, local, null, false);
        }
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static NativeBattleEffectArena Matrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix, short? padding, bool translation)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            arena = Word(arena, offset + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        if (translation)
        {
            arena = padding is short value ? Word(arena, offset + 18, unchecked((ushort)value)) : arena.MarkUnknown(offset + 18, 2);
            arena = Dword(arena, offset + 20, unchecked((uint)matrix.X));
            arena = Dword(arena, offset + 24, unchecked((uint)matrix.Y));
            arena = Dword(arena, offset + 28, unchecked((uint)matrix.Z));
        }
        return arena;
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
