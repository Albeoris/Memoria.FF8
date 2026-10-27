using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215TargetBoundsResult(NativeBattleDuel215Frame Frame, short Maximum, short Minimum);
public sealed record NativeBattleDuel215TargetServices(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, ImmutableArray<byte> Targets, Func<NativeBattleDuel215Frame, int, NativeBattleDuel215TargetBoundsResult> ReadBounds69D360, Func<NativeBattleDuel215Frame, int, NativeBattleVector, NativeBattleVector, NativeBattleDuel215Frame> Draw69D200);

public static class NativeBattleDuel215Targets
{
    public static NativeBattleDuel215Frame Advance69B070(NativeBattleDuel215Frame frame, int slot, short tick, NativeBattleDuel215TargetServices services)
    {
        uint phase = unchecked((uint)(tick - 222));
        if (phase >= 59)
        {
            return frame;
        }
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int ratio = 0;
        short displacement = 0;
        if (phase < 40)
        {
            ratio = checked((int)(phase << 12)) / 40;
            int height = ratio >= 3072 ? 3444 * (4096 - ratio) / 1024 : unchecked((int)(Math.Sqrt(NativeBattleCameraMath.Sine(checked((int)(phase << 12)) / 80) * (double)BitConverter.Int32BitsToSingle(0x39800000)) * 4096f));
            displacement = unchecked((short)(-2 * height));
            if (phase == 0)
            {
                frame = NativeBattleDuel215Workspaces.Initialize69B070(frame, slot, 1420, 0x116AD0C, 340);
                Refresh();
                for (int index = 0; index < services.Targets.Length; index++)
                {
                    int target = Target(index);
                    Store();
                    NativeBattleDuel215TargetBoundsResult bounds = services.ReadBounds69D360(frame, target);
                    frame = bounds.Frame;
                    Refresh();
                    task = task.WriteWord(64 + index * 32 + 28, unchecked((ushort)bounds.Maximum)).WriteWord(64 + index * 32 + 30, unchecked((ushort)bounds.Minimum));
                    NativeBattleGraphicsModel model = services.ReadModel(target);
                    services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 8) });
                }
            }
        }
        for (int index = 0; index < services.Targets.Length; index++)
        {
            int target = Target(index);
            int offset = 64 + index * 32;
            NativeBattleGraphicsModel model = services.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel215 target motion requires its actual raw position WORDs.");
            NativeBattleVector angles;
            NativeBattleVector pivot;
            if (phase < 40)
            {
                short maximum = Signed(offset + 28);
                short minimum = Signed(offset + 30);
                short y = unchecked((short)(displacement + Signed(offset + 6) - ratio * maximum / 4096));
                transform = transform with { Position28 = transform.Position28 with { Y = y } };
                angles = new(Signed(offset + 20), Signed(offset + 22), Signed(offset + 24));
                pivot = new(0, unchecked((short)((maximum + minimum) >> 1)), 0);
            }
            else
            {
                transform = transform with { Position28 = transform.Position28 with { Y = Signed(offset + 6) } };
                services.WriteModel(target, model with { Transform = transform });
                Store();
                NativeBattleDuel215TargetBoundsResult bounds = services.ReadBounds69D360(frame, target);
                frame = bounds.Frame;
                Refresh();
                task = task.WriteWord(offset + 28, unchecked((ushort)bounds.Maximum)).WriteWord(offset + 30, unchecked((ushort)bounds.Minimum));
                model = services.ReadModel(target);
                transform = model.Transform ?? throw new NotSupportedException("Native Duel215 target bounds lost its raw transform.");
                transform = transform with { Position28 = transform.Position28 with { Y = unchecked((short)(Signed(offset + 6) - bounds.Maximum)) } };
                angles = new(ReadWord(0x2508990), ReadWord(0x2508992), ReadWord(0x2508994));
                pivot = angles;
            }
            services.WriteModel(target, model with { Transform = transform });
            Store();
            frame = services.Draw69D200(frame, target, angles, pivot);
            Refresh();
            if (phase < 40)
            {
                task = task.WriteWord(offset + 20, unchecked((ushort)(Signed(offset + 20) + ((index & 1) != 0 ? 204 : -204))));
            }
            model = services.ReadModel(target);
            services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 4) });
        }
        if (phase == 40)
        {
            foreach (int target in services.Targets)
            {
                NativeBattleGraphicsModel model = services.ReadModel(target);
                services.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 & ~8) });
            }
            NativeBattleEffectMemory memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 360, NativeBattleDuel215Controller.Arena + 28952, 1);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 361, NativeBattleDuel215Controller.Arena + 28952, 1);
            frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(memory, 362, NativeBattleDuel215Controller.Arena + 28952, 0) };
        }
        Store();
        return frame;

        NativeBattleEffectTaskPool Pool() => frame.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleDuel215Controller.Scene);
        void Refresh()
        {
            pool = Pool();
            task = pool.Slots[slot];
        }
        void Store()
        {
            pool = pool.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        }
        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        short ReadWord(uint address) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(address, 2));
        int Target(int index)
        {
            int target = services.Targets[index];
            if (task.ReadDword(64 + index * 32) != 0x1D972C0u + (uint)target * 156)
            {
                throw new InvalidDataException("Native Duel215 target motion lost its actual model pointer.");
            }
            return target;
        }
    }
}
