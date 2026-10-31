using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    private static NativeBattleRenzokuken160ChildStep Advance334Lines(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] bank = state.ExtraStorage.ToArray();
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        short counter = unchecked((short)task.ReadWord(12));
        if (advance && counter <= 8)
        {
            int row = TargetFree(bank);
            if (row < bank.Length)
            {
                if (row + 64 > bank.Length)
                {
                    throw new NotSupportedException("Native588FC0 paired line write reaches beyond its physical stored-row bank.");
                }
                Initialize(row, state.Core.Origin, state.Core.Direction, state.Core.OriginPadding);
                NativeBattleVector middle = new(unchecked((short)((state.Core.Origin.X + state.PreviousOrigin.X) >> 1)), unchecked((short)((state.Core.Origin.Y + state.PreviousOrigin.Y) >> 1)), unchecked((short)((state.Core.Origin.Z + state.PreviousOrigin.Z) >> 1)));
                Initialize(row + 32, middle, state.PreviousDirection, null);
            }
        }
        int alive = 0;
        for (int row = 0; row < bank.Length; row += 32)
        {
            if ((bank[row] & 1) == 0)
            {
                continue;
            }
            short dx = GroupWord(bank, row + 24);
            short dz = GroupWord(bank, row + 28);
            NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(resources.SpecialAlignmentAxis[0], resources.SpecialAlignmentAxis[1], resources.SpecialAlignmentAxis[2], dx, 0, dz);
            NativeBattleActionMatrix world = new(NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis), GroupWord(bank, row + 8), GroupWord(bank, row + 10), GroupWord(bank, row + 12), 4096);
            world = NativeBattlePoseMatrices.Scale56BEF0(world, dx, GroupWord(bank, row + 18), dz);
            short phase = GroupWord(bank, row + 4);
            NativeBattleModuleMeshContext context = new(phase >= 4 ? 243u : 51u, 0, phase >= 4 ? (phase - 4) << 9 : null, null, null, null, 17, 2);
            NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resources.Meshes[4].Bytes, 0, default, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, context, colors, pilotIdentity: inputs.CapturePresentation ? NativeBattleRenzokukenVisualLifetimes.Particle(frame.Effect, 3, row / 32, resources.Meshes[4].Address) : null);
            frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
            if (!advance)
            {
                continue;
            }
            if (phase >= 12)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(row), 0);
                continue;
            }
            GroupPut(bank, row + 4, phase + 1);
            short speed = GroupWord(bank, row + 22);
            GroupPut(bank, row + 18, GroupWord(bank, row + 18) + speed);
            GroupPut(bank, row + 22, speed - speed / 4);
            alive++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        void Initialize(int row, NativeBattleVector origin, NativeBattleVector direction, ushort? padding)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(row), 1);
            GroupPut(bank, row + 4, 0);
            GroupPut(bank, row + 8, origin.X);
            GroupPut(bank, row + 10, origin.Y + 600);
            GroupPut(bank, row + 12, origin.Z);
            if (padding is ushort known)
            {
                GroupPut(bank, row + 14, known);
                frame = SetExtraPaddingUnknown(frame, row / 32, false);
            }
            GroupPut(bank, row + 18, 2048);
            GroupPut(bank, row + 22, 2048);
            var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(direction.X, 0, direction.Z);
            GroupPut(bank, row + 24, normalized.X);
            GroupPut(bank, row + 26, normalized.Y);
            GroupPut(bank, row + 28, normalized.Z);
            if (inputs.CapturePresentation)
            {
                NativeBattleRenzokukenVisualLifetimes.Allocated(frame.Effect, 3, row / 32);
            }
        }
    }
}
