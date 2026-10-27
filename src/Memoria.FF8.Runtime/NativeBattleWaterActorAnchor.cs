using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterActorAnchorResult(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix, NativeBattleFullMacVertex Anchor, NativeBattleGeometryRegisterStep Registers);
public sealed record NativeBattleWaterActorAnchorSource(ImmutableArray<byte> Skeleton, ImmutableArray<NativeBattleActionMatrix> LocalBones, NativeBattleActionMatrix Basis);

public sealed record NativeBattleWaterActorAnchorServices(Func<NativeBattleWaterExecutionFrame, NativeBattleGeometryRegisterStep> ReadRegisters, Func<NativeBattleWaterExecutionFrame, NativeBattleGeometryRegisterStep, NativeBattleWaterExecutionFrame> PublishRegisters)
{
    public NativeBattleWaterActorAnchorResult EvaluateB66B80(NativeBattleWaterExecutionFrame frame, ImmutableArray<byte> skeleton, ImmutableArray<NativeBattleActionMatrix> localBones, short selector, int fraction, NativeBattleActionMatrix basis)
    {
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, skeleton, localBones, selector, fraction, basis, ReadRegisters(frame));
        return result with { Frame = PublishRegisters(result.Frame, result.Registers) };
    }

    public NativeBattleWaterActorAnchorResult EvaluateB66B80(NativeBattleWaterExecutionFrame frame, int skeletonLength, Func<int, int, byte[]> readMetadata, ImmutableArray<NativeBattleActionMatrix> localBones, short selector, int fraction, NativeBattleActionMatrix basis)
    {
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, skeletonLength, readMetadata, localBones, selector, fraction, basis, ReadRegisters(frame));
        return result with { Frame = PublishRegisters(result.Frame, result.Registers) };
    }
}

/// <summary>The parent-length anchor over current local posed matrices and an explicit caller basis.</summary>
public static class NativeBattleWaterActorAnchor
{
    public static NativeBattleWaterActorAnchorResult EvaluateB66B80(NativeBattleWaterExecutionFrame frame, ImmutableArray<byte> skeleton, ImmutableArray<NativeBattleActionMatrix> localBones, short selector, int fraction, NativeBattleActionMatrix basis, NativeBattleGeometryRegisterStep registers)
    {
        if (skeleton.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native actor anchor requires its actual skeleton and current local posed bone matrices.");
        }
        return EvaluateB66B80(frame, skeleton.Length, (offset, length) => skeleton.AsSpan().Slice(offset, length).ToArray(), localBones, selector, fraction, basis, registers);
    }

