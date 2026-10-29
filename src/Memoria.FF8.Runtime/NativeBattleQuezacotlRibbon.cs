using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlRibbonStep(ImmutableArray<byte> Task128, NativeBattleGfGeometryAttempt TaskWrites, ushort Random17A0, NativeBattleGfGeometryBatch CommonPackets, int Returned)
{
    public uint PathOutlineDirectionDword { get; init; }
}

/// <summary>6C8850's endpoint interpolation, ribbon jitter, paused drawing and post-progress fade.</summary>
public static class NativeBattleQuezacotlRibbon
{
    public static NativeBattleStackFrame ResolveEntryStack6C8850(NativeBattleExecutionStack stack, byte entryLifecycle)
    {
        if (entryLifecycle is not (0 or 2 or 3 or 4))
        {
            throw new NotSupportedException("Native6C8850 stack derivation requires its proved outer47D490 phase; phase1 has a distinct pending stack frame.");
        }
        NativeBattleStackFrame frame = stack.ReadCommonFrame();
        frame = frame.EnterCdecl(0x1C, 2, 0); //47CF60→47CCB0, followed by the tail jump into47D490.
        frame = frame.EnterCdecl(0, 2, 0); //47D490→500900 on the admitted outer phase.
        frame = frame.EnterCdecl(0, 0, 4); //50093A→508420 with the main descriptor argument.
        frame = frame.EnterCdecl(0, 4, 4); //508420→6C3760 with the main task argument.
        frame = frame.EnterCdecl(0, 2, 12); //6C388C→508420; two pose arguments remain pending beside the descriptor.
        return frame.EnterCdecl(0, 4, 4); //508420→6C8850 with the ribbon task argument.
    }

    public static NativeBattleQuezacotlRibbonStep Advance6C8850(ImmutableArray<byte> task128, NativeBattleQuezacotlRibbonResources resources, int pairIndex, Func<int, NativeBattleActionMatrix> readBoneMatrix, ImmutableArray<short> currentEnvelope16, ushort random17A0, bool paused, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Action<ushort>? observeDepth = null, uint? incomingDirectionDword = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Advance6C8850(task128, resources, resources.PrimaryBank, pairIndex, readBoneMatrix, currentEnvelope16, random17A0, paused, view, projection, observeDepth, incomingDirectionDword, pilotIdentity);
    }

