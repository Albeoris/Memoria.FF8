using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleThundaga105TaskStep(NativeBattleThundaga105Frame Frame, NativeBattleModulePoolTask Task);
public abstract record NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105InitializeTextures : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105DebrisDraw : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105FragmentDraw(NativeBattleModulePoolTask Task, NativeBattleStaticMeshResource Mesh) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105Sprite(NativeBattleModulePoolTask Task) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105Capture(ImmutableArray<byte> Anchor, NativeBattleThundaga105Bounds Bounds) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105ActorColor(int Actor, uint Color40) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105StageFlash(int Amount) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105CommonFlash(int First, int Second, int Third, int Fourth) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105Sound(NativeBattleModuleAnchor Position) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105Contact(int Contact) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105LoadCommon(uint DestinationAddress, uint CompletionWordAddress) : NativeBattleThundaga105Operation;
public sealed record NativeBattleThundaga105Inputs(NativeBattleActionPacketsState Packets, Func<int, ImmutableArray<byte>> ReadBone241, Func<int, NativeBattleModuleAnchor> ReadPosition, Func<int, uint> ReadActorColor40, Func<int, short> ReadActorScale38, Func<NativeBattleThundaga105Frame, NativeBattleModulePoolTask, int, ImmutableArray<byte>, NativeBattleThundaga105TaskStep> PrimaryDraw, Func<NativeBattleThundaga105Frame, NativeBattleThundaga105Operation, NativeBattleThundaga105Frame> Execute);

/// <summary>The original four physical task banks and shared fragment/debris owners.</summary>
public static class NativeBattleThundaga105Controller
{
    public const uint MainDescriptor = 0x2543DE0;
    public const uint MainBank = 0x2543D90;
    public const uint PrimaryDescriptor = 0x2543DD0;
    public const uint SecondaryDescriptor = 0x2543DC0;
    public const uint TertiaryDescriptor = 0x2543DB0;
    public const uint ArenaAddress = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint ActorBank = 0x1D972C0;