    public static NativeBattleWaterActorAnchorResult EvaluateB66B80(NativeBattleWaterExecutionFrame frame, int skeletonLength, Func<int, int, byte[]> readMetadata, ImmutableArray<NativeBattleActionMatrix> localBones, short selector, int fraction, NativeBattleActionMatrix basis, NativeBattleGeometryRegisterStep registers)
    {
        ArgumentNullException.ThrowIfNull(readMetadata);
        ArgumentNullException.ThrowIfNull(registers);
        int count = readMetadata(0, 1)[0];
        if (count == 0 || skeletonLength < 0x10 + 0x30 * count || localBones.IsDefault || localBones.Length != count)
        {
            throw new NotSupportedException("Native actor anchor requires its actual skeleton and current local posed bone matrices.");
        }
        int bone = selector;
        if (selector >= 0xF0)
        {
            int alias = selector - 0xEC;
            if (alias >= skeletonLength)
            {
                throw new NotSupportedException("Native actor anchor alias leaves its supplied skeleton owner.");
            }
            bone = readMetadata(alias, 1)[0] & 0x7F;
        }
        if (bone < 0 || bone >= localBones.Length)
        {
            throw new NotSupportedException("Native actor anchor selector leaves its actual decoded skeleton.");
        }
        frame = frame.WriteTemporaryDword(0xC0, 0);
        short parent = BinaryPrimitives.ReadInt16LittleEndian(readMetadata(0x10 + bone * 0x30, 2));
        short distance = 0;
        if (parent >= 0)
        {
            if (parent >= localBones.Length)
            {
                throw new NotSupportedException("Native actor anchor parent leaves its actual skeleton records.");
            }
            short length = BinaryPrimitives.ReadInt16LittleEndian(readMetadata(0x10 + parent * 0x30 + 2, 2));
            distance = unchecked((short)(unchecked(length * fraction) >> 12));
        }
        frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)distance));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(basis, localBones[bone]);
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0xD0 + lane * 2, unchecked((ushort)composed.Rotation[lane]));
        }
        frame.TemporaryFrame.MarkUnknown(0xE2, 2);
        frame = frame.WriteTemporaryDword(0xE4, unchecked((uint)composed.X)).WriteTemporaryDword(0xE8, unchecked((uint)composed.Y)).WriteTemporaryDword(0xEC, unchecked((uint)composed.Z));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, null) };
        registers = registers with { Colors = NativeBattleCinematicRibbonInput.Capture(frame, frame.TemporaryFrame.Address + 0xC0, registers.Colors) };
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleFullMacVertex anchor = new(registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value, registers.Colors.Flags!.Value);
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)anchor.X)).WriteTemporaryDword(0xF4, unchecked((uint)anchor.Y)).WriteTemporaryDword(0xF8, unchecked((uint)anchor.Z)).WriteTemporaryDword(0xFC, anchor.Flags);
        return new(frame, composed, anchor, registers);
    }

    public static NativeBattleWaterExecutionFrame Execute60AA2190(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterActorAnchorSource> resolveActor, NativeBattleWaterActorAnchorServices services)
    {
        ArgumentNullException.ThrowIfNull(resolveActor);
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x60 || (instruction & 0x8000) != 0)
        {
            throw new NotSupportedException("Native selected actor positioning admits only its direct anchor branch.");
        }
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        if (actorIndex >= 7)
        {
            throw new NotSupportedException("Native actor positioning leaves its seven captured physical graphics rows.");
        }
        uint header = CoreDword(frame, 0x279744C);
        uint actorPointer = CoreDword(frame, unchecked(header + 0x60 + (uint)(4 * actorIndex)));
        uint pc = CoreDword(frame, 0x2797450);
        uint relative = unchecked(pc + 2 - program.Address);
        if (program.ArenaOffset < 0 || program.Length < 2 || program.ArenaOffset > NativeBattleEffectArena.Length - program.Length || relative > (uint)(program.Length - 2))
        {
            throw new InvalidDataException("Native actor anchor operand leaves its actual current program owner.");
        }
        short selector = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked(program.ArenaOffset + (int)relative), 2));
        NativeBattleWaterActorAnchorSource actor = resolveActor(frame, actorPointer) ?? throw new NotSupportedException("Native actor positioning requires its actual captured graphics owner.");
        NativeBattleWaterActorAnchorResult result = services.EvaluateB66B80(frame, actor.Skeleton, actor.LocalBones, selector, 0x1000, actor.Basis);
        frame = result.Frame;
        frame = WriteSlotDword(frame, 0x5C, unchecked((uint)result.Anchor.X << 16));
        frame = WriteSlotDword(frame, 0x60, unchecked((uint)result.Anchor.Y << 16));
        frame = WriteSlotDword(frame, 0x64, unchecked((uint)result.Anchor.Z << 16));
        frame = WriteCoreDword(frame, 0x2797450, unchecked(pc + 4));
        int offset = ArenaOffset(CoreDword(frame, 0x27973E8), 0x100) - bank.SlotBaseOffset;
        if (offset < 0 || offset % 0x100 != 0)
        {
            throw new InvalidDataException("Native actor anchor update requires its current physical slot in the cached bank.");
        }
        if (ReadSlot(frame, 0x18, 1)[0] is not (3 or 5 or 7))
        {
            return frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, offset / 0x100, []) };
        }
        byte[] order = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        for (int index = 0; index < order.Length; index++)
        {
            order[index] = frame.Core.Read(0x2797454 + (uint)index, 1)[0];
            if (order[index] == 0xFF)
            {
                return frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, offset / 0x100, order.ToImmutableArray()) };
            }
        }
        return frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, offset / 0x100, order.ToImmutableArray()) };
    }
}
