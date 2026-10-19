using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212PreparationServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, NativeBattleModuleAnchor> ReadAnchor571400, ImmutableArray<byte> Targets, Func<NativeBattleDuel212Frame, NativeBattleVector, NativeBattleThundaga105Bounds, NativeBattleDuel212Frame> Capture6A12B0, Func<NativeBattleDuel212Frame, int, uint, NativeBattleDuel212Frame> BindActor5022C0, Func<NativeBattleDuel212Frame, int, byte, NativeBattleDuel212Frame> SelectSequence505C00);

public static class NativeBattleDuel212Preparation
{
    public static NativeBattleDuel212Frame Initialize6A0DC0(NativeBattleDuel212Frame frame, int slot, NativeBattleDuel212PreparationServices services)
    {
        NativeBattleEffectParameters parameters = frame.Memory.Duel212?.Parameters ?? throw new NotSupportedException("Native Burning Rave preparation requires its entered action parameters.");
        NativeBattleDuel212Resources resources = frame.Memory.Duel212.Resources ?? throw new NotSupportedException("Native Burning Rave preparation requires its actual resources.");
        int actor = parameters.Actor78;
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel212Controller.Targets);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6A1660 || services.Targets.IsDefaultOrEmpty || services.Targets.Length > 7 || services.Targets.Any(target => target >= 7))
        {
            throw new InvalidDataException("Native Burning Rave preparation requires its actual scene and action contact rows.");
        }
        task = task with { Payload = new byte[3268].ToImmutableArray(), UnknownPayloadRanges = null, ResourcePointers = null };
        for (int index = 0; index < 7; index++)
        {
            frame = frame with { Staging = frame.Staging.Write(checked(0x25102A0u + (uint)index * 4), BitConverter.GetBytes((uint)services.ReadModel(index).Flags0)) };
        }
        int minX = short.MaxValue;
        int minY = short.MaxValue;
        int minZ = short.MaxValue;
        int maxX = -32767;
        int maxY = -32767;
        int maxZ = -32767;
        for (int index = 0; index < services.Targets.Length; index++)
        {
            int target = services.Targets[index];
            NativeBattleGraphicsModel model = services.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Burning Rave preparation requires its actual target position.");
            int offset = 64 + index * 24;
            task = task.WriteDword(offset, checked(0x1D972C0u + (uint)target * 156));
            task = Position(task, offset + 4, transform);
            task = task.WriteBytes(offset + 12, frame.Staging.Read(0x2510270, 8));
            NativeBattleModuleAnchor point = services.ReadAnchor571400(target);
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }
        short centerX = unchecked((short)((minX + maxX) / 2));
        short centerY = unchecked((short)((minY + maxY) / 2));
        NativeBattleGraphicsModel caster = services.ReadModel(actor);
        NativeBattleModelTransform casterTransform = caster.Transform ?? throw new NotSupportedException("Native Burning Rave preparation requires its actual caster position.");
        task = task.WriteWord(304, unchecked((ushort)centerX)).WriteWord(306, unchecked((ushort)centerY)).WriteWord(308, unchecked((ushort)maxZ));
        task = task.WriteWord(320, unchecked((ushort)centerX)).WriteWord(322, 0).WriteWord(324, unchecked((ushort)(casterTransform.Position28.Z - 5160)));
        task = task.WriteDword(328, unchecked((ushort)centerX)).WriteWord(332, unchecked((ushort)(maxZ + 4300)));
        Store();
        NativeBattleThundaga105Bounds first = Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA18)));
        NativeBattleThundaga105Bounds second = Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA1C)));
        first = first with { Maximum = first.Maximum with { X = second.Maximum.X } };
        frame = services.Capture6A12B0(frame, new(centerX, 0, unchecked((short)(casterTransform.Position28.Z - 5160))), first);
        Refresh();
        task = Position(task, 48, casterTransform);
        task = task.WriteWord(56, unchecked((ushort)(caster.Rotation12 ?? throw new NotSupportedException("Native Burning Rave requires actual caster rotationX.")))).WriteWord(58, unchecked((ushort)(caster.Rotation14 ?? throw new NotSupportedException("Native Burning Rave requires actual caster rotationY.")))).WriteWord(60, unchecked((ushort)(caster.Rotation16 ?? throw new NotSupportedException("Native Burning Rave requires actual caster rotationZ."))));
        task = caster.RotationPadding18 is ushort padding ? task.WriteWord(62, padding) : task.MarkPayloadUnknown(62, 2);
        casterTransform = casterTransform with { Position28 = casterTransform.Position28 with { X = centerX } };
        services.WriteModel(actor, caster with { Transform = casterTransform, Rotation14 = 0 });
        task = NativeBattleDuelEcho.Initialize6A1230(task, 336, [14, 18, 22]);
        frame = frame with { Staging = frame.Staging.Write(0x2510260, BitConverter.GetBytes(Pack(casterTransform.Position28.X, casterTransform.Position28.Y))).Write(0x2510264, BitConverter.GetBytes(Pack(casterTransform.Position28.Z, casterTransform.Vertical34))).Write(0x2510268, BitConverter.GetBytes(Pack(centerX, 0))).Write(0x251026C, BitConverter.GetBytes(unchecked((short)maxZ))) };
        Store();
        frame = services.BindActor5022C0(frame, actor, checked(pool.BankAddress + (uint)(slot * pool.Stride) + 16));
        frame = services.SelectSequence505C00(frame, actor, 1);
        return frame with { Memory = NativeBattleEffectIo.Initialize534110(frame.Memory, 7120) };

        void Store()
        {
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        }
        void Refresh()
        {
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel212Controller.Targets);
            task = pool.Slots[slot];
        }
    }

    public static NativeBattleThundaga105Bounds Bounds69A700(NativeBattleDuel212Resources resources, uint address)
    {
        int count = checked((int)resources.Dword(address + 4));
        if (count < 2)
        {
            throw new InvalidDataException("Native69A700 requires its original multi-vertex geometry.");
        }
        ImmutableArray<byte> vertices = resources.Read(address + 12, checked(count * 8));
        NativeBattleVector minimum = Point(0);
        NativeBattleVector maximum = minimum;
        for (int index = 1; index < count; index++)
        {
            NativeBattleVector point = Point(index);
            minimum = new(Math.Min(minimum.X, point.X), Math.Min(minimum.Y, point.Y), Math.Min(minimum.Z, point.Z));
            maximum = new(Math.Max(maximum.X, point.X), Math.Max(maximum.Y, point.Y), Math.Max(maximum.Z, point.Z));
        }
        return new(minimum, maximum);

        NativeBattleVector Point(int index) => new(Word(index * 8), Word(index * 8 + 2), Word(index * 8 + 4));
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(offset, 2));
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleModelTransform transform) => task.WriteDword(offset, Pack(transform.Position28.X, transform.Position28.Y)).WriteDword(offset + 4, Pack(transform.Position28.Z, transform.Vertical34));
    private static uint Pack(short first, short second) => unchecked((ushort)first) | unchecked((uint)(ushort)second << 16);
}
