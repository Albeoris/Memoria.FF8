using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84GlowResources(ImmutableArray<NativeBattleStaticMeshResource> Meshes);
public sealed record NativeBattleAngelWing84GlowInputs(Func<byte, byte, short, NativeBattleVector> ReadBone, NativeBattleActionMatrix Camera, NativeBattleCameraProjection Projection, NativeBattleLogicalOrdering Ordering, NativeBattleSummonColors Colors, NativeBattleAngelWing84GlowResources Resources)
{
    public bool CapturePresentation { get; init; }
}
public sealed record NativeBattleAngelWing84GlowStep(NativeBattleAngelWing84Frame Frame, int Result, NativeBattleLogicalOrdering Ordering);

/// <summary>The original796FD0 caster glow and7972B0 camera-centered glow, including their separate phase gates.</summary>
public static class NativeBattleAngelWing84Glows
{
    public static NativeBattleAngelWing84GlowStep Advance(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84GlowInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        bool caster = task.Callback == 0x796FD0;
        if (!caster && task.Callback != 0x7972B0)
        {
            throw new InvalidDataException("Native Angel Wing glow requires its original callback owner.");
        }
        NativeBattleAngelWing84SceneMemory scene = NativeBattleAngelWing84Scene.Read(frame);
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 4));
                Next();
                break;
            case 1:
                if (unchecked((short)task.ReadWord(36)) >= 32)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4)).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteWord(80, 4096).WriteWord(82, unchecked((ushort)-512)).WriteDword(76, caster ? 0x15720B0u : 0x1572810u);
                    if (!caster)
                    {
                        NativeBattleMoogleCameraState camera = scene.Camera ?? throw new NotSupportedException("Native797430 requires its current camera workspace.");
                        task = task.WriteWord(28, 0).WriteWord(30, 0).WriteWord(32, BinaryPrimitives.ReadUInt16LittleEndian(camera.Data.AsSpan().Slice(224)));
                    }
                    Next();
                }
                break;
            case 2:
                short faded = unchecked((short)(task.ReadWord(80) - 128));
                task = task.WriteWord(80, unchecked((ushort)faded));
                if (faded <= 0)
                {
                    task = task.WriteWord(80, 0);
                    Next();
                }
                break;
            case 3:
                if (scene.Word(64) >= (caster ? 4 : 3))
                {
                    task = caster ? task.WriteWord(84, 24) : task.WriteByte(38, (byte)(task.ReadByte(38) | 4));
                    Next();
                }
                break;
            case 4:
                if (caster)
                {
                    short wait = unchecked((short)(task.ReadWord(84) - 1));
                    task = task.WriteWord(84, unchecked((ushort)wait));
                    if (wait <= 0)
                    {
                        Next();
                    }
                }
                else if (scene.Word(64) >= 4)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) & ~4));
                    Next();
                }
                break;
            case 5:
                short fade = unchecked((short)(task.ReadWord(80) + (caster ? 512 : 128)));
                task = task.WriteWord(80, unchecked((ushort)fade));
                if (fade >= 4096)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1)).WriteWord(80, 4096);
                    Next();
                }
                break;
            case 6:
                break;
            default:
                throw new InvalidDataException("Native Angel Wing glow phase exceeds its seven original states.");
        }
        if (caster)
        {
            NativeBattleVector anchor = inputs.ReadBone(task.ReadByte(44), 241, 0);
            task = task.WriteWord(28, unchecked((ushort)anchor.X)).WriteWord(30, unchecked((ushort)anchor.Y)).WriteWord(32, unchecked((ushort)anchor.Z));
        }
        hierarchy = hierarchy.Write(address, task);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleLogicalOrdering ordering = inputs.Ordering;
        if ((task.ReadByte(38) & 4) == 0)
        {
            NativeBattleActionMatrix matrix = Matrix(task, inputs.Camera, caster);
            uint source = task.ReadDword(76);
            NativeBattleStaticMeshResource mesh = inputs.Resources.Meshes.SingleOrDefault(candidate => candidate.Address == source) ?? throw new NotSupportedException("Native Angel Wing glow has no original mesh source.");
            NativeBattleTextureWindow window = new(0, 0, 256, 256);
            NativeBattleEsunaMeshContext context = new(240, task.ReadDword(64), unchecked((short)task.ReadWord(80)), unchecked((short)task.ReadWord(82)), 0, 0, window, window, 17, 2);
            NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh.Bytes, matrix, inputs.Projection, context, inputs.Colors, observeFirstThreeFlags: flags => frame = frame with { Memory = frame.Memory with { NodeScratch2793E30 = (frame.Memory.NodeScratch2793E30 ?? throw new NotSupportedException("Native Angel Wing mesh requires the current projection flag owner.")).WriteFlags(flags) } }, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(84, task) with { ResourceGeneration = source } : null);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Angel Wing glow requires its executed private draw cursor."), ordering, NativeBattleRetainedPolygonDepths.Capture(batch));
            frame = frame with { Memory = written.Memory, State = frame.State with { PrimaryDrawCursor = written.Cursor } };
            ordering = written.Ordering;
        }
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address).WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0, ordering);

        void Next()
        {
            task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        }
    }

    internal static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, bool caster)
    {
        camera.Validate();
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.Identity8DD770();
        if (caster)
        {
            ImmutableArray<short> r = camera.Rotation;
            basis = basis with { Rotation = [r[0], r[3], r[6], r[1], r[4], r[7], r[2], r[5], r[8]] };
            basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, unchecked((short)task.ReadWord(72)));
        }
        basis = NativeBattlePoseMatrices.Scale56BEF0(basis, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56))) with { X = unchecked((short)task.ReadWord(28)), Y = unchecked((short)task.ReadWord(30)), Z = unchecked((short)task.ReadWord(32)) };
        return caster ? NativeBattleGfMatrices.Compose56C2F0(camera, basis) : basis;
    }
}
