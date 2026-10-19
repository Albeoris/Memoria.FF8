using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources)
{
    public static ImmutableArray<uint> Addresses { get; } = [0x1555D60, 0x1555DA8, 0x1555DF0, 0x1555E38, 0x1555E80, 0x1555EC8, 0x1555F10, 0x1555F58, 0x1555FA0, 0x1555FE8, 0x1556030, 0x1556078];

    public void Validate()
    {
        if (Sources.IsDefault || Sources.Length != Addresses.Length)
        {
            throw new InvalidDataException("Native Rinoa88 camera requires its twelve original sources.");
        }
        for (int index = 0; index < Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source.Address != Addresses[index] || source.Data.IsDefault || source.Data.Length != 72 || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(36)) != 0)
            {
                throw new InvalidDataException("Native769660 requires its exact original source, linear scalar and anchor mode.");
            }
        }
    }
}

public sealed record NativeBattleRinoa88CameraInputs(NativeBattleRinoa88CameraResources Resources, Func<NativeBattleRinoa88Frame, ImmutableArray<byte>> ReadPartyMidpoint769A90, Func<NativeBattleRinoa88Frame, (NativeBattleRinoa88Frame Frame, int Value)> Random55CBD2, Func<NativeBattleRinoa88Frame, NativeBattleMoogleCameraStep, NativeBattleRinoa88Frame> Publish);

/// <summary>769060's sixteen camera states and layout-identical769660/7691A0/7694F0 orbit kernel.</summary>
public static class NativeBattleRinoa88Camera
{
    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, NativeBattleRinoa88CameraInputs inputs)
    {
        inputs.Resources.Validate();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x769060)
        {
            throw new InvalidDataException("Native769060 requires its original144-byte camera task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                frame = frame with { State = frame.State with { Scene = Scene() with { Camera = NativeBattleMoogleCamera.Reset732AF0(), CameraAnchorPaddingKnown = true } } };
                Next();
                break;
            case 1:
                AdvanceCamera();
                if (Scene().Word(66) >= 1)
                {
                    Initialize(0x1555D60, SceneAnchor());
                    Next();
                }
                break;
            case 2:
                if (AdvanceCamera())
                {
                    Initialize(0x1555DA8, SceneAnchor());
                    Next();
                }
                break;
            case 3:
                if (AdvanceCamera())
                {
                    (NativeBattleRinoa88Frame randomFrame, int random) = inputs.Random55CBD2(frame);
                    frame = randomFrame;
                    Initialize((random & 1) != 0 ? 0x1555DF0u : 0x1555E38u, SceneAnchor());
                    Next();
                }
                break;
            case 4:
            case 5:
                if (AdvanceCamera())
                {
                    Initialize(task.ReadByte(41) == 4 ? 0x1555E80u : 0x1555EC8u, SceneAnchor());
                    Next();
                }
                break;
            case 6:
            case 7:
            case 9:
                AdvanceCamera();
                ushort phase = task.ReadByte(41) == 6 ? (ushort)3 : task.ReadByte(41) == 7 ? (ushort)4 : (ushort)5;
                if (Scene().Word(66) >= phase)
                {
                    uint source = phase == 3 ? 0x1555F10u : phase == 4 ? 0x1555F58u : 0x1555FE8u;
                    Initialize(source, phase == 4 ? PartyAnchor() : SceneAnchor(), phase != 4);
                    Next();
                }
                break;
            case 8:
                if (AdvanceCamera())
                {
                    Initialize(0x1555FA0, PartyAnchor(), false);
                    Next();
                }
                break;
            case 10:
            case 11:
                if (AdvanceCamera())
                {
                    Initialize(task.ReadByte(41) == 10 ? 0x1556030u : 0x1556078u, SceneAnchor());
                    Next();
                }
                break;
            case 12:
                if (AdvanceCamera())
                {
                    Next();
                }
                break;
            case 13:
                if (Scene().Word(66) >= 6)
                {
                    Next();
                }
                break;
            case 14:
                hierarchy = new(frame.Effect.Pools);
                task = hierarchy.Read(address);
                frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1))).Pools } };
                Next();
                break;
            case 15:
                break;
            default:
                throw new InvalidDataException("Native769060's signed phase exceeds its sixteen callbacks.");
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

        NativeBattleRinoa88SceneMemory Scene() => NativeBattleRinoa88Scene.Read(frame);
        ImmutableArray<byte> SceneAnchor() => Scene().Settings.Read(0, 8).ToImmutableArray();
        ImmutableArray<byte> PartyAnchor()
        {
            // Native769A90 writes three WORDs; its fourth stack WORD is unconsumed padding.
            ImmutableArray<byte> result = inputs.ReadPartyMidpoint769A90(frame);
            if (result.IsDefault || result.Length != 8)
            {
                throw new InvalidDataException("Native769060 requires the actual party bounding-box midpoint anchor.");
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
            if (pointer != 0)
            {
                throw new NotSupportedException("Native7691A0 requires the original zero scalar pointer in each reached source.");
            }
            return null;
        }
        bool AdvanceCamera()
        {
            NativeBattleMoogleCameraState camera = Scene().Camera ?? throw new NotSupportedException("Native7691A0 requires769130's initialized camera owner.");
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(camera, Curve(BinaryPrimitives.ReadUInt32LittleEndian(camera.Data.AsSpan().Slice(80))));
            Publish(step);
            return step.Complete;
        }
        void Initialize(uint sourceAddress, ImmutableArray<byte> anchor, bool anchorPaddingKnown = true)
        {
            NativeBattleMoogleCameraSource source = inputs.Resources.Sources.Single(source => source.Address == sourceAddress);
            NativeBattleMoogleCameraState camera = Scene().Camera ?? throw new NotSupportedException("Native769660 requires769130's initialized camera owner.");
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(camera, source, anchor, 0, Curve(BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan())));
            Publish(step);
            frame = frame with { State = frame.State with { Scene = Scene() with { CameraAnchorPaddingKnown = anchorPaddingKnown } } };
        }
    }
}
