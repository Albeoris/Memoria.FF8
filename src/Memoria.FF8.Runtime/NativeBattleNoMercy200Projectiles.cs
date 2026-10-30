using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep Begin63D370(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, NativeBattleNoMercy200TargetInputs inputs)
    {
        NativeBattleActionMatrix basis = ReadMatrix(frame.Staging, 0x24F8D90);
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(-400, -500, -500));
        NativeBattleVector origin = new(unchecked((short)(rotated.X + basis.X)), unchecked((short)(rotated.Y + basis.Y)), unchecked((short)(rotated.Z + basis.Z)));
        NativeBattleFileStaging staging = WritePoint(frame.Staging, 0x24FBCD8, origin);
        int index = 0;
        foreach (int actor in SelectedTargets(frame, inputs.Packets))
        {
            if (index >= 8)
            {
                throw new NotSupportedException("Native No Mercy projectile target leaves its original eight physical rows.");
            }
            uint row = checked(0x24F7AA0u + (uint)index * 592);
            NativeBattleModuleAnchor anchor = inputs.ReadAnchor(actor);
            NativeBattleVector target = new(anchor.X, anchor.Y, anchor.Z);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(target.X - origin.X, 0, target.Z - origin.Z);
            staging = CopyEight(staging, 0x24FBCD8, row);
            staging = WriteWord(WritePoint(staging, row + 8, target), row + 14, anchor.W);
            staging = CopyEight(staging, row, row + 16);
            staging = CopyEight(staging, row + 8, row + 24);
            staging = WriteWord(staging, row + 26, 0);
            int distance = Rand() % 1000 + 1500;
            short x = unchecked((short)(target.X + (unchecked(direction.X * distance) >> 12)));
            short z = unchecked((short)(target.Z + (unchecked(direction.Z * distance) >> 12)));
            short y = unchecked((short)(target.Y - Rand() % 1000 - 1500));
            staging = WritePoint(staging, row + 32, new(x, y, z));
            staging = WritePoint(staging, row + 40, new(unchecked((short)(x + (unchecked(direction.X * distance) >> 12))), unchecked((short)(y - Rand() % 1000 - 1500)), unchecked((short)(z + (unchecked(direction.Z * distance) >> 12)))));
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(staging, 400);
            NativeBattleEffectArena points = NativeBattleTemporaryMemory.ReadView(temporary.Staging, row + 16, 32);
            NativeBattleEffectArena coefficients = NativeBattleGilgameshSwordTrailCurve.Prepare571620(4, points, 0, NativeBattleTemporaryMemory.ReadView(temporary.Staging, temporary.Address, 400));
            NativeBattleEffectArena curve = NativeBattleTemporaryMemory.ReadView(temporary.Staging, row + 48, 512);
            for (int sample = 0; sample < 64; sample++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(4, coefficients, sample * 4096 / 63, curve, sample * 8);
            }
            staging = NativeBattleTemporaryMemory.PublishView(temporary.Staging, temporary.Address, coefficients, 400);
            staging = NativeBattleTemporaryMemory.PublishView(staging, row + 48, curve, 512);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 400);
            NativeBattleNoMercy200Resources resources = frame.Memory.NoMercy200?.Resources ?? throw new NotSupportedException("Native No Mercy projectile requires its actual axis source.");
            ReadOnlySpan<byte> axis = resources.Read(0xE33D40, 12);
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(axis), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(axis.Slice(4)), System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(axis.Slice(8)), direction.X, direction.Y, direction.Z);
            ImmutableArray<short> rotation = NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis);
            for (int component = 0; component < 9; component++)
            {
                staging = WriteWord(staging, row + 560 + (uint)component * 2, rotation[component]);
            }
            for (ushort part = 0; part < 4; part++)
            {
                (pool, int slot) = pool.Append(0x63D670);
                NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(14, checked((ushort)index)).WriteWord(20, part).WriteWord(22, part).WriteWord(24, (ushort)(Rand() % 2048)).WriteWord(26, (ushort)(Rand() % 60 + 180)).WriteWord(28, 2048).WriteWord(32, 1).WriteWord(34, 2);
                pool = pool.WriteSlot(slot, task);
            }
            (pool, int wave) = pool.Append(0x63D800);
            pool = pool.WriteSlot(wave, pool.Slots[wave].WriteWord(12, 0).WriteWord(14, checked((ushort)index)).WriteWord(32, 1).WriteWord(34, 2));
            index++;
        }
        return new(frame with { Staging = staging }, pool, 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }

    public static NativeBattleNoMercy200ChildStep Advance63D670(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (Word(task, 22) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(22, unchecked((ushort)(Word(task, 22) - 1)));
            }
            return new(frame, pool.WriteSlot(slot, task), 0);
        }
        uint row = ProjectileRow(task);
        int sample = Word(task, 32);
        if (sample is < 0 or >= 64)
        {
            throw new NotSupportedException("Native No Mercy projectile sample leaves its actual64-point path.");
        }
        NativeBattleActionMatrix orientation = ReadMatrix(frame.Staging, row + 560) with { X = 0, Y = 0, Z = 0 };
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Compose56C2F0(orientation, NativeBattleGfMatrices.Euler56CE30(new(Word(task, 24), 0, 0)));
        uint point = checked(row + 48 + (uint)sample * 8);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Word(task, 28), Word(task, 28), Word(task, 28)) with { X = ReadWord(frame.Staging, point), Y = ReadWord(frame.Staging, point + 2), Z = ReadWord(frame.Staging, point + 4) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(inputs.View, local);
        frame = inputs.Execute(frame, new NativeBattleNoMercy200MeshDraw(0xE3239C, matrix, new(0xF3, 0, 768 * Word(task, 20), null, null, null, 17, 2)) { PilotIdentity = Visual200(task, inputs, 0xE3239C) });
        if (paused)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 34);
        short tick = Word(task, 12);
        task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26)))).WriteWord(32, unchecked((ushort)(sample + speed)));
        if (tick > 4 && speed < 6)
        {
            task = task.WriteWord(34, unchecked((ushort)(speed + 2)));
        }
        task = task.WriteWord(12, unchecked((ushort)(tick + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 32) >= 64 ? 2 : 0);
    }

    private static uint ProjectileRow(NativeBattleModulePoolTask task)
    {
        int row = Word(task, 14);
        if (row is < 0 or >= 8)
        {
            throw new NotSupportedException("Native No Mercy projectile requires its original bounded target row.");
        }
        return checked(0x24F7AA0u + (uint)row * 592);
    }
}
