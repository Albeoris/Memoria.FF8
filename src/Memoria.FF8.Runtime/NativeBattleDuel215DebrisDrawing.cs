using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215Packets(NativeBattleGfGeometryBatch Batch, bool Common) : NativeBattleDuel215Operation;

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleDuel215Frame DrawParticles69CC90(NativeBattleDuel215Frame frame, NativeBattleModulePoolTask task, NativeBattleDuel215Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, bool capturePresentation = false)
    {
        NativeBattleDuel215Resources resources = frame.Memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 debris requires resources retained by actual entry.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        frame = frame with { Staging = allocation.Staging };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 128; index++)
        {
            int row = 2300 + index * 22;
            if (task.ReadWord(row + 6) == 0)
            {
                continue;
            }
            NativeBattleActionMatrix matrix = ParticleMatrix69CC90(resources, task, row, view);
            short angle = unchecked((short)task.ReadWord(row + 18));
            short sine = resources.Sine(angle);
            short cosine = resources.Cosine(angle);
            NativeBattleActionMatrix second = new([cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine], 0, 0, 0, 4096);
            NativeBattleFileStaging staging = frame.Staging.Write(allocation.Address, new byte[20]).Write(allocation.Address + 32, new byte[20]);
            staging = Rotation(staging, allocation.Address, matrix);
            staging = Rotation(staging, allocation.Address + 32, second);
            staging = Dword(Dword(Dword(staging, allocation.Address + 20, unchecked((uint)matrix.X)), allocation.Address + 24, unchecked((uint)matrix.Y)), allocation.Address + 28, unchecked((uint)matrix.Z));
            uint polygons = checked(0x1154F60u + resources.Dword(0x1154F50));
            staging = Dword(Dword(staging, allocation.Address + 64, 0x1154F58), allocation.Address + 68, polygons);
            frame = frame with { Staging = staging };
            int count = checked((int)resources.Dword(polygons));
            uint cursor = checked(polygons + 4);
            frame = frame with { Staging = Dword(frame.Staging, allocation.Address + 68, cursor) };
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = resources.Read(cursor, 20).ToArray();
                System.Numerics.Vector3[]? world = capturePresentation ? new System.Numerics.Vector3[3] : null;
                NativeBattleGfGeometryBatch batch = NativeBattleDuel215ParticlePackets.Emit69CE00(face, Project, 17, 2, allocated, observation =>
                {
                    NativeBattleFileStaging current = Dword(frame.Staging, allocation.Address + 84, observation.Flags);
                    if (observation.Nclip is int area)
                    {
                        current = Dword(current, allocation.Address + 72, unchecked((uint)area));
                    }
                    if (observation.AverageDepth is int depth)
                    {
                        current = Dword(current, allocation.Address + 80, unchecked((uint)depth));
                    }
                    frame = frame with { Staging = current };
                }, capturePresentation ? NativeBattleDuel28Presentation.Identity(215, task, 0x1154F58, unchecked(index * 65537 + faceIndex)) with { ResourceGeneration = NativeBattleDuel28Presentation.Row(task, row, 0x1154F58) } : null, world);
                attempts.AddRange(batch.Attempts);
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + 20);
                frame = frame with { Staging = Dword(frame.Staging, allocation.Address + 68, cursor) };

                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2));
                    uint address = checked(0x1154F58u + (uint)offset * 4);
                    NativeBattleVector point = new(resources.Word(address), resources.Word(address + 2), resources.Word(address + 4));
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                    }
                    return NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
                }
            }
        }
        if (attempts.Count != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleDuel215Packets(new(attempts.ToImmutable(), allocated), true));
        }
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 88) };

        static NativeBattleFileStaging Rotation(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
        {
            byte[] bytes = new byte[18];
            for (int lane = 0; lane < 9; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), matrix.Rotation[lane]);
            }
            return staging.Write(address, bytes);
        }
    }
}
