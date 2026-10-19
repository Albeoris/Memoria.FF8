using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources, ImmutableArray<NativeBattleMoogleCameraCurve> Curves)
{
    public static ImmutableArray<uint> Addresses { get; } = [0x15600F8, 0x1560140, 0x1560188, 0x15601D0, 0x1560218, 0x1560260, 0x15602E0, 0x1560328, 0x1560370, 0x15603B8, 0x1560400, 0x1560468, 0x15604F0];

    public void Validate()
    {
        if (Sources.IsDefault || Sources.Length != Addresses.Length || Curves.IsDefault || Curves.Length != 3)
        {
            throw new InvalidDataException("Native Rinoa87 camera requires its thirteen original sources and bounded scalar curves.");
        }
        for (int index = 0; index < Addresses.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != Addresses[index] || source.Data.IsDefault || source.Data.Length != 72 || BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(36)) != 0)
            {
                throw new InvalidDataException("Native7749C0 camera source contradicts its original address or extent.");
            }
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan());
            uint expected = index == 6 ? 0x15602A8u : index == 11 ? 0x1560448u : index == 12 ? 0x15604B0u : 0;
            if (pointer != expected)
            {
                throw new NotSupportedException("Native7749C0 requires the exact scalar curve referenced by its source.");
            }
        }
        uint[] curveAddresses = [0x15602A8, 0x1560448, 0x15604B0];
        int[] curveLengths = [28, 15, 32];
        for (int index = 0; index < Curves.Length; index++)
        {
            NativeBattleMoogleCameraCurve curve = Curves[index];
            if (curve.Address != curveAddresses[index] || curve.Values.IsDefault || curve.Values.Length != curveLengths[index] || curve.Values[^1] != 4096 || curve.Values.Take(curve.Values.Length - 1).Contains((short)4096))
            {
                throw new InvalidDataException("Native774500 scalar curve must end at its first original4096 terminator.");
            }
        }
    }
}

public sealed record NativeBattleRinoa87CameraInputs(NativeBattleRinoa87CameraResources Resources, Func<int, short> ReadCasterYaw, Func<NativeBattleRinoa87Frame, ImmutableArray<byte>> ReadBodyDestination328, Func<NativeBattleRinoa87Frame, (NativeBattleRinoa87Frame Frame, int Value)> Random55CBD2, Func<NativeBattleRinoa87Frame, NativeBattleMoogleCameraStep, NativeBattleRinoa87Frame> Publish);

