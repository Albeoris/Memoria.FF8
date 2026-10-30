using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuelEchoDrawing
{
    public static TFrame Draw6A2760<TFrame>(TFrame frame, NativeBattleGraphicsModel model, uint echoAddress, int echoOffset, Func<TFrame, NativeBattleModulePoolTask> readEcho, Func<TFrame, NativeBattleModulePoolTask, IReadOnlyList<(int Offset, int Length)>, TFrame> storeEcho, uint sectionAddress, Func<TFrame, int, int, byte[]> readSection, ushort scratchMaximum, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<TFrame, uint, ImmutableArray<byte>, TFrame> writeCpu, Func<TFrame, uint, int, (byte[] Data, bool[] Known)> readCpu, Func<TFrame, NativeBattleGfGeometryBatch, TFrame> submitCommon, short zsf3, short zsf4, Func<TFrame, NativeBattleFileStaging> readStaging, Func<TFrame, NativeBattleFileStaging, TFrame> writeStaging, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<int, long>? pilotHistoryGeneration = null)
    {
        return Draw(frame, model, echoAddress, echoOffset, readEcho, storeEcho, sectionAddress, readSection, scratchMaximum, view, projection, writeCpu, readCpu, submitCommon, zsf3, zsf4, readStaging, writeStaging, 512, 256, true, pilotIdentity, pilotHistoryGeneration);
    }

    public static TFrame Draw697520<TFrame>(TFrame frame, NativeBattleGraphicsModel model, uint echoAddress, int echoOffset, Func<TFrame, NativeBattleModulePoolTask> readEcho, Func<TFrame, NativeBattleModulePoolTask, IReadOnlyList<(int Offset, int Length)>, TFrame> storeEcho, uint sectionAddress, Func<TFrame, int, int, byte[]> readSection, ushort scratchMaximum, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<TFrame, uint, ImmutableArray<byte>, TFrame> writeCpu, Func<TFrame, uint, int, (byte[] Data, bool[] Known)> readCpu, Func<TFrame, NativeBattleGfGeometryBatch, TFrame> submitCommon, short zsf3, short zsf4, Func<TFrame, NativeBattleFileStaging> readStaging, Func<TFrame, NativeBattleFileStaging, TFrame> writeStaging, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<int, long>? pilotHistoryGeneration = null)
    {
        return Draw(frame, model, echoAddress, echoOffset, readEcho, storeEcho, sectionAddress, readSection, scratchMaximum, view, projection, writeCpu, readCpu, submitCommon, zsf3, zsf4, readStaging, writeStaging, 1152, 576, true, pilotIdentity, pilotHistoryGeneration);
    }

    public static TFrame Draw7EE0<TFrame>(TFrame frame, NativeBattleGraphicsModel model, uint echoAddress, int echoOffset, Func<TFrame, NativeBattleModulePoolTask> readEcho, Func<TFrame, NativeBattleModulePoolTask, IReadOnlyList<(int Offset, int Length)>, TFrame> storeEcho, uint sectionAddress, Func<TFrame, int, int, byte[]> readSection, ushort scratchMaximum, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<TFrame, uint, ImmutableArray<byte>, TFrame> writeCpu, Func<TFrame, uint, int, (byte[] Data, bool[] Known)> readCpu, Func<TFrame, NativeBattleGfGeometryBatch, TFrame> submitCommon, short zsf3, short zsf4, Func<TFrame, NativeBattleFileStaging> readStaging, Func<TFrame, NativeBattleFileStaging, TFrame> writeStaging, bool frozen, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<int, long>? pilotHistoryGeneration = null)
    {
        return Draw(frame, model, echoAddress, echoOffset, readEcho, storeEcho, sectionAddress, readSection, scratchMaximum, view, projection, writeCpu, readCpu, submitCommon, zsf3, zsf4, readStaging, writeStaging, 64, 32, !frozen, pilotIdentity, pilotHistoryGeneration);
    }

    private static TFrame Draw<TFrame>(TFrame frame, NativeBattleGraphicsModel model, uint echoAddress, int echoOffset, Func<TFrame, NativeBattleModulePoolTask> readEcho, Func<TFrame, NativeBattleModulePoolTask, IReadOnlyList<(int Offset, int Length)>, TFrame> storeEcho, uint sectionAddress, Func<TFrame, int, int, byte[]> readSection, ushort scratchMaximum, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<TFrame, uint, ImmutableArray<byte>, TFrame> writeCpu, Func<TFrame, uint, int, (byte[] Data, bool[] Known)> readCpu, Func<TFrame, NativeBattleGfGeometryBatch, TFrame> submitCommon, short zsf3, short zsf4, Func<TFrame, NativeBattleFileStaging> readStaging, Func<TFrame, NativeBattleFileStaging, TFrame> writeStaging, int historyStride, int preparedOffset, bool completeHistory = true, NativeBattlePilotVisualIdentity? pilotIdentity = null, Func<int, long>? pilotHistoryGeneration = null)
    {
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(readStaging(frame), 116);
        frame = writeStaging(frame, temporary.Staging);
        uint vertices = checked(0x196B018u - (uint)scratchMaximum * 8);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel echo requires its current model matrix64.");
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, world);
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Duel echo requires its current model mesh mask124.");
        (byte[] data, bool[] known) = readStaging(frame).ReadSnapshot(temporary.Address, 116);
        NativeBattleModulePoolTask context = new(0, 0, null, 0, data.ToImmutableArray());
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                context = context.MarkPayloadUnknown(index + 12, 1);
            }
        }
        context = Matrix(context, 12, basis).WriteDword(48, vertices).WriteWord(56, 0).WriteWord(58, 0).WriteWord(60, 320).WriteWord(62, 216).WriteDword(68, mask).WriteDword(120, echoAddress);
        foreach ((int offset, int length) in new (int, int)[] { (12, 18), (32, 12), (48, 4), (56, 8), (68, 4), (120, 4) })
        {
            StoreContext(context, offset, length);
        }
        NativeBattleModulePoolTask echo = readEcho(frame);
        int histories = echo.ReadByte(echoOffset + 1);
        int amount = 2048;
        for (int history = 0; history < histories; history++, amount -= 1024)
        {
            uint color = model.Color40 ?? throw new NotSupportedException("Native Duel echo requires its actual model color40.");
            context = context.WriteDword(64, Scale45E9D0(color, amount + (amount >> 1)));
            StoreContext(context, 64, 4);
            int bank = NativeBattleDuelEcho.ReadHistoryOffset(echo, echoOffset, history, historyStride);
            context = context.WriteDword(124, checked(echoAddress + (uint)(bank - echoOffset)));
            StoreContext(context, 124, 4);
            int count = echo.ReadByte(echoOffset + 4);
            List<(int Offset, int Length)> writes = [];
            for (int bone = 0; bone < count; bone++)
            {
                int source = checked(bank + bone * 32);
                NativeBattleActionMatrix prepared = NativeBattleDuelEchoProjection.ComposeHistory460820(basis, NativeBattleDuelEchoWalker.Matrix(echo, source));
                int destination = checked(source + preparedOffset);
                echo = Matrix(echo, destination, prepared);
                writes.Add((destination - echoOffset, 18));
                writes.Add((destination + 20 - echoOffset, 12));
            }
            frame = storeEcho(frame, echo, writes);
            context = NativeBattleDuelEchoWalker.Draw(context, 44, sectionAddress, (offset, length) => readSection(frame, offset, length), echo, echoOffset, history, projection, (offset, bytes) =>
            {
                if (offset < 0 || bytes.Length > scratchMaximum * 8 - offset)
                {
                    throw new InvalidDataException("Native Duel echo projected write leaves its actual507010 CPU allocation.");
                }
                frame = writeCpu(frame, checked(vertices + (uint)offset), bytes);
            }, offset =>
            {
                if (offset < 0 || offset > scratchMaximum * 8 - 8)
                {
                    throw new InvalidDataException("Native Duel echo projected read leaves its actual507010 CPU allocation.");
                }
                return readCpu(frame, checked(vertices + (uint)offset), 8);
            }, batch => frame = submitCommon(frame, batch), 17, 0, zsf3, zsf4, StoreContext, historyStride, preparedOffset, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + bank), ResourceGeneration = pilotHistoryGeneration?.Invoke(bank) ?? identity.ResourceGeneration } : null);
        }
        frame = writeStaging(frame, NativeBattleTemporaryMemory.Release5082D0(readStaging(frame), 116));
        if (!completeHistory)
        {
            return frame;
        }
        bool decrement = echo.ReadByte(echoOffset) == 0 && echo.ReadByte(echoOffset + 1) != 0;
        echo = NativeBattleDuelEcho.CompleteDraw6A2760(echo, echoOffset);
        return storeEcho(frame, echo, decrement ? [(0, 2)] : [(0, 1)]);

        void StoreContext(NativeBattleModulePoolTask current, int offset, int length)
        {
            NativeBattleFileStaging staging = readStaging(frame);
            int end = checked(offset + length);
            int cursor = offset;
            if (current.ReadUnknownPayloadRanges() is { } ranges)
            {
                foreach (NativeBattleEffectUnknownRegion range in ranges)
                {
                    int unknownStart = Math.Max(cursor, range.Offset);
                    int unknownEnd = Math.Min(end, range.Offset + range.Length);
                    if (unknownStart >= end)
                    {
                        break;
                    }
                    if (unknownEnd <= cursor)
                    {
                        continue;
                    }
                    if (unknownStart > cursor)
                    {
                        staging = staging.Write(checked(temporary.Address + (uint)(cursor - 12)), current.ReadRawPayloadSpan().Slice(cursor - 12, unknownStart - cursor));
                    }
                    staging = staging.MarkUnknown(checked(temporary.Address + (uint)(unknownStart - 12)), unknownEnd - unknownStart);
                    cursor = unknownEnd;
                }
            }
            if (cursor < end)
            {
                staging = staging.Write(checked(temporary.Address + (uint)(cursor - 12)), current.ReadRawPayloadSpan().Slice(cursor - 12, end - cursor));
            }
            frame = writeStaging(frame, staging);
        }
    }

    private static NativeBattleModulePoolTask Matrix(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task.WriteDword(offset + 20, unchecked((uint)matrix.X)).WriteDword(offset + 24, unchecked((uint)matrix.Y)).WriteDword(offset + 28, unchecked((uint)matrix.Z));
    }

    private static uint Scale45E9D0(uint color, int factor)
    {
        uint result = 0;
        for (int index = 0; index < 3; index++)
        {
            int value = NativePrecision53.RoundInteger((byte)(color >> (index * 8)) * (factor / 4096d));
            result |= (uint)unchecked((byte)Math.Clamp(value, short.MinValue, short.MaxValue)) << (index * 8);
        }
        return result;
    }
}
