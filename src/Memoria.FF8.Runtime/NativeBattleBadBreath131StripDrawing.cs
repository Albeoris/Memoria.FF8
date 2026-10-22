using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBadBreath131StripDrawing
{
    public static (NativeBattleBadBreath131Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleBadBreath131Frame frame, uint temporaryAddress, uint sourceAddress, uint selectedVerticesAddress, NativeBattleCameraProjection projection, NativeBattleSummonColors constants)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        if (selectedVerticesAddress != checked(working + ScratchOffset))
        {
            throw new InvalidDataException("Bad Breath strip vertices leave their actual working scratch owner.");
        }
        int scratch = checked((int)(selectedVerticesAddress - ArenaAddress));
        if (scratch < 0 || scratch > NativeBattleEffectArena.Length - ScratchLength)
        {
            throw new InvalidDataException("Bad Breath strip scratch leaves its actual physical arena.");
        }
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Bad Breath strip drawing requires its physical temporary memory.");
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, temporaryAddress, 92), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), 0, 92);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Bad Breath strip drawing requires its current working arena.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Bad Breath strip drawing requires its retained projection controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Bad Breath strip drawing requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Bad Breath strip drawing requires its current arithmetic registers."));
        NativeBattleBadBreath300StripMeshStep drawn = NativeBattleBadBreath300StripMesh.Draw66F710(frame.State.ReadMesh(sourceAddress), sourceAddress, temporary, selectedVerticesAddress, ReadVertex, registers, projection, controls.Dqa, controls.Dqb, controls.Zsf4, constants);
        return (frame with
        {
            Memory = frame.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors },
            Staging = NativeBattleTemporaryMemory.PublishView(staging, temporaryAddress, drawn.Context92, 92)
        }, drawn.Batch);

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > ScratchLength - 8)
            {
                throw new InvalidDataException("Bad Breath strip face leaves its current selected vertex bank.");
            }
            int at = checked(scratch + offset);
            byte[] xyz = arena.Read(at, 6);
            (byte[] bytes, bool[] known) = arena.ReadSnapshot(at + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }
}
