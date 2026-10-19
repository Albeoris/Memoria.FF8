using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84CameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources, ImmutableArray<NativeBattleMoogleCameraCurve> Curves)
{
    public void Validate()
    {
        uint[] sourceAddresses = [0x1575DE0, 0x1575E28, 0x1575E70, 0x1575F20, 0x1575F90, 0x1575FD8];
        uint[] curveAddresses = [0x1575EB8, 0x1575F68];
        int[] curveLengths = [50, 18];
        if (Sources.IsDefault || Sources.Length != sourceAddresses.Length || Curves.IsDefault || Curves.Length != curveAddresses.Length)
        {
            throw new InvalidDataException("Native Angel Wing camera requires its six sources and two bounded scalar curves.");
        }
        for (int index = 0; index < Sources.Length; index++)
        {
            NativeBattleMoogleCameraSource source = Sources[index];
            if (source is null || source.Address != sourceAddresses[index] || source.Data.IsDefault || source.Data.Length != 72 || BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(36)) != 0)
            {
                throw new InvalidDataException("Native Angel Wing camera source contradicts its original address, extent or anchor mode.");
            }
            uint curve = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan());
            uint expected = index == 3 ? curveAddresses[0] : index == 4 ? curveAddresses[1] : 0;
            if (curve != expected)
            {
                throw new InvalidDataException("Native Angel Wing camera source points to a different scalar curve.");
            }
        }
        for (int index = 0; index < Curves.Length; index++)
        {
            NativeBattleMoogleCameraCurve curve = Curves[index];
            if (curve is null || curve.Address != curveAddresses[index] || curve.Values.IsDefault || curve.Values.Length != curveLengths[index] || curve.Values[^1] != 4096)
            {
                throw new InvalidDataException("Native Angel Wing camera curve contradicts its original bounded4096 terminator.");
            }
        }
    }
}
public sealed record NativeBattleAngelWing84CameraInputs(NativeBattleAngelWing84CameraResources Resources, Func<NativeBattleAngelWing84Frame, NativeBattleMoogleCameraStep, NativeBattleAngelWing84Frame> Publish);

/// <summary>78DE40's ten-state camera director over its own236-byte owner and the identical732B10/732E00 camera kernel.</summary>
public static class NativeBattleAngelWing84Camera
{
    public static NativeBattleAngelWing84LeafStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84CameraInputs inputs)
    {
        inputs.Resources.Validate();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x78DE40)
        {
            throw new InvalidDataException("Native78DE40 requires its original100-byte camera task.");
        }
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84Scene.Read(frame);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                frame = Store(frame, scene.ClearCamera());
                Next();
                break;
            case 1:
                if (scene.Word(66) >= 1)
                {
                    Initialize(0x1575DE0);
                    Next();
                }
                break;
            case 2:
                if (AdvanceCamera())
                {
                    Initialize(0x1575E28);
                    Next();
                }
                break;
            case 3:
                AdvanceCamera();
                if (NativeBattleAngelWing84Scene.Read(frame).Word(66) >= 2)
                {
                    Initialize(0x1575E70);
                    Next();
                }
                break;
            case 4:
                AdvanceCamera();
                if (NativeBattleAngelWing84Scene.Read(frame).Word(66) >= 3)
                {
                    Initialize(0x1575F20);
                    Next();
                }
                break;
            case 5:
                if (AdvanceCamera())
                {
                    Initialize(0x1575F90);
                    Next();
                }
                break;
            case 6:
                AdvanceCamera();
                if (NativeBattleAngelWing84Scene.Read(frame).Word(66) >= 4)
                {
                    Initialize(0x1575FD8);
                    Next();
                }
                break;
            case 7:
                AdvanceCamera();
                if (NativeBattleAngelWing84Scene.Read(frame).Word(66) >= 5)
                {
                    Next();
                }
                break;
            case 8:
                hierarchy = new(frame.Effect.Pools);
                task = hierarchy.Read(address);
                hierarchy = hierarchy.Write(address, task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
                Next();
                break;
            case 9:
                break;
            default:
                throw new InvalidDataException("Native78DE40's signed phase exceeds its ten callbacks.");
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

        void Next()
        {
            NativeBattleEffectHierarchy current = new(frame.Effect.Pools);
            NativeBattleModulePoolTask cameraTask = current.Read(address);
            current = current.Write(address, cameraTask.WriteByte(41, unchecked((byte)(cameraTask.ReadByte(41) + 1))));
            frame = frame with { Effect = frame.Effect with { Pools = current.Pools } };
        }

        void Initialize(uint sourceAddress)
        {
            NativeBattleMoogleCameraSource source = inputs.Resources.Sources.SingleOrDefault(candidate => candidate.Address == sourceAddress) ?? throw new NotSupportedException("Native78DF50 requires its exact original camera source.");
            scene = NativeBattleAngelWing84Scene.Read(frame);
            NativeBattleMoogleCameraState camera = scene.Camera ?? throw new NotSupportedException("Native78DF50 requires78DEE0's initialized camera workspace.");
            (byte[] anchor, bool[] known) = scene.Settings.ReadSnapshot(0, 8);
            camera = camera with { EyePaddingKnown = camera.EyePaddingKnown && known[6] && known[7] };
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Initialize732B10(camera, source, anchor.ToImmutableArray(), 0, Curve(BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan())));
            Publish(step);
        }

        bool AdvanceCamera()
        {
            scene = NativeBattleAngelWing84Scene.Read(frame);
            NativeBattleMoogleCameraState camera = scene.Camera ?? throw new NotSupportedException("Native78E240 requires its retained camera workspace.");
            NativeBattleMoogleCameraStep step = NativeBattleMoogleCamera.Advance732E00(camera, Curve(BinaryPrimitives.ReadUInt32LittleEndian(camera.Data.AsSpan().Slice(0x50))));
            Publish(step);
            return step.Complete;
        }

        void Publish(NativeBattleMoogleCameraStep step)
        {
            frame = Store(frame, NativeBattleAngelWing84Scene.Read(frame) with { Camera = step.State });
            frame = inputs.Publish(frame, step);
        }

        NativeBattleMoogleCameraCurve? Curve(uint pointer)
        {
            if (pointer == 0)
            {
                return null;
            }
            return inputs.Resources.Curves.SingleOrDefault(curve => pointer >= curve.Address && (ulong)pointer < (ulong)curve.Address + (ulong)curve.Values.Length * 2) ?? throw new NotSupportedException("Native78E240 requires its original bounded scalar curve.");
        }
    }

    private static NativeBattleAngelWing84Frame Store(NativeBattleAngelWing84Frame frame, NativeBattleAngelWing84SceneMemory scene) => frame with { State = frame.State with { Scene = scene } };
}
