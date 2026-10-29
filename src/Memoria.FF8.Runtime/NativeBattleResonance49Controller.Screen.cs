using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    public static NativeBattleResonance49Frame InitializeScreen824300(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteView(0x26466B0, NativeBattleEffectArena.Clear().Write(0, new byte[16]), 16) };
        frame = DrawRandom(frame, out ushort first);
        frame = frame with { State = frame.State.WriteWord(0x26466B6, (ushort)((first & 0x3F) + 32)) };
        frame = DrawRandom(frame, out ushort second);
        frame = frame with { State = frame.State.WriteWord(0x26466B8, (ushort)(second & 0xFFF)) };
        frame = DrawRandom(frame, out ushort third);
        frame = frame with { State = frame.State.WriteWord(0x26466BC, (ushort)(third & 0xFFF)) };
        for (int row = 0; row < 11; row++)
        {
            byte u0 = unchecked((byte)(row * 32));
            byte u1 = unchecked((byte)(u0 + 32));
            if (u1 == 0)
            {
                u1 = 255;
            }
            for (int column = 0; column < 8; column++)
            {
                byte v0 = unchecked((byte)(column * 32));
                byte v1 = unchecked((byte)(v0 + 32));
                byte[] uv = [u0, v0, u1, v0, u0, v1, u1, v1];
                frame = frame with { State = frame.State.WriteView(checked(0x2647904 + (uint)((row * 8 + column) * 12)), NativeBattleEffectArena.Clear().Write(0, uv), 8) };
            }
        }
        return frame;
    }

    public static NativeBattleResonance49Frame DrawScreen8244E0(NativeBattleResonance49Frame frame, uint address, int bucket, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        frame = DeformScreen8246D0(frame, address);
        for (int channel = 1; channel >= 0; channel--)
        {
            frame = PositionScreen824520(frame, channel);
            for (int row = 0; row < 10; row++)
            {
                ushort page = row < 8 ? (ushort)0x119 : (ushort)0x11D;
                for (int column = 0; column < 7; column++)
                {
                    uint cell = checked(0x2647900 + (uint)((row * 8 + column) * 12));
                    byte[] quad = new byte[40];
                    NativeBattleGfPacketByteState[] quadMask = new NativeBattleGfPacketByteState[40];
                    Put(0, BitConverter.GetBytes(0x09000000u));
                    Put(7, new[] { channel == 1 ? (byte)0x2F : (byte)0x2D });
                    Put(8, frame.State.ReadView(cell, 4).Read(0, 4));
                    Put(16, frame.State.ReadView(cell + 96, 4).Read(0, 4));
                    Put(24, frame.State.ReadView(cell + 12, 4).Read(0, 4));
                    Put(32, frame.State.ReadView(cell + 108, 4).Read(0, 4));
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Put(12 + corner * 8, frame.State.ReadView(cell + 4 + (uint)(corner * 2), 2).Read(0, 2));
                    }
                    Put(22, BitConverter.GetBytes(page));
                    uint[] quadWords = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(quad.AsSpan(index * 4))).ToArray();
                    NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(49, Read(frame, address), channel * 70 + row * 7 + column);
                    System.Numerics.Vector3 Corner(int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(offset)), BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(offset + 2)), 0);
                    // ScreenPair submits full-coordinate packets; presentation sampling first converts eighth units.
                    NativeBattleGeometryPrimitive primitive = new(bucket, quadWords.ToImmutableArray(), []) { PilotGeometry = new(identity, Corner(8), Corner(16), Corner(24), Corner(32), 4) { ScreenSpace = true, ScreenScale = 8 } };
                    NativeBattleGfGeometryAttempt polygon = new(0, quad.ToImmutableArray(), quadMask.ToImmutableArray(), primitive);
                    byte[] mode = new byte[12];
                    mode[3] = 2;
                    BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000020);
                    NativeBattleGfPacketByteState[] modeMask = new NativeBattleGfPacketByteState[12];
                    modeMask.AsSpan(3).Fill(NativeBattleGfPacketByteState.Known);
                    NativeBattleGfGeometryAttempt drawingMode = new(40, mode.ToImmutableArray(), modeMask.ToImmutableArray(), new(bucket, [0x02000000, 0xE1000020, 0], [0, 0, 0, 0]));
                    frame = inputs.Execute(frame, new NativeBattleResonance49DeliverScreenPair(polygon, drawingMode));

                    void Put(int offset, byte[] value)
                    {
                        value.CopyTo(quad, offset);
                        quadMask.AsSpan(offset, value.Length).Fill(NativeBattleGfPacketByteState.Known);
                    }
                }
            }
        }
        return frame;
    }

    public static NativeBattleResonance49Frame PublishEnvironment824410(NativeBattleResonance49Frame frame, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        int parity = (inputs.CurrentPrimitiveBufferByte(frame) - 1) & 1;
        NativeBattleEffectArena original = inputs.ReadEnvironment92(frame, parity);
        uint beforeAddress = checked(0x2646700 + (uint)(parity * 92));
        uint captureAddress = checked(0x2647760 + (uint)(parity * 92));
        NativeBattleEffectArena before = NativeBattleEffectNodeContextMemory.Copy(original, 0, NativeBattleEffectArena.Clear(), 0, 92).Write(0x18, new byte[1]);
        NativeBattleEffectArena capture = NativeBattleEffectNodeContextMemory.Copy(original, 0, NativeBattleEffectArena.Clear(), 0, 92).Write(0, BitConverter.GetBytes((short)576)).Write(2, BitConverter.GetBytes((short)256)).Write(4, BitConverter.GetBytes((short)320)).Write(6, BitConverter.GetBytes((short)240)).Write(8, BitConverter.GetBytes((short)576)).Write(10, BitConverter.GetBytes((short)256)).Write(0x18, new byte[] { 1, 0, 0, 0 });
        frame = frame with { State = frame.State.WriteView(beforeAddress, before, 92).WriteView(captureAddress, capture, 92) };
        uint capturePacket = checked(0x26472D8 + (uint)(parity * 64));
        uint restorePacket = checked(0x2647820 + (uint)(parity * 64));
        frame = inputs.Execute(frame, new NativeBattleResonance49StorePacketBatch(Environment49(frame.State.ReadView(capturePacket, 64), capture, 4385), capturePacket));
        return inputs.Execute(frame, new NativeBattleResonance49StorePacketBatch(Environment49(frame.State.ReadView(restorePacket, 64), before, 5), restorePacket));
    }

    private static NativeBattleGfGeometryBatch Environment49(NativeBattleEffectArena retained, NativeBattleEffectArena environment, int bucket)
    {
        bool clear = environment.Read(24, 1)[0] != 0;
        environment.Read(0, clear ? 28 : 25);
        (byte[] source, _) = environment.ReadSnapshot(0, 92);
        (byte[] previous, _) = retained.ReadSnapshot(0, 64);
        ImmutableArray<byte> produced = NativeBattleDrawEnvironmentPackets.Restore45C0F0(previous.ToImmutableArray(), source.ToImmutableArray());
        int written = clear ? 36 : 24;
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[64];
        mask.AsSpan(3, written - 3).Fill(NativeBattleGfPacketByteState.Known);
        ImmutableArray<uint> words = Enumerable.Range(0, written / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(produced.AsSpan().Slice(index * 4))).ToImmutableArray();
        return new([new(0, produced, mask.ToImmutableArray(), new(bucket, words, [0, 0, 0, 0]))], 64);
    }

    private static NativeBattleResonance49Frame DeformScreen8246D0(NativeBattleResonance49Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        foreach (int offset in new[] { 0x58, 0x5A, 0x5C, 0x68, 0x6A, 0x6C })
        {
            frame = DrawRandom(frame, out ushort random);
            task = task.WriteWord(offset, (ushort)((task.ReadWord(offset) + 256 + (random & 255)) & 4095));
        }
        frame = Write(frame, address, task);
        int amplitude = unchecked((short)frame.State.ReadWord(0x26466B0));
        for (int channel = 0; channel < 2; channel++)
        {
            int angle = task.ReadWord(0x58 + channel * 2);
            int step = task.ReadWord(0x60 + channel * 2);
            for (int point = 0; point < 8; point++)
            {
                int product = unchecked(NativeBattleCameraMath.Sine(unchecked((short)angle)) * amplitude);
                frame = frame with { State = frame.State.WriteWord(checked(0x26466E0 + (uint)(channel * 2 + point * 4)), unchecked((ushort)(product / 2097152))) };
                angle = (angle + step) & 4095;
            }
            angle = task.ReadWord(0x68 + channel * 2);
            step = task.ReadWord(0x70 + channel * 2);
            for (int point = 0; point < 11; point++)
            {
                int product = unchecked(NativeBattleCameraMath.Sine(unchecked((short)angle)) * amplitude);
                frame = frame with { State = frame.State.WriteWord(checked(0x26467B8 + (uint)(channel * 2 + point * 4)), unchecked((ushort)(product / 2097152))) };
                angle = (angle + step) & 4095;
            }
        }
        return frame;
    }

    private static NativeBattleResonance49Frame PositionScreen824520(NativeBattleResonance49Frame frame, int channel)
    {
        for (int row = 0; row < 11; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                short x = row == 0 ? (short)0 : row == 10 ? (short)320 : unchecked((short)(frame.State.ReadWord(checked(0x26466E0 + (uint)(channel * 2 + column * 4))) + row * 32));
                short y = column == 0 ? (short)0 : column == 7 ? (short)240 : unchecked((short)(frame.State.ReadWord(checked(0x26467B8 + (uint)(channel * 2 + row * 4))) + column * 32));
                uint cell = checked(0x2647900 + (uint)((row * 8 + column) * 12));
                frame = frame with { State = frame.State.WriteWord(cell, unchecked((ushort)x)).WriteWord(cell + 2, unchecked((ushort)y)) };
            }
        }
        return frame;
    }
}