    public static NativeBattleQuezacotlRibbonStep Advance6C8850(ImmutableArray<byte> task128, NativeBattleQuezacotlRibbonResources resources, NativeBattleQuezacotlRibbonBank bank, int pairIndex, Func<int, NativeBattleActionMatrix> readBoneMatrix, ImmutableArray<short> currentEnvelope16, ushort random17A0, bool paused, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Action<ushort>? observeDepth = null, uint? incomingDirectionDword = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (task128.IsDefault || task128.Length != 128 || bank.Pairs.IsDefault || bank.Pairs.Length % 8 != 0 || (uint)pairIndex >= bank.Pairs.Length / 8 || currentEnvelope16.IsDefault || currentEnvelope16.Length != 16 || random17A0 >= 0x8000)
        {
            throw new InvalidDataException("Native6C8850 requires its actual128-byte task, endpoint pair, current envelope and15-bit private random owner.");
        }
        short ReadTask(int offset) => BinaryPrimitives.ReadInt16LittleEndian(task128.AsSpan().Slice(offset));
        ReadOnlySpan<byte> pair = bank.Pairs.AsSpan().Slice(pairIndex * 8, 8);
        short progress = ReadTask(0x0E);
        NativeBattleQuezacotlRibbonSample first = NativeBattleQuezacotlRibbonMath.Sample6C8F70(bank.ControlTriangles, BinaryPrimitives.ReadInt16LittleEndian(pair), BinaryPrimitives.ReadInt16LittleEndian(pair[2..]), progress, readBoneMatrix, resources.SqrtWords256);
        NativeBattleQuezacotlRibbonSample second = NativeBattleQuezacotlRibbonMath.Sample6C8F70(bank.ControlTriangles, BinaryPrimitives.ReadInt16LittleEndian(pair[4..]), BinaryPrimitives.ReadInt16LittleEndian(pair[6..]), progress, readBoneMatrix, resources.SqrtWords256);
        int distanceQ12 = NativeBattleQuezacotlRibbonMath.SqrtQ12(NativeBattleQuezacotlRibbonMath.SquaredLength(second.Position.X - first.Position.X, second.Position.Y - first.Position.Y, second.Position.Z - first.Position.Z), resources.SqrtWords256);
        int distance = distanceQ12 >> 12;
        int tangentWeight = distanceQ12 >> task128[0x12];
        NativeBattleVector firstOffset = NativeBattleQuezacotlRibbonMath.Scale(first.Normal, tangentWeight);
        NativeBattleVector secondOffset = NativeBattleQuezacotlRibbonMath.Scale(second.Normal, tangentWeight);
        NativeBattleVector firstControl = Add(first.Position, firstOffset);
        NativeBattleVector secondControl = Add(second.Position, secondOffset);
        ImmutableArray<NativeBattleQuezacotlRibbonPoint>.Builder points = ImmutableArray.CreateBuilder<NativeBattleQuezacotlRibbonPoint>(16);
        points.Add(Point(0, first.Position));
        for (int index = 1; index < 15; index++)
        {
            int ratio = (index << 12) / 15;
            int inverse = 4096 - ratio;
            NativeBattleVector left = NativeBattleQuezacotlRibbonMath.Blend(first.Position, firstControl, inverse, ratio);
            NativeBattleVector right = NativeBattleQuezacotlRibbonMath.Blend(second.Position, secondControl, ratio, inverse);
            points.Add(Point(index, NativeBattleQuezacotlRibbonMath.Blend(left, right, inverse, ratio)));
        }
        points.Add(Point(15, second.Position));
        for (int index = 1; index < 15; index++)
        {
            int amplitude = unchecked(currentEnvelope16[index] * distance) >> 12;
            NativeBattleVector position = points[index].Position;
            short Noise(short value)
            {
                NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random17A0);
                int delta = unchecked((random17A0 - 0x4000) * amplitude) >> 18;
                return unchecked((short)(value + delta));
            }
            points[index] = points[index] with { Position = new(Noise(position.X), Noise(position.Y), Noise(position.Z)) };
        }
        NativeBattleGfGeometryBatch packets = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(points.ToImmutable(), view, projection, resources.DistanceBytes16384, observeDepth, incomingDirectionDword, pilotIdentity);
        SkinWrites writes = new(128, 0);
        byte[] task = task128.ToArray();
        int returned = 0;
        if (!paused)
        {
            progress = unchecked((short)(progress + ReadTask(0x10)));
            Store(0x0E, progress);
            if (progress > 4096)
            {
                Store(0x0E, 4096);
                for (int index = 0; index < 16; index++)
                {
                    short decay = unchecked((short)((5120 - currentEnvelope16[index]) >> task128[0x13]));
                    short color = unchecked((short)(ReadTask(0x22 + index * 6) - decay));
                    Store(0x22 + index * 6, Math.Max(color, (short)0));
                }
                short lifetime = unchecked((short)(ReadTask(0x0C) + 1));
                Store(0x0C, lifetime);
                if (lifetime >= 10)
                {
                    returned = 2;
                }
            }
        }
        //6C88B0's second endpoint output aliases the later6C79A0 direction slot.
        uint pathDirection = (ushort)second.Position.X | ((uint)(ushort)second.Position.Y << 16);
        return new(task.ToImmutableArray(), writes.Attempt(null), random17A0, packets, returned) { PathOutlineDirectionDword = pathDirection };

        NativeBattleQuezacotlRibbonPoint Point(int index, NativeBattleVector position)
        {
            return new(position, ReadTask(0x20 + index * 6), ReadTask(0x22 + index * 6));
        }

        void Store(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(offset), value);
            writes.Word16(offset, unchecked((ushort)value));
        }
    }

    private static NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second)
    {
        return new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
    }
}
