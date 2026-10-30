using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect64Particles
{
    public static (NativeBattleEffect64Frame Frame, uint Next) Birth6E68C0(NativeBattleEffect64Frame frame, uint script, uint model, uint offsets, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        short[] words = Enumerable.Range(0, 24).Select(index => NativeBattleEffect64Script.ReadWord(frame, script + (uint)(index * 2))).ToArray();
        if (words[0] != 0)
        {
            throw new NotSupportedException("Effect64 particle mutation requires its actual addressed model-field consumer.");
        }
        frame = inputs.Execute(frame, new NativeBattleEffect64ApplyOffsetPose662C00(model, offsets));
        NativeBattleEffect64PointStep bone = inputs.ReadBone502170(frame, model, words[1], words[2], NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = bone.Frame;
        NativeBattleEffectArena origin = NativeBattleEffect64Programme.AddOffset(frame, bone.Point8, offsets, words[1]);
        for (int index = 0; index < words[5]; index++)
        {
            (frame, uint address) = Append(frame, 0x2545028, 0x6E67F0);
            if (address == 0)
            {
                break;
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            for (int axis = 0; axis < 3; axis++)
            {
                (frame, int random) = Random6E6B30(frame, words[6 + axis * 2], words[7 + axis * 2]);
                short position = BinaryPrimitives.ReadInt16LittleEndian(origin.Read(axis * 2, 2));
                task = task.WriteWord(0xC + axis * 2, unchecked((ushort)(position + random)));
            }
            task = task.WriteWord(0x12, unchecked((ushort)words[4])).WriteByte(0x14, 0).WriteByte(0x15, unchecked((byte)words[3]));
            for (int lane = 0; lane < 6; lane++)
            {
                (frame, int random) = Random6E6B30(frame, words[12 + lane * 2], words[13 + lane * 2]);
                task = task.WriteByte(0x16 + lane, unchecked((byte)random));
            }
            frame = Write(frame, address, task);
        }
        return (frame, script + 48);
    }

    public static (NativeBattleEffect64Frame Frame, uint Next) Birth6E6710(NativeBattleEffect64Frame frame, uint script, uint model, uint offsets, NativeBattleEffect64Inputs inputs)
    {
        short flag = NativeBattleEffect64Script.ReadWord(frame, script);
        if (flag != 0)
        {
            throw new NotSupportedException("Effect64 direct particle mutation requires its actual addressed model-field consumer.");
        }
        (frame, uint address) = Append(frame, 0x2545028, 0x6E67F0);
        if (address == 0)
        {
            return (frame, script + 22);
        }
        short[] words = Enumerable.Range(0, 10).Select(index => NativeBattleEffect64Script.ReadWord(frame, script + (uint)(index * 2))).ToArray();
        frame = inputs.Execute(frame, new NativeBattleEffect64ApplyOffsetPose662C00(model, offsets));
        NativeBattleEffect64PointStep bone = inputs.ReadBone502170(frame, model, words[1], words[2], NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = bone.Frame;
        NativeBattleEffectArena point = NativeBattleEffect64Programme.AddOffset(frame, bone.Point8, offsets, words[1]);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 0xC, point, 6).WriteWord(0x12, unchecked((ushort)words[3])).WriteByte(0x14, 0).WriteByte(0x15, unchecked((byte)words[2]));
        for (int lane = 0; lane < 6; lane++)
        {
            task = task.WriteByte(0x16 + lane, unchecked((byte)words[4 + lane]));
        }
        return (Write(frame, address, task), script + 20);
    }

    public static (NativeBattleEffect64Frame Frame, int Value) Random6E6B30(NativeBattleEffect64Frame frame, int range, int basis)
    {
        if (range == 0)
        {
            return (frame, basis);
        }
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        return (frame with { Random = random }, unchecked(basis + (unchecked(range * value) >> 15)));
    }

    public static NativeBattleEffect64Step Advance6E67F0(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short scale = unchecked((short)task.ReadWord(0x12));
        NativeBattleVector position = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, scale, scale, -1024) };
        (frame, uint temporary) = Allocate(frame, 180);
        int sprite = task.ReadByte(0x15);
        if ((uint)sprite >= 2)
        {
            throw new InvalidDataException("Effect64 particle leaves its selected sprite directory.");
        }
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0x13433C0u + (uint)(sprite * 4), 4).AsSpan());
        NativeBattleModuleResource source = resources.Sprite(sourceAddress);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(sourceAddress)).Write(4, BitConverter.GetBytes((ushort)task.ReadByte(0x14))).Write(0x24, new byte[2]);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, pointer => pointer == sourceAddress ? source : throw new InvalidDataException("Effect64 sprite source changed during its draw."), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, allowCallerScale: true, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(64, task) with { ResourceGeneration = sourceAddress });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect64DeliverBatch(drawn.Batch, 0x254508C));
        frame = Release(frame, 180);
        task = Read(frame, address);
        sbyte vx = unchecked((sbyte)task.ReadByte(0x16));
        sbyte vy = unchecked((sbyte)task.ReadByte(0x17));
        sbyte vz = unchecked((sbyte)task.ReadByte(0x18));
        task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + (vx << 2)))).WriteWord(0xE, unchecked((ushort)(task.ReadWord(0xE) + (vy << 2)))).WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + (vz << 2))));
        task = task.WriteByte(0x16, unchecked((byte)(vx + task.ReadByte(0x19)))).WriteByte(0x17, unchecked((byte)(vy + (task.ReadByte(0x1A) << 1))));
        byte next = unchecked((byte)(task.ReadByte(0x14) + 1));
        frame = Write(frame, address, task.WriteByte(0x14, next));
        return new(frame, next < BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan().Slice(8, 2)) ? 0 : 2);
    }

    public static (NativeBattleEffect64Frame Frame, uint Next) Overlay6E6B60(NativeBattleEffect64Frame frame, uint script, NativeBattleEffect64Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x05000000);
        for (int lane = 0; lane < 3; lane++)
        {
            bytes[4 + lane] = unchecked((byte)NativeBattleEffect64Script.ReadWord(frame, script + (uint)(lane * 2)));
        }
        bytes[7] = 0x2A;
        foreach (int offset in new[] { 0xC, 0x14 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 320);
        }
        foreach (int offset in new[] { 0x12, 0x16 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 216);
        }
        ImmutableArray<uint> words = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]);
        if (pilotIdentity is { } identity)
        {
            primitive = primitive with { PilotGeometry = new(identity, new(0, 0, 0), new(320, 0, 0), new(0, 216, 0), new(320, 216, 0), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
        }
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray(), primitive);
        return (inputs.Execute(frame, new NativeBattleEffect64DeliverBatch(new([attempt], 24), 0x254508C)), script + 6);
    }
}
