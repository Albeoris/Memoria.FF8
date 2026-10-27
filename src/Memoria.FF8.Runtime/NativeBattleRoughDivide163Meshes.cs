using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRoughDivide163Geometry(NativeBattleGfGeometryBatch Batch) : NativeBattleRoughDivide163Operation;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep AdvanceMesh(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        bool delay = task.Callback is 0x5F8770 or 0x5F8B80;
        if (delay && Word(task, 14) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool.WriteSlot(slot, task), 0);
        }
        short counter = Word(task, 12);
        uint address;
        int fadeStart;
        int lifetime;
        int fade;
        NativeBattleActionMatrix world;
        switch (task.Callback)
        {
            case 0x5F65E0:
                address = 0xD9E2D4;
                fadeStart = 7;
                lifetime = 15;
                fade = (counter - 7) << 9;
                NativeBattleModuleAnchor bone = inputs.ReadBone241(frame.Parameters.Actor78);
                int dx = Word(task, 16) - bone.X;
                int dz = Word(task, 20) - bone.Z;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                double squared = (float)((double)dz * (float)dz + (double)dx * dx);
                int returnedSquare = unchecked((int)((uint)BitConverter.DoubleToInt64Bits(squared + 4503601774854144d) + 0x80000000u));
                if (returnedSquare < 0)
                {
                    throw new NotSupportedException("Native5F65E0 reaches an unsupported exceptional signed56BEC0 square root.");
                }
                int length = unchecked((int)(long)Math.Sqrt(returnedSquare));
                NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(0, 0, 4096, direction.X, direction.Y, direction.Z);
                world = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), bone.X, 0, bone.Z, 4096);
                world = NativeBattlePoseMatrices.Scale56BEF0(world, Word(task, 28), Word(task, 32), unchecked(length << 12) / 1800);
                break;
            case 0x5F78D0:
                address = 0xD9EC8C;
                fadeStart = 1;
                lifetime = 7;
                fade = 682 * counter - 682;
                world = TaskWorld(task);
                break;
            case 0x5F8540:
                address = 0xD9B5D0;
                fadeStart = 12;
                lifetime = 20;
                fade = (counter - 12) << 9;
                world = TaskWorld(task);
                break;
            case 0x5F8770:
                address = 0xD9D594;
                fadeStart = 8;
                lifetime = 16;
                fade = (counter - 8) << 9;
                world = TaskWorld(task);
                break;
            case 0x5F8960:
                address = 0xD9DD34;
                fadeStart = 8;
                lifetime = 16;
                fade = (counter - 8) << 9;
                world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Word(task, 28), Word(task, 28), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
                break;
            case 0x5F8B80:
                address = 0xD9CB38;
                fadeStart = 4;
                lifetime = 18;
                fade = counter < 4 ? (4 - counter) << 10 : (counter - 10) << 9;
                world = TaskWorld(task);
                break;
            default:
                throw new NotSupportedException($"Native Rough Divide mesh callback0x{task.Callback:X} is unsupported.");
        }
        if (mesh.Address != address)
        {
            throw new InvalidDataException("Native Rough Divide callback requires its actual mesh directory entry.");
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleStaticMeshResource current = mesh with { Bytes = frame.Staging.Read(address, mesh.Bytes.Length).ToImmutableArray() };
        NativeBattleGfGeometryBatch batch;
        if (task.Callback == 0x5F8B80)
        {
            bool fading = counter < 4 || counter >= 10;
            batch = NativeBattleShivaScrollingPackets.Emit64E080(current, matrix, projection, new(fading ? 0xF3u : 0x33u, fading ? fade : null, counter * Word(task, 34), 0, 17, 2, new(0, 192, 128, 64)), 341, 256, 0, 0, colors);
        }
        else
        {
            batch = NativeBattleModulePackets.EmitBatch572200(current.Bytes, 0, default, matrix, projection, new(counter >= fadeStart ? 0xF3u : 0x33u, 0, counter >= fadeStart ? fade : null, null, null, null, 17, 2), colors);
        }
        frame = inputs.Execute(frame, new NativeBattleRoughDivide163Geometry(batch));
        if (paused)
        {
            return new(frame, pool, 0);
        }
        switch (task.Callback)
        {
            case 0x5F65E0:
                task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + Word(task, 34))));
                break;
            case 0x5F78D0:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                task = Damp(task, 32, 34, 7);
                break;
            case 0x5F8540:
                task = Damp(task, 32, 34, 4);
                break;
            case 0x5F8770:
                task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
                task = Damp(task, 28, 30, 6);
                break;
            case 0x5F8960:
                task = Damp(task, 28, 30, 4);
                break;
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= lifetime ? 2 : 0);
    }

    private static NativeBattleActionMatrix TaskWorld(NativeBattleModulePoolTask task)
    {
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0));
        return NativeBattlePoseMatrices.Scale56BEF0(world, Word(task, 28), Word(task, 32), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
    }

    private static NativeBattleModulePoolTask Damp(NativeBattleModulePoolTask task, int scaleOffset, int velocityOffset, int divisor)
    {
        short velocity = Word(task, velocityOffset);
        return task.WriteWord(scaleOffset, unchecked((ushort)(Word(task, scaleOffset) + velocity))).WriteWord(velocityOffset, unchecked((ushort)(velocity - velocity / divisor)));
    }
}