    public static NativeBattleThundaga105Frame Initialize6D3DE0(NativeBattleThundaga105Frame frame, NativeBattleThundaga105Inputs inputs, NativeBattleThundaga105Resources resources)
    {
        if (frame.Effect.Binding is not { Effect: 105, Entry: 0x6D3DB0, Initializer: 0x6D3DC0 } || frame.Effect.Parameters is null || frame.Memory.Arena is null)
        {
            throw new InvalidDataException("Native Thundaga requires its original entry, parameters and allocated arena.");
        }
        NativeBattleThundaga105State state = (frame.Memory.Thundaga105 ?? throw new NotSupportedException("Native Thundaga requires its retained mutable source owner.")).Materialize(resources);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 0x1C, 1);
        (main, _) = main.Append(0x6D3E50);
        frame = frame with { Memory = frame.Memory with { Thundaga105 = state }, Effect = frame.Effect with { Pools = [main], ReturnedDescriptor = MainDescriptor } };
        if ((frame.Effect.Parameters.Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleThundaga105InitializeTextures());
        }
        return frame;
    }

    public static NativeBattleOriginalEffectState WriteCompletionWord(NativeBattleOriginalEffectState effect, uint address, ushort value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == TertiaryDescriptor);
        long delta = (long)address - pool.BankAddress - 0xE;
        if (delta < 0 || delta % pool.Stride != 0 || delta / pool.Stride >= pool.Slots.Length)
        {
            throw new InvalidDataException("Native Thundaga completion requires its physical tertiary WORD E.");
        }
        int slot = (int)(delta / pool.Stride);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x6D62B0)
        {
            throw new InvalidDataException("Native Thundaga completion requires its live final load task.");
        }
        return effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(pool), Put(pool, slot, task.WriteWord(0xE, value))) };
    }

    public static NativeBattleThundaga105Frame Advance6D3E50(NativeBattleThundaga105Frame frame, NativeBattleThundaga105Inputs inputs, NativeBattleThundaga105Resources resources)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Thundaga lost its action parameters.");
        NativeBattleThundaga105State retained = (frame.Memory.Thundaga105 ?? throw new NotSupportedException("Native Thundaga lost its mutable fragment owner.")).Materialize(resources);
        frame = frame with { Memory = frame.Memory with { Thundaga105 = retained } };
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool? primary = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == PrimaryDescriptor);
        NativeBattleEffectTaskPool? secondary = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == SecondaryDescriptor);
        NativeBattleEffectTaskPool? tertiary = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == TertiaryDescriptor);
        int stage = 0;
        NativeBattleEffectTaskStep result = main.Advance((pool, rootSlot) =>
        {
            main = pool;
            NativeBattleModulePoolTask task = main.Slots[rootSlot];
            if (task.Callback != 0x6D3E50)
            {
                throw new NotSupportedException("Native Thundaga root bank contains an unsupported callback.");
            }
            bool alternate = task.ReadDword(0x18) == 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = alternate ? 0x20C1C : 0x8C1C }, Memory = frame.Memory with { Thundaga105 = State() with { SecondaryWorkspaceArenaOffset = alternate ? 0x38C1C : 0x20C1C } } };
            task = task.WriteDword(0x18, alternate ? 1u : 0u);
            if (task.ReadWord(0xC) == 1)
            {
                if (task.ReadByte(0x14) == 0)
                {
                    primary = NativeBattleEffectTaskPool.Initialize(PrimaryDescriptor, ArenaAddress, 0x884, 3);
                    secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, ArenaAddress + 0x198C, 0x3C, 14);
                    tertiary = NativeBattleEffectTaskPool.Initialize(TertiaryDescriptor, ArenaAddress + 0x1CD4, 0x2C, 32);
                    frame = frame with { Memory = NativeBattleThundaga105Debris.ClearAlive(frame.Memory) };
                    task = task.WriteByte(0x14, 1);
                }
                int sequence = task.ReadByte(0xE);
                if (sequence <= Header(0).ReadByte(17) && HasFree(primary!))
                {
                    int actor = Actor(sequence);
                    (primary, int slot) = primary!.Append(0x6D43B0);
                    NativeBattleModulePoolTask child = primary.Slots[slot] with { Payload = NativeBattleTelemetry.Immutable(1246, new byte[0x884 - 12]) };
                    child = child.WriteWord(0xE, (ushort)sequence).WriteDword(0x10, ActorBank + (uint)(156 * actor)).WriteDword(0x14, inputs.ReadActorColor40(actor)).WriteDword(0x18, unchecked((uint)(((inputs.ReadActorScale38(actor) >> 1) + 3718) >> 1)));
                    child = NativeBattleThundaga105ProgramDrawing.InitializeWorkspace7016B0(child, resources.ProgramSource);
                    primary = Put(primary, slot, child);
                    task = task.WriteDword(0x10, primary.BankAddress + (uint)(slot * primary.Stride)).WriteByte(0xE, unchecked((byte)(sequence + 1)));
                }
            }
            int debrisCount = 1;
            int firstCount = 1;
            int secondCount = 1;
            int thirdCount = 1;
            if (task.ReadByte(0x14) == 1)
            {
                NativeBattleThundaga105DebrisUpdate update = NativeBattleThundaga105Debris.Update6D40E0(frame.Memory);
                frame = frame with { Memory = update.Memory };
                debrisCount = update.Survivors;
                Emit(new NativeBattleThundaga105DebrisDraw());
            }
            if (task.ReadByte(0x14) != 0)
            {
                NativeBattleEffectTaskStep first = primary!.Advance(Primary);
                primary = first.Pool;
                firstCount = first.Survivors;
                NativeBattleEffectTaskStep second = secondary!.Advance(Secondary);
                secondary = second.Pool;
                secondCount = second.Survivors;
                NativeBattleEffectTaskStep third = tertiary!.Advance(Tertiary);
                tertiary = third.Pool;
                thirdCount = third.Survivors;
            }
            Emit(new NativeBattleThundaga105StageFlash(stage));
            if (task.ReadByte(0x14) == 1 && debrisCount + firstCount + secondCount + thirdCount == 0 && task.ReadByte(0xE) > Header(0).ReadByte(17))
            {
                if ((parameters.Flags79 & 2) != 0)
                {
                    return new(Put(main, rootSlot, task), 2);
                }
                if (!HasFree(tertiary!))
                {
                    throw new InvalidOperationException("Native Thundaga final load task exhausted its tertiary bank.");
                }
                (tertiary, int finalSlot) = tertiary!.Append(0x6D62B0);
                tertiary = Put(tertiary, finalSlot, tertiary.Slots[finalSlot] with { Payload = NativeBattleTelemetry.Immutable(1247, new byte[0x20]) });
                task = task.WriteByte(0x14, 2);
            }
            else if (task.ReadByte(0x14) == 2 && thirdCount == 0)
            {
                return new(Put(main, rootSlot, task), 2);
            }
            task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            if (unchecked((short)task.ReadWord(0xC)) >= 46)
            {
                task = task.WriteWord(0xC, 0);
            }
            return new(Put(main, rootSlot, task), 0);
        });
        main = result.Pool;
        frame = Publish();
        return frame with { Effect = frame.Effect with { ReturnedDescriptor = result.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Primary(NativeBattleEffectTaskPool pool, int slot)
        {
            primary = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6D43B0)
            {
                throw new NotSupportedException("Native Thundaga primary callback is unsupported.");
            }
            int phase = task.ReadWord(0xC);
            int sequence = task.ReadWord(0xE);
            int actor = ActorIndex(task.ReadDword(0x10));
            ImmutableArray<byte> anchor = inputs.ReadBone241(actor);
            if (anchor.IsDefault || anchor.Length != 8)
            {
                throw new InvalidDataException("Native Thundaga requires its complete actual bone241 raw anchor.");
            }
            NativeBattleThundaga105TaskStep drawn = inputs.PrimaryDraw(Publish(), task, actor, anchor);
            frame = drawn.Frame;
            task = drawn.Task;
            foreach (int start in new[] { 2, 30, 39 })
            {
                if (phase >= start && phase <= start + 2)
                {
                    uint saved = task.ReadDword(0x14);
                    uint color = phase == start ? (inputs.ReadActorColor40(actor) & 0xFF000000) | 0xFFFFFF : phase == start + 2 ? saved : HalfColor(saved, inputs.ReadActorColor40(actor));
                    Emit(new NativeBattleThundaga105ActorColor(actor, color));
                }
            }
            stage = Math.Max(stage, phase < 4 ? phase * 512 : phase <= 60 ? 2048 : (64 - phase) * 512);
            if (phase == 0)
            {
                Emit(new NativeBattleThundaga105Sound(inputs.ReadPosition(actor)));
            }
            if (phase is 3 or 30 or 39)
            {
                Emit(phase == 3 ? new NativeBattleThundaga105CommonFlash(0, 1, 0, 128) : phase == 30 ? new NativeBattleThundaga105CommonFlash(1, 1, 6, 128) : new NativeBattleThundaga105CommonFlash(0, 1, 0, 80));
            }
            if (phase == 28)
            {
                NativeBattleThundaga105Bounds bounds = NativeBattleThundaga105Bounds.Begin6D5FA0();
                for (int index = 0; index < 14; index++)
                {
                    if (!HasFree(secondary!))
                    {
                        continue;
                    }
                    (secondary, int child) = secondary!.Append(0x6D5AD0);
                    NativeBattleThundaga105FragmentInitialization initialized = NativeBattleThundaga105Fragments.Initialize6D5990(secondary.Slots[child], resources.FragmentPairs[index], State().MutableFragments[index], Vector(anchor), unchecked((int)task.ReadDword(0x18)), bounds, frame.Random);
                    secondary = Put(secondary, child, initialized.Task);
                    frame = frame with { Random = initialized.Random, Memory = frame.Memory with { Thundaga105 = State() with { MutableFragments = State().MutableFragments.SetItem(index, initialized.Mesh) } } };
                    bounds = initialized.Bounds;
                }
                Emit(new NativeBattleThundaga105Capture(anchor, bounds));
            }
            if (phase is 34 or 36 or 38 or 40)
            {
                frame = frame with { Random = frame.Random.Draw(out ushort angle) };
                for (int index = 0; index < 8; index++)
                {
                    if (!HasFree(tertiary!))
                    {
                        continue;
                    }
                    int theta = MovingAngle(angle, index);
                    short cosine = unchecked((short)NativeBattleCameraMath.Cosine(theta));
                    short sine = unchecked((short)NativeBattleCameraMath.Sine(theta));
                    (tertiary, int child) = tertiary!.Append(0x6D6100);
                    NativeBattleModulePoolTask sprite = ClearSprite(tertiary.Slots[child], resources.SpriteMoving.Address, 12).WriteWord(0x14, unchecked((ushort)(Signed(anchor, 0) + (cosine >> 2)))).WriteWord(0x16, unchecked((ushort)Signed(anchor, 2))).WriteWord(0x18, unchecked((ushort)(Signed(anchor, 4) + (sine >> 2))));
                    frame = frame with { Random = frame.Random.Draw(out ushort velocity) };
                    short[] speed = [unchecked((short)(cosine >> 8)), unchecked((short)(-256 - (velocity & 127))), unchecked((short)(sine >> 8))];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        sprite = sprite.WriteWord(0x1C + axis * 2, unchecked((ushort)speed[axis])).WriteWord(0x24 + axis * 2, unchecked((ushort)(-speed[axis] / 10)));
                    }
                    tertiary = Put(tertiary, child, sprite);
                }
            }
            if (phase is 45 or 47 or 49 or 51 or 53 or 55)
            {
                NativeBattleModuleAnchor position = inputs.ReadPosition(actor);
                if (HasFree(tertiary!))
                {
                    (tertiary, int child) = tertiary!.Append(0x6D61C0);
                    NativeBattleModulePoolTask sprite = ClearSprite(tertiary.Slots[child], resources.SpriteStationary.Address, 15);
                    short[] coordinates = [position.X, position.Y, position.Z];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort offset) };
                        sprite = sprite.WriteWord(0x14 + axis * 2, unchecked((ushort)(coordinates[axis] + (offset & 2047) - 1024)));
                    }
                    tertiary = Put(tertiary, child, sprite);
                }
            }
            if (phase >= 64)
            {
                Emit(new NativeBattleThundaga105Contact(Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Thundaga requires its contact owner.")));
                return new(Put(primary!, slot, task), 2);
            }
            return new(Put(primary!, slot, task.WriteWord(0xC, unchecked((ushort)(phase + 1)))), 0);
        }

        NativeBattleEffectTaskCallback Secondary(NativeBattleEffectTaskPool pool, int slot)
        {
            secondary = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x6D5AD0)
            {
                throw new NotSupportedException("Native Thundaga fragment callback is unsupported.");
            }
            NativeBattleStaticMeshResource mesh = State().MutableFragments.Single(mesh => mesh.Address == task.ReadDword(0x38));
            Emit(new NativeBattleThundaga105FragmentDraw(task, mesh));
            NativeBattleThundaga105FragmentStep step = NativeBattleThundaga105Fragments.Step6D5AD0(task);
            if (step.Complete)
            {
                NativeBattleThundaga105FragmentEmission emitted = NativeBattleThundaga105Fragments.EmitDebris6D5C00(frame.Memory, step.Task, mesh, frame.Random);
                frame = frame with { Memory = emitted.Memory, Random = emitted.Random };
            }
            return new(Put(secondary!, slot, step.Task), step.Complete ? 2 : 0);
        }

        NativeBattleEffectTaskCallback Tertiary(NativeBattleEffectTaskPool pool, int slot)
        {
            tertiary = pool;
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback == 0x6D62B0)
            {
                if (task.ReadWord(0xC) == 0)
                {
                    Emit(new NativeBattleThundaga105LoadCommon(ArenaAddress + 0x8C1C, pool.BankAddress + (uint)(slot * pool.Stride) + 0xE));
                    tertiary = frame.Effect.Pools.Single(candidate => candidate.DescriptorAddress == TertiaryDescriptor);
                    task = tertiary.Slots[slot];
                }
                task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
                return new(Put(tertiary!, slot, task), task.ReadWord(0xE) != 0 ? 2 : 0);
            }
            if (task.Callback is not (0x6D6100 or 0x6D61C0))
            {
                throw new NotSupportedException("Native Thundaga sprite callback is unsupported.");
            }
            Emit(new NativeBattleThundaga105Sprite(task));
            if (task.Callback == 0x6D6100)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(S(task, 0x14 + axis * 2) + S(task, 0x1C + axis * 2))));
                    task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)(S(task, 0x1C + axis * 2) + S(task, 0x24 + axis * 2))));
                }
            }
            task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            return new(Put(tertiary!, slot, task), S(task, 0xC) >= S(task, 0xE) ? 2 : 0);
        }

        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[parameters.HeaderIndex7C + sequence];
        int Actor(int sequence) => inputs.Packets.ReadContact(Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Thundaga requires its physical contact.")).Primary.ActorIndex;
        NativeBattleThundaga105State State() => frame.Memory.Thundaga105 ?? throw new NotSupportedException("Native Thundaga lost its retained source state.");
        NativeBattleThundaga105Frame Publish() => frame with { Effect = frame.Effect with { Pools = primary is null ? [main] : [main, primary, secondary!, tertiary!] } };
        void Emit(NativeBattleThundaga105Operation operation)
        {
            frame = inputs.Execute(Publish(), operation);
        }
    }

    internal static int MovingAngle(ushort random, int direction) => random + direction * 512;

    private static NativeBattleModulePoolTask ClearSprite(NativeBattleModulePoolTask task, uint address, ushort duration) => (task with { Payload = NativeBattleTelemetry.Immutable(1248, new byte[0x20]) }).WriteDword(0x10, address).WriteWord(0xE, duration).WriteWord(0x1A, 4096);
    private static uint HalfColor(uint saved, uint current) => (current & 0xFF000000) | (((saved & 255) + 255) >> 1) | (((((saved >> 8) & 255) + 255) >> 1) << 8) | (((((saved >> 16) & 255) + 255) >> 1) << 16);
    private static int ActorIndex(uint address) => address >= ActorBank && address < ActorBank + 7 * 156 && (address - ActorBank) % 156 == 0 ? (int)((address - ActorBank) / 156) : throw new InvalidDataException("Native Thundaga requires its actual seven-row actor address.");
    private static NativeBattleVector Vector(ImmutableArray<byte> raw) => new(Signed(raw, 0), Signed(raw, 2), Signed(raw, 4));
    private static short Signed(ImmutableArray<byte> raw, int offset) => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan().Slice(offset));
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static bool HasFree(NativeBattleEffectTaskPool pool) => pool.Slots.Any(task => (task.Flags & 1) == 0);
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, task) };
}
