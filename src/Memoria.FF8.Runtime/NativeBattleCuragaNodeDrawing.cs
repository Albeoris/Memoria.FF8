using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeDrawStep(NativeBattleCuragaFrame Frame, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);
public sealed record NativeBattleCuragaNodePresentationStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleCuragaNodeKinematics
{
    /// <summary>87EC80's active phase:882E20/8804D0/880390/87ED10 share one view; packet stores follow its single node flush.</summary>
    public static NativeBattleCuragaNodePresentationStep AdvanceAndDraw87EC80(NativeBattleCuragaFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState bank = RequiredBank(frame);
        NativeBattleCuragaNodeEngine.Birth(memory, bank);
        (bank, random) = AdvanceList(memory, bank, random, readModel);
        NativeBattleGfGeometryBatch batch = DrawNodes(memory, bank, projection, cameraRoll, colors);
        NativeBattleCuragaNodeDrawStep drawn = CommitDraws(memory, bank, ordering, batch);
        return new(drawn.Frame, random, drawn.Ordering, batch);
    }

    /// <summary>880390 runs the complete sprite list before87ED10's complete mesh list, advancing only the primary cursor.</summary>
    public static NativeBattleCuragaNodeDrawStep DrawNodes880390And87ED10(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleCuragaExecutableState bank = RequiredBank(frame);
        NativeBattleGfGeometryBatch batch = DrawNodes(memory, bank, projection, cameraRoll, colors);
        return CommitDraws(memory, bank, ordering, batch);
    }

    private static NativeBattleCuragaExecutableState RequiredBank(NativeBattleCuragaFrame frame) => frame.Memory.CuragaExecutable ?? throw new NotSupportedException("Native Curaga draw lists require their retained current executable bank.");

    private static NativeBattleCuragaNodeDrawStep CommitDraws(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaExecutableState bank, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        NativeBattleCuragaFrame result = memory.Flush();
        result = result with { Memory = result.Memory with { CuragaExecutable = bank } };
        if (batch.Attempts.Length != 0)
        {
            int cursor = result.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Curaga packet writes require87DC50's actual primary26D75C0 cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(result.Memory, cursor, ordering, batch);
            result = result with { Memory = written.Memory, State = result.State with { PrimaryDrawCursor = written.Cursor } };
            ordering = written.Ordering;
        }
        return new(result, ordering, batch);
    }

    private static NativeBattleGfGeometryBatch DrawNodes(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaExecutableState bank, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleSummonColors? colors)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        Dictionary<uint, ImmutableArray<byte>> resources = [];
        int allocated = 0;
        DrawList(false);
        DrawList(true);
        return new(attempts.ToImmutable(), allocated);

        void DrawList(bool meshes)
        {
            NativeBattleCuragaNodeEngine.WalkList(() => memory, (node, kind) =>
            {
                if (kind != 1)
                {
                    return;
                }
                byte[] source = ReadSource(memory, bank, unchecked((sbyte)memory.Byte(node + 0x1D6)));
                bool selected = meshes ? source[0x16] is 4 or 5 : source[0x16] is 0 or 1 or 2;
                if (!selected)
                {
                    return;
                }
                uint resource = memory.Dword(node + 0x170);
                if (resource == 0)
                {
                    return;
                }
                int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
                for (int instance = 0; instance < count; instance++)
                {
                    if (count != 1)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            memory.Dword(node + 0xC0 + axis * 4, unchecked((uint)(int)(short)memory.Word(node + 0x19C + instance * 8 + axis * 2)));
                        }
                    }
                    NativeBattleActionMatrix matrix = ReadMatrix(memory, node + 0xAC);
                    if (!resources.TryGetValue(resource, out ImmutableArray<byte> bytes))
                    {
                        bytes = bank.Read(resource, checked(NativeBattleCuragaExecutableState.Length - (int)(resource - NativeBattleCuragaExecutableState.Address))).ToImmutableArray();
                        resources.Add(resource, bytes);
                    }
                    NativeBattleGfGeometryBatch batch;
                    if (meshes)
                    {
                        uint flags = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                        short fade = unchecked((short)memory.Word(node + 0x1CE));
                        uint? farColor = null;
                        if (fade != 0)
                        {
                            flags |= 0xC0;
                            farColor = memory.Dword(node + 0x16C);
                        }
                        batch = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(bytes, matrix, projection, new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2), colors: colors);
                    }
                    else
                    {
                        ushort phase = unchecked((ushort)(short)(sbyte)memory.Byte(node + 0x1D0));
                        batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(bytes, phase, matrix, projection, cameraRoll, 17, 2);
                    }
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) });
                    }
                    allocated = checked(allocated + batch.AllocationBytes);
                }
            });
        }
    }
}
