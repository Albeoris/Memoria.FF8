using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90ProgrammeServices(Func<NativeBattleTonberry90Frame, uint, int, byte[]> ReadAddress, Func<NativeBattleTonberry90Frame, NativeBattleMoogleCameraStep, NativeBattleTonberry90Frame> PublishCamera, Func<NativeBattleTonberry90Frame, uint, int, int, NativeBattleTonberry90Frame> PlaySound501330);

public static class NativeBattleTonberry90Programme
{
    public static NativeBattleTonberry90TaskStep Advance762DA0(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90ProgrammeServices services)
    {
        NativeBattleModulePoolTask task = ReadTask(frame, address);
        if (task.Callback != 0x762DA0)
        {
            throw new InvalidDataException("Native Tonberry programme requires its actual callback task.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = Put(frame, Camera(frame), new byte[0xEC]);
                Next();
                break;
            case 1:
                if (Active(2))
                {
                    Initialize(0x154E9E8, 0);
                    Next();
                }
                break;
            case 2:
                if (Advance())
                {
                    frame = InitializeChannels7636C0(frame, 0x154E9D8, services);
                    Store(ReadTask(frame, address).WriteWord(0x74, 99));
                    Next();
                }
                break;
            case 3:
                frame = TickChannels7637B0(frame, services);
                ushort remaining = unchecked((ushort)(ReadTask(frame, address).ReadWord(0x74) - 1));
                Store(ReadTask(frame, address).WriteWord(0x74, remaining));
                if (unchecked((short)remaining) <= 0)
                {
                    Next();
                }
                break;
            case 4:
                Request(3);
                break;
            case 5:
                Advance();
                if (Active(3))
                {
                    frame = services.PlaySound501330(frame, 0x1547170, 0, 128);
                    Initialize(0x154EAC0, 0);
                    Next();
                }
                break;
            case 6:
            case 10:
                if (Advance())
                {
                    Next();
                }
                break;
            case 7:
                Advance();
                Request(4);
                break;
            case 8:
                Advance();
                if (Active(4))
                {
                    Initialize(0x154EB08, 0);
                    Next();
                }
                break;
            case 9:
                Advance();
                if (Active(6))
                {
                    Initialize(DirectorWord(0x4E) < 0x1000 ? 0x154EB50u : 0x154EB98u, 8);
                    Next();
                }
                break;
            case 11:
                Advance();
                Request(7);
                break;
            case 12:
                Advance();
                if (Active(7))
                {
                    Initialize(DirectorWord(0x4E) < 0x1000 ? 0x154EBE0u : 0x154EC28u, 8);
                    Next();
                }
                break;
            case 13:
                Advance();
                if (Active(9))
                {
                    Next();
                }
                break;
            case 14:
                Store(ReadTask(frame, address).WriteByte(0x26, (byte)(ReadTask(frame, address).ReadByte(0x26) | 1)));
                Next();
                break;
            case 15:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry signed programme phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = ReadTask(frame, address);
        Store(task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        task = ReadTask(frame, address);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Store(NativeBattleModulePoolTask value) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, value).Pools } };
        void Next() => Store(ReadTask(frame, address).WriteByte(0x29, unchecked((byte)(ReadTask(frame, address).ReadByte(0x29) + 1))));
        short DirectorWord(int offset) => unchecked((short)State(frame).ReadWord(unchecked(Director(frame) + (uint)offset)));
        bool Active(short phase) => DirectorWord(0x42) >= phase;
        void Request(short phase)
        {
            var requested = RequestPhase763940(frame, phase);
            frame = requested.Frame;
            if (requested.Accepted)
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

    public static (NativeBattleTonberry90Frame Frame, bool Accepted) RequestPhase763940(NativeBattleTonberry90Frame frame, short phase)
    {
        uint director = Director(frame);
        NativeBattleTonberry90State state = State(frame);
        ushort spawned = state.ReadWord(unchecked(director + 0x40));
        if (state.ReadWord(unchecked(director + 0x44)) != spawned || unchecked((short)spawned) != (int)phase - 1)
        {
            return (frame, false);
        }
        return (PutWord(frame, unchecked(director + 0x44), unchecked((ushort)phase)), true);
    }

    public static NativeBattleTonberry90Frame InitializeChannels7636C0(NativeBattleTonberry90Frame frame, uint source, NativeBattleTonberry90ProgrammeServices services)
    {
        return NativeBattleCameraChannels.InitializeFour(frame, Camera(frame), source, Channels(services));
    }

    public static NativeBattleTonberry90Frame TickChannels7637B0(NativeBattleTonberry90Frame frame, NativeBattleTonberry90ProgrammeServices services)
    {
        frame = NativeBattleCameraChannels.TickFour(frame, Camera(frame), Channels(services));
        NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.RecomputePublication(ReadCamera(frame));
        return services.PublishCamera(StoreCamera(frame, step.State), step);
    }

    public static NativeBattleTonberry90Frame InitializeChannel763740(NativeBattleTonberry90Frame frame, uint channel, uint output, NativeBattleTonberry90ProgrammeServices services)
    {
        return NativeBattleCameraChannels.Initialize(frame, channel, output, Channels(services));
    }

    public static (NativeBattleTonberry90Frame Frame, bool Complete) TickChannel763820(NativeBattleTonberry90Frame frame, uint channel, uint output, NativeBattleTonberry90ProgrammeServices services)
    {
        return NativeBattleCameraChannels.Tick(frame, Camera(frame), channel, output, Channels(services));
    }

    public static (NativeBattleTonberry90Frame Frame, bool Complete) StepChannel763870(NativeBattleTonberry90Frame frame, uint channel, uint output, NativeBattleTonberry90ProgrammeServices services)
    {
        return NativeBattleCameraChannels.Step(frame, Camera(frame), channel, output, Channels(services));
    }

    private static NativeBattleCameraChannelServices<NativeBattleTonberry90Frame> Channels(NativeBattleTonberry90ProgrammeServices services) => new((current, address, length) => address >= NativeBattleTonberry90State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleTonberry90State.GlobalAddress + NativeBattleTonberry90State.GlobalLength ? State(current).Read(address, length) : Read(current, address, length, services), (current, address, bytes) => Put(current, address, bytes.Span));

    private static NativeBattleMoogleCameraState ReadCamera(NativeBattleTonberry90Frame frame)
    {
        uint address = Camera(frame);
        NativeBattleTonberry90State state = State(frame);
        int padding = checked((int)(address - NativeBattleTonberry90State.GlobalAddress + 0x5A));
        bool known = !state.Globals.UnknownRegions.Any(region => region.Offset < padding + 2 && region.Offset + region.Length > padding);
        byte[] data = new byte[0xEC];
        state.Read(address, 0x5A).CopyTo(data, 0);
        state.Read(unchecked(address + 0x5C), 0x90).CopyTo(data, 0x5C);
        if (known)
        {
            state.Read(unchecked(address + 0x5A), 2).CopyTo(data, 0x5A);
        }
        return new(data.ToImmutableArray(), known);
    }

    private static NativeBattleTonberry90Frame StoreCamera(NativeBattleTonberry90Frame frame, NativeBattleMoogleCameraState camera)
    {
        uint address = Camera(frame);
        frame = Put(frame, address, camera.Data.AsSpan());
        if (!camera.EyePaddingKnown)
        {
            NativeBattleTonberry90State state = State(frame);
            frame = frame with { Memory = frame.Memory with { Tonberry90 = state with { Globals = state.Globals.MarkUnknown(checked((int)(address - NativeBattleTonberry90State.GlobalAddress + 0x5A)), 2) } } };
        }
        return frame;
    }

    private static NativeBattleModulePoolTask ReadTask(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90State State(NativeBattleTonberry90Frame frame) => frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry programme process owner is missing.");
    private static uint Camera(NativeBattleTonberry90Frame frame) => State(frame).CameraAddress1547510 ?? throw new NotSupportedException("Native Tonberry current camera pointer is unknown.");
    private static uint Director(NativeBattleTonberry90Frame frame) => State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry current director pointer is unknown.");
    private static NativeBattleTonberry90Frame Put(NativeBattleTonberry90Frame frame, uint address, ReadOnlySpan<byte> bytes) => frame with { Memory = frame.Memory with { Tonberry90 = State(frame).Write(address, bytes) } };
    private static NativeBattleTonberry90Frame PutWord(NativeBattleTonberry90Frame frame, uint address, ushort value) => Put(frame, address, BitConverter.GetBytes(value));
    private static byte[] Read(NativeBattleTonberry90Frame frame, uint address, int length, NativeBattleTonberry90ProgrammeServices services)
    {
        byte[] bytes = services.ReadAddress(frame, address, length);
        return bytes is not null && bytes.Length == length ? bytes : throw new InvalidDataException("Native Tonberry source read lost its exact addressed extent.");
    }
}
