using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    internal const uint Cursor = 0x25FCE30;
    internal const uint Highlight = 0x25FD708;
    internal const uint Exit = 0x25FFAA4;
    internal const uint Camera = 0x25FCDE0;

    public static NativeBattleEffect68Step Advance(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        uint callback = Read(frame, address).Callback;
        frame = callback switch
        {
            0x7DC6F0 => Reticle(frame, address, resources, inputs),
            0x7DC950 => Pair(frame, address, resources, inputs),
            0x7DCBE0 => MorphTask(frame, address, resources, inputs),
            0x7DCF10 => Corner(frame, address, resources, inputs, false),
            0x7DD150 => Corner(frame, address, resources, inputs, true),
            0x7DD510 => Annotation(frame, address, resources, inputs),
            0x7DD650 => SpriteTask(frame, address, resources, inputs),
            0x7DD950 => CameraTask(frame, address, resources, inputs),
            0x7DDED0 => Decoration(frame, address, resources, inputs, 0),
            0x7DE110 => Decoration(frame, address, resources, inputs, 1),
            0x7DE200 => Decoration(frame, address, resources, inputs, 2),
            0x7DE540 => Results(frame, address, resources, inputs),
            0x7DE830 => RestoreCamera(frame, address, inputs),
            _ => throw new NotSupportedException($"Effect68 lower callback {callback:X8} is unsupported.")
        };
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Write(frame, address, task);
        if ((task.ReadByte(0x26) & 1) == 0 || task.ReadByte(0x28) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Complete8DC530(address);
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, 2);
    }

    public static NativeBattleEffect68Frame Invoke(NativeBattleEffect68Frame frame, uint function, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        return function switch
        {
            0x7DD9B0 => InitializeCamera(frame, address, resources, inputs),
            0x7DDC60 => AdvanceCamera(frame, address, inputs),
            0x7DDDC0 => PublishCamera(frame, inputs),
            0x7DDE90 => DelayedCamera(frame, address, inputs),
            0x7DE0C0 => DelayedResults(frame, address, inputs),
            0x7DE710 => ActiveCamera(frame, address, resources, inputs),
            0x7DE7A0 => ExitCamera(frame, address, inputs),
            0x7DE910 => Read(frame, address).ReadByte(0x28) == 0 ? Write(frame, address, Done(Read(frame, address))) : frame,
            0x7DE930 => frame,
            _ => throw new NotSupportedException($"Effect68 lower function {function:X8} is unsupported.")
        };
    }

    public static NativeBattleEffect68Frame Select7DC470(NativeBattleEffect68Frame frame, short actor, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        ImmutableArray<NativeBattleScanByteOwner> models = inputs.ReadModels(frame).Select(row => ByteOwner(row, 0x9C)).ToImmutableArray();
        NativeBattleScanModelSelectionResult selected = NativeBattleScanModelSelection.Select84D320(models, actor, _ => throw new NotSupportedException("Effect68 requires the current resource sequence."), index => inputs.ReadResourceSequence(frame, index));
        frame = PutDword(frame, 0x25FFAA8, selected.SelectedPhysicalModel);
        for (int index = 0; index < 7; index++)
        {
            frame = PutDword(frame, 0x25FCE34 + (uint)(index * 4), selected.SelectionFlags[index]);
        }
        return frame;
    }

    internal static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static int Phase(NativeBattleModulePoolTask task, int count)
    {
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if ((uint)phase >= count)
        {
            throw new InvalidDataException("Effect68 signed task phase leaves its selected table.");
        }
        return phase;
    }

    internal static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    internal static NativeBattleModulePoolTask Done(NativeBattleModulePoolTask task) => Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
    internal static NativeBattleModulePoolTask Scale(NativeBattleModulePoolTask task, int scale) => task.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
    internal static NativeBattleModulePoolTask Angle(NativeBattleModulePoolTask task, int offset, int delta) => task.WriteWord(offset, (ushort)((task.ReadWord(offset) + delta) & 0xFFF));
    internal static ushort W(NativeBattleEffect68Frame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
    internal static uint D(NativeBattleEffect68Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4));
    internal static NativeBattleEffect68Frame PutWord(NativeBattleEffect68Frame frame, uint address, int value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)value))), 2);
    internal static NativeBattleEffect68Frame PutDword(NativeBattleEffect68Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    internal static short Table(NativeBattleEffect68Resources resources, uint address) => BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(address, 2));
    internal static NativeBattleScanByteOwner ByteOwner(NativeBattleEffectArena arena, int length)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, length);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    internal static NativeBattleEffectArena Arena(NativeBattleScanByteOwner owner)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, owner.Bytes.AsSpan());
        for (int index = 0; index < owner.Bytes.Length; index++)
        {
            if (!owner.Known[index])
            {
                arena = arena.MarkUnknown(index, 1);
            }
        }
        return arena;
    }

    internal static (NativeBattleEffect68Frame Frame, uint Address) Child(NativeBattleEffect68Frame frame, uint descriptor, uint callback, ushort size, uint parent, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Effect68 requires its current action parameters.");
        int pool = Enumerable.Range(0, frame.Effect.Pools.Length).Single(index => frame.Effect.Pools[index].DescriptorAddress == descriptor);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, size, parent, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, inputs.ReadPackets(frame), pointer, group, target));
        return (frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }
}
