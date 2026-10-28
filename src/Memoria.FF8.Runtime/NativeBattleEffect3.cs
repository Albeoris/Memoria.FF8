using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect3Resources(NativeBattleEffectHeapAllocation Texture, NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound, uint FlashColor);
public sealed record NativeBattleEffect3Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleVector> ReadBone241, Func<int, NativeBattleModelTransform> ReadTransform)
{
    public Action<int>? ApplyContact { get; init; }
}
public sealed record NativeBattleEffect3Sound(uint Sound, uint Flags, NativeBattleModuleAnchor Position);
public sealed record NativeBattleEffect3Step(NativeBattleOriginalEffectState Effect, ImmutableArray<NativeBattleModuleDraw> Draws, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleEffect3Sound> Sounds);
public readonly record struct NativeBattleScreenPoint(short X, short Y);

public sealed partial record NativeBattleModuleDraw
{
    /// <summary>Explicit camera-space Z translation after the native world/camera composition.</summary>
    public int SpriteCameraZOffset { get; init; }
    public byte? NativePrimitiveCommand { get; init; }
    public int? NativeOrderingBucket { get; init; }
    public int? NativeEffectArenaOffset { get; init; }
    public ImmutableArray<NativeBattleScreenPoint> NativeScreenVertices { get; init; } = [];
}