/// <summary>7743A0's fourteen camera states and its layout-identical7749C0/774500/774850 orbit kernel.</summary>
public static class NativeBattleRinoa87Camera
{
    public static NativeBattleRinoa87LeafStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87CameraInputs inputs)
    {
        inputs.Resources.Validate();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x7743A0)
        {
            throw new InvalidDataException("Native7743A0 requires its original144-byte camera task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                byte[] reset = NativeBattleMoogleCamera.Reset732AF0().Data.ToArray();
                BinaryPrimitives.WriteInt16LittleEndian(reset.AsSpan(232), inputs.ReadCasterYaw(task.ReadByte(44)));
                Store(new(reset.ToImmutableArray(), true));
                Next();
                break;
            case 1:
                AdvanceCamera();
                if (Scene().Word(66) >= 1)
                {
                    Initialize(0x15600F8, SceneAnchor());
                    Next();
                }
                break;
            case 2:
                if (frame.State.Counter25B4B4C == 0 && AdvanceCamera())
                {
                    Initialize(0x1560140, SceneAnchor());
                    Next();
                }
                break;
            case 3:
                if (frame.State.Counter25B4B4C == 0 && AdvanceCamera())
                {
                    (NativeBattleRinoa87Frame randomFrame, int random) = inputs.Random55CBD2(frame);
                    frame = randomFrame;
                    Initialize((random & 1) != 0 ? 0x1560188u : 0x15601D0u, BodyAnchor());
                    Next();
                }
                break;
            case 4:
                if (AdvanceCamera())
                {
                    Initialize(Large() ? 0x1560260u : 0x1560218u, BodyAnchor());
                    Next();
                }
                break;
            case 5:
                if (AdvanceCamera())
                {
                    Initialize(Large() ? 0x1560370u : 0x15602E0u, BodyAnchor());
                    Next();
                }
                break;
            case 6:
                if (AdvanceCamera())
                {
                    if (!Large())
                    {
                        Initialize(0x1560328, BodyAnchor());
                    }
                    Next();
                }
                break;
            case 7:
                if (AdvanceCamera())
                {
                    byte[] anchor = BodyAnchor().ToArray();
                    BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(2), 0);
                    Initialize(Large() ? 0x1560400u : 0x15603B8u, anchor.ToImmutableArray());
                    Next();
                }
                break;
            case 8:
                if (AdvanceCamera())
                {
                    Initialize(0x1560468, SceneAnchor());
                    Next();
                }
                break;
            case 9:
                if (AdvanceCamera())
                {
                    Initialize(0x15604F0, SceneAnchor());
                    Next();
                }
                break;
            case 10:
                if (AdvanceCamera())
                {
                    Next();
                }
                break;
            case 11:
                if (Scene().Word(66) >= 3)
                {
                    Next();
                }
                break;
            case 12:
                hierarchy = new(frame.Effect.Pools);
                task = hierarchy.Read(address);
                frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1))).Pools } };
                Next();
                break;
            case 13:
                break;
            default:
                throw new InvalidDataException("Native7743A0's signed phase exceeds its fourteen callbacks.");
        }
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address);
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        NativeBattleRinoa87SceneMemory Scene() => NativeBattleRinoa87Scene.Read(frame);
        bool Large() => Scene().SignedWord(78) >= 4096;
        ImmutableArray<byte> SceneAnchor() => Scene().Settings.Read(0, 8).ToImmutableArray();
        ImmutableArray<byte> BodyAnchor()
        {
            ImmutableArray<byte> result = inputs.ReadBodyDestination328(frame);
            if (result.IsDefault || result.Length != 8)
            {
                throw new InvalidDataException("Native7743A0 requires the body's actual raw destination328 anchor.");
            }
            return result;
        }
        void Next()
        {
            NativeBattleEffectHierarchy current = new(frame.Effect.Pools);
            NativeBattleModulePoolTask currentTask = current.Read(address);
            frame = frame with { Effect = frame.Effect with { Pools = current.Write(address, currentTask.WriteByte(41, unchecked((byte)(currentTask.ReadByte(41) + 1)))).Pools } };
        }
        void Store(NativeBattleMoogleCameraState camera)
        {
            frame = frame with { State = frame.State with { Scene = Scene() with { Camera = camera } } };
        }
        void Publish(NativeBattleMoogleCameraStep step)
        {
            Store(step.State);
            frame = inputs.Publish(frame, step);
        }
        NativeBattleMoogleCameraCurve? Curve(uint pointer)
        {
            if (pointer == 0)
            {
                return null;
            }
            return inputs.Resources.Curves.SingleOrDefault(curve => pointer >= curve.Address && (ulong)pointer < (ulong)curve.Address + (ulong)curve.Values.Length * 2) ?? throw new NotSupportedException("Native774500 requires its bounded original scalar curve.");
        }
        bool AdvanceCamera()
        {
            NativeBattleMoogleCameraState camera = Scene().Camera ?? throw new NotSupportedException("Native774500 requires774490's initialized camera owner.");
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(camera, Curve(BinaryPrimitives.ReadUInt32LittleEndian(camera.Data.AsSpan().Slice(80))));
            Publish(step);
            return step.Complete;
        }
        void Initialize(uint sourceAddress, ImmutableArray<byte> anchor)
        {
            NativeBattleMoogleCameraSource source = inputs.Resources.Sources.Single(source => source.Address == sourceAddress);
            NativeBattleMoogleCameraState camera = Scene().Camera ?? throw new NotSupportedException("Native7749C0 requires774490's initialized camera owner.");
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(camera, source, anchor, 0, Curve(BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan())));
            Publish(step);
        }
    }
}
