using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ProgrammeServices(Func<NativeBattleSiren95Frame, uint, int, byte[]> ReadAddress, Func<NativeBattleSiren95Frame, NativeBattleMoogleCameraStep, NativeBattleSiren95Frame> PublishCamera);

public static class NativeBattleSiren95Programme
{
    public static NativeBattleSiren95TaskStep Advance73A9E0(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95ProgrammeServices services)
    {
        NativeBattleModulePoolTask task = ReadTask(frame, address);
        if (task.Callback != 0x73A9E0)
        {
            throw new InvalidDataException("Native Siren programme requires its actual callback task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (Active(1))
                {
                    frame = Put(frame, Camera(frame), new byte[0xEC]);
                    Initialize(0x1533C40, 8);
                    Next();
                }
                break;
            case 1:
                if (Advance())
                {
                    Next();
                }
                break;
            case 2:
                if (Active(2))
                {
                    Initialize(0x1533C88, 8);
                    Next();
                }
                break;
            case 3:
                Advance();
                if (Active(3))
                {
                    Initialize(0x1533CD0, 0);
                    Next();
                }
                break;
            case 4:
                Advance();
                if (Active(4))
                {
                    Initialize(0x1533D18, 0);
                    Next();
                }
                break;
            case 5:
                if (Advance())
                {
                    frame = NativeBattleCameraChannels.InitializeFour(frame, Camera(frame), 0x1533C30, Channels(services));
                    Store(ReadTask(frame, address).WriteWord(0xDC, 104));
                    Next();
                }
                break;
            case 6:
                frame = NativeBattleCameraChannels.TickFour(frame, Camera(frame), Channels(services));
                NativeBattleMoogleCameraStep curves = NativeBattleMoogleCamera.RecomputePublication(ReadCamera(frame));
                frame = services.PublishCamera(StoreCamera(frame, curves.State), curves);
                Countdown();
                break;
            case 7:
                Request(5);
                break;
            case 8:
                if (Active(5))
                {
                    Initialize(0x1533D60, 0);
                    Store(ReadTask(frame, address).WriteWord(0xDC, 32));
                    Next();
                }
                break;
            case 9:
                Advance();
                Countdown();
                break;
            case 10:
                Advance();
                Request(6);
                break;
            case 11:
                Advance();
                if (Active(6))
                {
                    Initialize(0x1533DA8, 8);
                    Next();
                }
                break;
            case 12:
                Advance();
                if (Active(8))
                {
                    Next();
                }
                break;
            case 13:
                Store(ReadTask(frame, address).WriteByte(0x26, (byte)(ReadTask(frame, address).ReadByte(0x26) | 1)));
                Next();
                break;
            case 14:
                break;
            default:
                throw new NotSupportedException($"Native Siren signed programme phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = ReadTask(frame, address);
        Store(task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        task = ReadTask(frame, address);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Store(NativeBattleModulePoolTask value) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, value).Pools } };
        void Next() => Store(ReadTask(frame, address).WriteByte(0x29, unchecked((byte)(ReadTask(frame, address).ReadByte(0x29) + 1))));
        bool Active(short phase) => unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + 0x42))) >= phase;
        void Request(short phase)
        {
            if (NativeBattleSiren95Director.Request(frame, phase, out NativeBattleSiren95Frame requested))
            {
                frame = requested;
                Next();
            }
        }
        void Countdown()
        {
            ushort remaining = unchecked((ushort)(ReadTask(frame, address).ReadWord(0xDC) - 1));
            Store(ReadTask(frame, address).WriteWord(0xDC, remaining));
            if (unchecked((short)remaining) <= 0)
            {
                Next();
            }
        }
        bool Advance()
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(ReadCamera(frame));
            frame = StoreCamera(frame, step.State);
            frame = services.PublishCamera(frame, step);
            return step.Complete;
        }
        void Initialize(uint source, uint anchorOffset)
        {
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(ReadCamera(frame), new(source, Read(frame, source, 0x48, services).ToImmutableArray()), State(frame).Read(unchecked(Director(frame) + anchorOffset), 8).ToImmutableArray(), 0);
            frame = StoreCamera(frame, step.State);
            frame = services.PublishCamera(frame, step);
        }
    }

    private static NativeBattleCameraChannelServices<NativeBattleSiren95Frame> Channels(NativeBattleSiren95ProgrammeServices services) => new((current, address, length) => address >= NativeBattleSiren95State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleSiren95State.GlobalAddress + NativeBattleSiren95State.GlobalLength ? State(current).Read(address, length) : Read(current, address, length, services), (current, address, bytes) => Put(current, address, bytes.Span));

    private static NativeBattleMoogleCameraState ReadCamera(NativeBattleSiren95Frame frame)
    {
        uint address = Camera(frame);
        NativeBattleSiren95State state = State(frame);
        int padding = checked((int)(address - NativeBattleSiren95State.GlobalAddress + 0x5A));
        bool known = !state.Globals.EnumerateUnknownRanges().Any(region => region.Offset < padding + 2 && region.Offset + region.Length > padding);
        byte[] data = new byte[0xEC];
        state.Read(address, 0x5A).CopyTo(data, 0);
        state.Read(unchecked(address + 0x5C), 0x90).CopyTo(data, 0x5C);
        if (known)
        {
            state.Read(unchecked(address + 0x5A), 2).CopyTo(data, 0x5A);
        }
        return new(data.ToImmutableArray(), known);
    }

    private static NativeBattleSiren95Frame StoreCamera(NativeBattleSiren95Frame frame, NativeBattleMoogleCameraState camera)
    {
        uint address = Camera(frame);
        frame = Put(frame, address, camera.Data.AsSpan());
        if (!camera.EyePaddingKnown)
        {
            NativeBattleSiren95State state = State(frame);
            frame = frame with { Memory = frame.Memory with { Siren95 = state with { Globals = state.Globals.MarkUnknown(checked((int)(address - NativeBattleSiren95State.GlobalAddress + 0x5A)), 2) } } };
        }
        return frame;
    }

    private static NativeBattleModulePoolTask ReadTask(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren programme process owner is missing.");
    private static uint Camera(NativeBattleSiren95Frame frame) => State(frame).CameraAddress15339D0 ?? throw new NotSupportedException("Native Siren current camera pointer is unknown.");
    private static uint Director(NativeBattleSiren95Frame frame) => State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren current director pointer is unknown.");
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Siren95 = State(frame).Write(address, bytes) } };
    private static NativeBattleSiren95Frame PutWord(NativeBattleSiren95Frame frame, uint address, ushort value) => Put(frame, address, BitConverter.GetBytes(value));
    private static byte[] Read(NativeBattleSiren95Frame frame, uint address, int length, NativeBattleSiren95ProgrammeServices services)
    {
        byte[] bytes = services.ReadAddress(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native Siren source read lost its exact addressed extent.");
    }
}
