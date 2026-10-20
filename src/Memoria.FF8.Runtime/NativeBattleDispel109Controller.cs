using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleDispel109OperationKind
{
    Flash,
    Preset,
    Sound,
    Contact
}

public sealed record NativeBattleDispel109Operation(NativeBattleDispel109OperationKind Kind, int Value, NativeBattleModuleAnchor? Anchor = null);
public sealed record NativeBattleDispel109Inputs(NativeBattleActionPacketsState Packets, Func<byte, NativeBattleModuleAnchor> ReadAnchor, Func<byte, ushort> ReadHeight, Func<NativeBattleDispel109Frame, NativeBattleDispel109Operation, NativeBattleDispel109Frame> Execute, Func<NativeBattleDispel109Frame, NativeBattleModulePoolTask, NativeBattleGfAnimationNode, NativeBattleDispel109Frame> DrawModel, Func<NativeBattleDispel109Frame, NativeBattleModulePoolTask, NativeBattleDispel109Frame> DrawSprite)
{
    public Action? ObserveTargetInitialize6CF687 { get; init; }
}
public sealed record NativeBattleDispel109LeafStep(NativeBattleDispel109Frame Frame, NativeBattleModulePoolTask Task, int Result);

public static class NativeBattleDispel109Controller
{
    public static NativeBattleDispel109Frame Initialize6CF4D0(NativeBattleDispel109Frame frame)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521AB0, 0x2521A78, 20, 1);
        (root, int slot) = root.Append(0x6CF540);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0)) };
        return frame with { Effect = frame.Effect with { Dispel109 = new(0), Pools = [root], ReturnedDescriptor = 0x2521AB0, DrawArenaOffset = null } };
    }

    public static NativeBattleDispel109Frame Advance6CF540(NativeBattleDispel109Frame frame, NativeBattleDispel109Resources resources, NativeBattleDispel109Inputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        int buffer = frame.Effect.Dispel109?.BufferOffset ?? throw new NotSupportedException("Native Dispel requires its executed fixed-base initializer.");
        NativeBattleEffectTaskPool roots = frame.Effect.Pools[0];
        NativeBattleModulePoolTask root = roots.Slots[0];
        bool second = root.ReadDword(16) != 0;
        root = root.WriteDword(16, second ? 0u : 1u);
        frame = frame with { Effect = frame.Effect with { DrawArenaOffset = checked(buffer + (second ? 4320 : 69856)) } };
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                root = root.WriteByte(15, 1);
                frame = frame with { Effect = frame.Effect with { Pools = [roots, NativeBattleEffectTaskPool.Initialize(0x2521AA0, checked(0x20DFAB8u + (uint)buffer + 1792), 840, 3), NativeBattleEffectTaskPool.Initialize(0x2521A90, checked(0x20DFAB8u + (uint)buffer), 28, 64)] } };
            }
            int headerIndex = frame.Effect.Parameters?.HeaderIndex7C ?? throw new InvalidDataException("Native Dispel requires its current action header.");
            byte group = root.ReadByte(14);
            if (group <= inputs.Packets.Headers[headerIndex].ReadByte(17))
            {
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[checked(headerIndex + group)];
                int contact = header.FirstContactIndex ?? throw new InvalidDataException("Native Dispel requires the actual group contact pointer.");
                byte actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                uint actorPointer = checked(0x1D972C0u + (uint)actor * 156);
                NativeBattleEffectTaskPool targets = frame.Effect.Pools[1];
                if (targets.Slots.Any(task => (task.Flags & 1) != 0 && task.ReadDword(20) == actorPointer && unchecked((short)task.ReadWord(12)) <= 67))
                {
                    root = root.WriteWord(12, 0);
                }
                else
                {
                    (targets, int slot) = targets.Append(0x6CF700);
                    NativeBattleModulePoolTask task = ClearPayload(targets.Slots[slot]).WriteWord(14, group).WriteDword(20, actorPointer);
                    NativeBattleModuleAnchor anchor = inputs.ReadAnchor(actor);
                    task = task.WriteWord(24, unchecked((ushort)anchor.X)).WriteWord(26, unchecked((ushort)anchor.Y)).WriteWord(28, unchecked((ushort)anchor.Z)).WriteWord(30, unchecked((ushort)(3 * (inputs.ReadHeight(actor) + 256))));
                    NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(20, 808));
                    inputs.ObserveTargetInitialize6CF687?.Invoke();
                    task = StoreWorkspace(task, workspace.Initialize7016B0(resources.Animation, 808));
                    targets = targets with { Slots = targets.Slots.SetItem(slot, task) };
                    frame = SetPool(frame, 1, targets);
                    root = root.WriteByte(14, unchecked((byte)(group + 1)));
                }
            }
        }
        int targetSurvivors = 1;
        int sparkSurvivors = 1;
        if (root.ReadByte(15) != 0)
        {
            NativeBattleEffectTaskStep targets = frame.Effect.Pools[1].Advance((pool, slot) =>
            {
                frame = SetPool(frame, 1, pool);
                NativeBattleDispel109LeafStep leaf = Advance6CF700(frame, pool.Slots[slot], resources, inputs);
                frame = leaf.Frame;
                NativeBattleEffectTaskPool updated = frame.Effect.Pools[1];
                return new(updated with { Slots = updated.Slots.SetItem(slot, leaf.Task) }, leaf.Result);
            });
            frame = SetPool(frame, 1, targets.Pool);
            targetSurvivors = targets.Survivors;
            NativeBattleEffectTaskStep sparks = frame.Effect.Pools[2].Advance((pool, slot) =>
            {
                frame = SetPool(frame, 2, pool);
                NativeBattleDispel109LeafStep leaf = Advance6CFC50(frame, pool.Slots[slot], inputs);
                frame = leaf.Frame;
                NativeBattleEffectTaskPool updated = frame.Effect.Pools[2];
                return new(updated with { Slots = updated.Slots.SetItem(slot, leaf.Task) }, leaf.Result);
            });
            frame = SetPool(frame, 2, sparks.Pool);
            sparkSurvivors = sparks.Survivors;
        }
        bool complete = root.ReadByte(15) != 0 && targetSurvivors == 0 && sparkSurvivors == 0;
        if (!complete)
        {
            short next = unchecked((short)(root.ReadWord(12) + 1));
            root = root.WriteWord(12, next >= 10 ? (ushort)0 : unchecked((ushort)next));
        }
        roots = roots with { Slots = roots.Slots.SetItem(0, root) };
        if (complete)
        {
            roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
        }
        frame = SetPool(frame, 0, roots);
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = complete ? 0u : 0x2521AB0u } };
    }

    public static int Flash(short age) => age < 8 ? 2300 * age / 8 : age < 60 ? 2300 : 2300 * (72 - age) / 12;

    public static NativeBattleDispel109LeafStep Advance6CF700(NativeBattleDispel109Frame frame, NativeBattleModulePoolTask task, NativeBattleDispel109Resources resources, NativeBattleDispel109Inputs inputs)
    {
        short age = unchecked((short)task.ReadWord(12));
        frame = inputs.Execute(frame, new(NativeBattleDispel109OperationKind.Flash, Flash(age)));
        uint sparkAge = unchecked((uint)(age - 26));
        if (sparkAge < 16 && (sparkAge & 1) == 0)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort r1).Draw(out ushort r2).Draw(out ushort r3).Draw(out ushort r4).Draw(out ushort r5);
            short scale = unchecked((short)task.ReadWord(30));
            int x = unchecked((short)task.ReadWord(24)) + (unchecked((short)((uint)unchecked(scale * (r1 + r2 - 32768)) >> 16)) >> 2);
            int y = unchecked((short)task.ReadWord(26)) - (r3 >> 6) + 256;
            int z = unchecked((short)task.ReadWord(28)) + (unchecked((short)((uint)unchecked(scale * (r4 + r5 - 32768)) >> 16)) >> 2);
            frame = AppendSprite(frame with { Random = random }, x, y, z, 1024, 16, 0x12E3440);
        }
        uint swirlAge = unchecked((uint)(age - 23));
        if (swirlAge < 6 && (swirlAge & 1) == 0)
        {
            int angle = (int)(swirlAge << 12) / 6;
            frame = AppendSprite(frame, unchecked((short)task.ReadWord(24)) + (NativeBattleCameraMath.Sine(angle) >> 4), unchecked((short)task.ReadWord(26)) - (angle >> 3) + 256, unchecked((short)task.ReadWord(28)) + (NativeBattleCameraMath.Cosine(angle) >> 4), 2048, 7, 0x12E337C);
        }
        if (age == 27)
        {
            frame = inputs.Execute(frame, new(NativeBattleDispel109OperationKind.Preset, 96));
        }
        if (age >= 0)
        {
            NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(20, 808));
            NativeBattleGfAnimationAdvance played = workspace.Advance701970(resources.Animation, 0, (current, node) =>
            {
                frame = inputs.DrawModel(frame, task, node);
                return current;
            });
            task = StoreWorkspace(task, played.Workspace);
            if (age == 0)
            {
                byte actor = Actor(task);
                frame = inputs.Execute(frame, new(NativeBattleDispel109OperationKind.Sound, 257, inputs.ReadAnchor(actor)));
            }
        }
        if (age == 67)
        {
            frame = inputs.Execute(frame, new(NativeBattleDispel109OperationKind.Contact, unchecked((short)task.ReadWord(14))));
        }
        if (age < 72)
        {
            return new(frame, task.WriteWord(12, unchecked((ushort)(age + 1))), 0);
        }
        frame = inputs.Execute(frame, new(NativeBattleDispel109OperationKind.Flash, 0));
        return new(frame, task, 2);
    }

    public static NativeBattleDispel109LeafStep Advance6CFC50(NativeBattleDispel109Frame frame, NativeBattleModulePoolTask task, NativeBattleDispel109Inputs inputs)
    {
        frame = inputs.DrawSprite(frame, task);
        ushort age = unchecked((ushort)(task.ReadWord(12) + 1));
        return new(frame, task.WriteWord(12, age), unchecked((short)age) < unchecked((short)task.ReadWord(14)) ? 0 : 2);
    }

    private static byte Actor(NativeBattleModulePoolTask task)
    {
        uint pointer = task.ReadDword(20);
        if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= 7)
        {
            throw new InvalidDataException("Native Dispel target lost its current actor pointer.");
        }
        return (byte)((pointer - 0x1D972C0) / 156);
    }

    private static NativeBattleDispel109Frame AppendSprite(NativeBattleDispel109Frame frame, int x, int y, int z, ushort scale, ushort life, uint resource)
    {
        NativeBattleEffectTaskPool sparks = frame.Effect.Pools[2];
        (sparks, int slot) = sparks.Append(0x6CFC50);
        NativeBattleModulePoolTask spark = ClearPayload(sparks.Slots[slot]).WriteWord(14, life).WriteWord(16, unchecked((ushort)x)).WriteWord(18, unchecked((ushort)y)).WriteWord(20, unchecked((ushort)z)).WriteWord(22, scale).WriteDword(24, resource);
        return SetPool(frame, 2, sparks with { Slots = sparks.Slots.SetItem(slot, spark) });
    }

    private static NativeBattleModulePoolTask ClearPayload(NativeBattleModulePoolTask task) => (task with { Payload = ImmutableArray.CreateRange(new byte[task.Payload.Length]) }).WithoutResourcePointers(12, task.Payload.Length).WithoutUnknownPayload(12, task.Payload.Length);

    private static NativeBattleModulePoolTask StoreWorkspace(NativeBattleModulePoolTask task, NativeBattleGfAnimationWorkspace workspace)
    {
        byte[] payload = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(payload.AsSpan(20));
        return (task with { Payload = payload.ToImmutableArray() }).WithoutUnknownPayload(32, 808).WithoutResourcePointers(32, 808);
    }

    private static NativeBattleDispel109Frame SetPool(NativeBattleDispel109Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
}
