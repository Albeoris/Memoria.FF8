using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112DrawingInputs(NativeBattleDuel112ActorServices Actor, Func<int, byte, short, NativeBattleVector> ReadBone502170, NativeBattleActionMatrix View, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Action<int, int, int, int> Flash5712C0);
public sealed record NativeBattleDuel112DrawingStep(NativeBattleDuel112Frame Frame, NativeBattleLogicalOrdering Ordering);

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112DrawingStep DrawMiddle6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112DrawingInputs inputs, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (frame.Effect.Pools[1].DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90)
        {
            throw new InvalidDataException("Native Duel hierarchy requires its actual target task owner.");
        }
        short phase = unchecked((short)task.ReadWord(12));
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel hierarchy requires its actual actor.");
        int first = phase - 17;
        if ((uint)first <= 35)
        {
            if (first == 0)
            {
                frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 372);
                frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 644);
            }
            NativeBattleVector midpoint = NativeBattleDuel112Geometry.Midpoint6CD8B0(checked(0x1D972C0u + 156u * (uint)actor), (_, bone, fraction) => inputs.ReadBone502170(actor, bone, fraction));
            Draw(372, new(0x12DB664, MorphAddress(frame), midpoint, first < 22 ? (short)0 : (short)-1024, null));
            if (first > 4)
            {
                Draw(644, new(0x12DB67C, MorphAddress(frame), midpoint, null, null));
                if (first >= 8)
                {
                    Flash(50, 80, 80, first - 8, 3);
                }
                if (first == 21)
                {
                    inputs.Flash5712C0(0, 1, 1, 96);
                }
                else if (first >= 25)
                {
                    Flash(40, 60, 60, first - 25, 5);
                }
            }
        }
        if (phase == 34)
        {
            inputs.Flash5712C0(0, 1, 1, 96);
        }
        int second = phase - 48;
        if ((uint)second < 50)
        {
            if (second == 0)
            {
                frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 1468);
                short yaw = inputs.Actor.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("Native Duel hierarchy requires its actual actor yaw.");
                NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler56CE30(new(300, yaw, 0));
                task = frame.Effect.Pools[1].Slots[slot];
                for (int word = 0; word < 9; word++)
                {
                    task = task.WriteWord(108 + 2 * word, unchecked((ushort)basis.Rotation[word]));
                }
                Store();
            }
            task = frame.Effect.Pools[1].Slots[slot];
            NativeBattleVector bone21 = inputs.ReadBone502170(actor, 21, 0);
            task = task.WriteWord(84, unchecked((ushort)bone21.X)).WriteWord(86, unchecked((ushort)bone21.Y)).WriteWord(88, unchecked((ushort)bone21.Z));
            task = task.WriteDword(128, unchecked((uint)bone21.X)).WriteDword(132, unchecked((uint)bone21.Y)).WriteDword(136, unchecked((uint)bone21.Z));
            Store();
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(108 + 2 * index))).ToImmutableArray();
            NativeBattleActionMatrix local = new(rotation, unchecked((int)task.ReadDword(128)), unchecked((int)task.ReadDword(132)), unchecked((int)task.ReadDword(136)), 4096);
            NativeBattleActionMatrix context = NativeBattleGfMatrices.Compose56C2F0(inputs.View, local);
            Draw(1468, new(0x12DB6AC, MorphAddress(frame), null, null, context));
            if (second >= 4)
            {
                Flash(80, 60, 30, second - 4, 4);
                if (second == 4)
                {
                    task = frame.Effect.Pools[1].Slots[slot];
                    int target = ModelIndex(task.ReadDword(176));
                    NativeBattleGraphicsModel model = inputs.Actor.ReadModel(target);
                    inputs.Actor.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 | 8) });
                }
            }
        }
        if (phase == 67)
        {
            inputs.Flash5712C0(0, 1, 6, 255);
        }
        return new(frame, ordering);

        void Draw(int offset, NativeBattleDuel112HierarchyContext context)
        {
            NativeBattleDuel112WorkspaceStep step = NativeBattleDuel112Workspaces.Advance701970(frame, slot, offset, context, inputs.View, inputs.Projection, inputs.Colors, ordering);
            frame = step.Frame;
            ordering = step.Ordering;
        }
        void Flash(int red, int green, int blue, int elapsed, int duration)
        {
            NativeBattleDuel112DrawingStep step = Append6CBF90(frame, ordering, NativeBattleDuelSharedPackets.Flash6A83B0(red, green, blue, elapsed, duration));
            frame = step.Frame;
            ordering = step.Ordering;
        }
        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(slot, task)) } };
        }
    }

    public static NativeBattleDuel112DrawingStep Append6CBF90(NativeBattleDuel112Frame frame, NativeBattleLogicalOrdering ordering, NativeBattleModulePacketBatch batch)
    {
        if (batch.AllocationBytes == 0 && batch.Primitives.IsEmpty && batch.CapturedWrites is null)
        {
            return new(frame, ordering);
        }
        int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Duel packet requires its actual draw cursor.");
        uint physical = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2521914, 4));
        if (physical != checked(0x20DFAB8u + (uint)cursor))
        {
            throw new InvalidDataException("Native Duel draw cursor differs from its physical process DWORD.");
        }
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, batch, NativeBattleEffectGouraudWrites.FullDword);
        frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2521914, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
        return new(frame, written.Ordering);
    }

    private static uint MorphAddress(NativeBattleDuel112Frame frame) => unchecked(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x25218E8, 4)) + 0x1808);

    private static int ModelIndex(uint address)
    {
        uint relative = unchecked(address - 0x1D972C0u);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new InvalidDataException("Native Duel requires its actual graphics model pointer.");
        }
        return (int)(relative / 156);
    }
}