/// <summary>Original effect 3's 700C10/700C20 entry and 700CA0/700DE0 callbacks.</summary>
public static class NativeBattleEffect3
{
    public static NativeBattleOriginalEffectState InitializePools(NativeBattleOriginalEffectState effect)
    {
        effect.Validate();
        if (effect.Binding.Entry != 0x700C10 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native effect3 requires its original700C10 entry and50AFC0 parameters.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x2557078, 0x2557060, 20, 1);
        (main, int slot) = main.Append(0x700CA0);
        main = main.WriteSlot(slot, main.Slots[slot].ClearPayload(8));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x2557088, 0x2557000, 28, 3);
        return effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress, DrawArenaOffset = null };
    }

    public static NativeBattleEffect3Step Step(NativeBattleOriginalEffectState effect, NativeBattleEffect3Inputs inputs)
    {
        effect.Validate();
        inputs.Packets.Validate();
        if (effect.Binding.Entry != 0x700C10 || effect.Pools.Length != 2 || effect.Pools[0].DescriptorAddress != 0x2557078 || effect.Pools[0].BankAddress != 0x2557060 || effect.Pools[0].Stride != 20 || effect.Pools[0].Slots.Length != 1 || effect.Pools[1].DescriptorAddress != 0x2557088 || effect.Pools[1].BankAddress != 0x2557000 || effect.Pools[1].Stride != 28 || effect.Pools[1].Slots.Length != 3 || effect.ReturnedDescriptor is not (0 or 0x2557078))
        {
            throw new InvalidDataException("Native effect3 requires its original1x20 and3x28 physical task banks.");
        }
        NativeBattleEffect3Resources resources = effect.Binding.Effect3Resources ?? throw new NotSupportedException("Native effect3 requires its original embedded TIM, camera, sound and sprite banks.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native effect3 lacks its50AFC0 parameters.");
        if (effect.ReturnedDescriptor == 0)
        {
            return new(effect, [], [], []);
        }
        NativeBattleEffectTaskPool children = effect.Pools[1];
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleEffect3Sound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleEffect3Sound>();
        int? drawArenaOffset = effect.DrawArenaOffset;
        NativeBattlePilotVisualIdentity? drawingIdentity = null;
        NativeBattleEffectTaskStep main = effect.Pools[0].Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x700CA0)
            {
                throw new NotSupportedException("Native effect3 main task lost its700CA0 callback.");
            }
            uint buffer = BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(4));
            drawArenaOffset = buffer != 0 ? 0 : 0x8000;
            task.WriteDword(16, buffer != 0 ? 0u : 1u);
            if (Word(task, 12) == 1)
            {
                task.WriteByte(15, 1);
                byte sequence = task.ReadByte(14);
                if (sequence <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
                {
                    int first = FirstContact(Header(sequence));
                    int model = inputs.Packets.ReadContact(first).Primary.ActorIndex;
                    uint modelAddress = checked(0x1D972C0u + 156u * (uint)model);
                    bool busy = children.Slots.Any(child => (child.Flags & 1) != 0 && BinaryPrimitives.ReadUInt32LittleEndian(child.ReadRawPayloadSpan().Slice(4)) == modelAddress && Word(child, 12) <= 15);
                    if (busy)
                    {
                        task.WriteWord(12, 0);
                    }
                    else
                    {
                        (children, int childSlot) = children.Append(0x700DE0);
                        byte[] childPayload = new byte[16];
                        BinaryPrimitives.WriteUInt16LittleEndian(childPayload.AsSpan(2), sequence);
                        BinaryPrimitives.WriteUInt32LittleEndian(childPayload.AsSpan(4), modelAddress);
                        NativeBattleVector anchor = inputs.ReadBone241(model);
                        BinaryPrimitives.WriteInt16LittleEndian(childPayload.AsSpan(8), anchor.X);
                        BinaryPrimitives.WriteInt16LittleEndian(childPayload.AsSpan(10), anchor.Y);
                        BinaryPrimitives.WriteInt16LittleEndian(childPayload.AsSpan(12), anchor.Z);
                        BinaryPrimitives.WriteInt16LittleEndian(childPayload.AsSpan(14), unchecked((short)((-4096 - inputs.ReadTransform(model).Scale38) >> 2)));
                        children = children.WriteSlot(childSlot, children.Slots[childSlot].WriteBytes(12, childPayload));
                        task.WriteByte(14, unchecked((byte)(sequence + 1)));
                    }
                }
            }
            NativeBattleEffectTaskStep childStep = children.Advance(AdvanceChild);
            children = childStep.Pool;
            bool released = task.ReadByte(15) != 0 && childStep.Survivors == 0;
            if (!released)
            {
                short counter = unchecked((short)(unchecked((short)task.ReadWord(12)) + 1));
                task.WriteWord(12, unchecked((ushort)(counter >= 5 ? (short)0 : counter)));
            }
            return new(pool.WriteSlot(slot, task), released ? 2 : 0);
        });
        return new(effect with { Pools = [main.Pool, children], ReturnedDescriptor = main.Survivors == 0 ? 0 : main.Pool.DescriptorAddress, DrawArenaOffset = drawArenaOffset }, draws.ToImmutable(), contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectTaskCallback AdvanceChild(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            drawingIdentity = new(3, pool.DescriptorAddress, slot, NativeBattlePilotVisualLifetimes.Generation(task, slot), 0);
            if (task.Callback != 0x700DE0)
            {
                throw new NotSupportedException("Native effect3 child lost its700DE0 callback.");
            }
            short frame = Word(task, 12);
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(4));
            uint relative = unchecked(address - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= 7)
            {
                throw new InvalidDataException("Native700DE0 model pointer leaves the original seven-row bank.");
            }
            int model = (int)(relative / 156);
            NativeBattleModuleAnchor anchor = new(Word(task, 20), Word(task, 22), Word(task, 24), 0);
            int depth = Word(task, 26);
            if (frame == 0)
            {
                if (resources.FlashColor != 0x808080)
                {
                    throw new NotSupportedException("Native7011A0 requires its authenticated monochrome808080 fullscreen polygon.");
                }
                int flashCursor = drawArenaOffset ?? throw new NotSupportedException("Native7011A0 requires the selected700CA0 private draw cursor.");
                draws.Add(new(NativeBattleModuleDrawKind.Flash, 128) { NativePrimitiveCommand = 0x2A, NativeOrderingBucket = 8, NativeEffectArenaOffset = flashCursor, NativeScreenVertices = [new(0, 0), new(320, 0), new(0, 216), new(320, 216)] });
                drawArenaOffset = checked(flashCursor + 24);
            }
            if (frame >= 1)
            {
                int phase = frame - 1;
                if (phase == 0)
                {
                    sounds.Add(new(resources.Sound, 0x101, anchor));
                }
                Draw(0x1522FB0, 0, 7, phase, anchor, depth);
                Draw(0x1523204, 2, 7, phase, anchor with { Y = unchecked((short)(anchor.Y + 200)) }, depth);
                Draw(0x1523458, 1, 9, phase, anchor, depth);
                Draw(0x1523578, 3, 9, phase, anchor, depth);
                Draw(0x1523684, 6, 9, phase, anchor, depth);
                Draw(0x15237A4, 1, 9, phase, anchor, depth);
                if (phase is >= 1 and < 14)
                {
                    Draw(0x1523DE0, 1, 13, phase, anchor with { Y = inputs.ReadTransform(model).Angle36 }, 0, true);
                }
                Draw(0x1523928, 5, 19, phase, anchor, depth);
            }
            if (frame == 5)
            {
                int first = FirstContact(Header(Word(task, 14)));
                contacts.Add(first);
                inputs.ApplyContact?.Invoke(first);
            }
            task = task.WriteWord(12, unchecked((ushort)(frame + 1)));
            return new(pool.WriteSlot(slot, task), Word(task, 12) >= 25 ? 2 : 0);
        }

        void Draw(uint address, int first, int count, int phase, NativeBattleModuleAnchor anchor, int depth, bool ground = false)
        {
            if (phase < first || phase >= first + count)
            {
                return;
            }
            NativeBattleModuleResource resource = resources.Sprites.Single(resource => resource.Address == address);
            draws.Add(new(NativeBattleModuleDrawKind.Sprite, 0) { PilotIdentity = drawingIdentity is { } identity ? identity with { Primitive = unchecked((int)address) } : null, PilotContinuous = false, Resource = resource, SpriteFrame = resource.SpriteFrame(phase - first), Phase = (ushort)(phase - first), Anchor = anchor, Scale = 4096, DepthOffset = depth, Direction = 4096, SpriteFlags = 0, SpriteWorldRotation = ground ? NativeBattlePoseMatrices.RotationWords(new(90, 0, 0)) : [], SpriteCameraZOffset = ground ? 500 : 0 });
        }

        NativeBattleActionHeaderSlot Header(int sequence)
        {
            int index = parameters.HeaderIndex7C + sequence;
            if ((uint)index >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new InvalidDataException("Native effect3 continuation leaves its physical action header bank.");
            }
            return inputs.Packets.Headers[index];
        }
    }

    private static int FirstContact(NativeBattleActionHeaderSlot header) => header.FirstContactIndex ?? throw new InvalidDataException("Native effect3 requires its original physical contact pointer.");
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
