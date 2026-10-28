using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFireInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, NativeCrtRandomState CrtRandom, Func<int, NativeBattleModuleAnchor> ReadModelAnchor, Func<int, NativeBattleModelTransform> ReadTransform, ImmutableArray<NativeBattleModuleResource> Resources)
{
    public Action<int>? ApplyContact { get; init; }
}
public sealed record NativeBattleFireSound(uint DescriptorAddress, int Volume, int? Pan, NativeBattleModuleAnchor? Position);
public sealed record NativeBattleFireStep(NativeBattleOriginalEffectState Effect, NativeCrtRandomState CrtRandomAfter, ImmutableArray<NativeBattleModuleDraw> Draws, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleFireSound> Sounds);

public static partial class NativeBattleFireEffect
{
    public static NativeBattleFireStep Step(NativeBattleOriginalEffectState effect, NativeBattleFireInputs inputs)
    {
        effect.Validate();
        inputs.Packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Fire callbacks require their50AFC0 parameters.");
        ImmutableArray<int> completion = effect.FireModelCompletion ?? throw new NotSupportedException("Native Fire requires its original seven DEE360 completion DWORDs.");
        NativeCrtRandomState random = inputs.CrtRandom;
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        ImmutableArray<int>.Builder contacts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<NativeBattleFireSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleFireSound>();
        bool frozen = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattlePilotVisualIdentity? drawingIdentity = null;
        NativeBattleOriginalEffectState advanced = Advance(effect, Callback);
        return new(advanced with { FireModelCompletion = completion }, random, draws.ToImmutable(), contacts.ToImmutable(), sounds.ToImmutable());

        NativeBattleEffectTaskCallback Callback(NativeBattleEffectTaskPool pool, int slot, uint drawBuffer)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            drawingIdentity = new(2, pool.DescriptorAddress, slot, NativeBattlePilotVisualLifetimes.Generation(task, slot), 0);
            short phase = Word(task, 12);
            short delay = Word(task, 14);
            if (task.Callback is 0x62A380 or 0x629990)
            {
                if (frozen)
                {
                    return new(pool, 0);
                }
                int sequence = delay;
                if (Word(task, 34) == 12 && sequence + 1 <= inputs.Packets.Headers[parameters.HeaderIndex7C].ReadByte(17))
                {
                    NativeBattleActionHeaderSlot following = Header(sequence + 1);
                    bool multiple = following.ReadByte(16) > 1;
                    (pool, int nextSlot) = pool.Append(multiple ? 0x629990u : 0x62A380u);
                    NativeBattleModulePoolTask next = pool.Slots[nextSlot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(sequence + 1))).WriteWord(34, 0);
                    if (!multiple)
                    {
                        next = next.WriteWord(32, inputs.Packets.ReadContact(FirstContact(following)).Primary.ActorIndex);
                    }
                    pool = Put(pool, nextSlot, next);
                }
                task = task.WriteWord(34, unchecked((ushort)(Word(task, 34) + 1)));
                bool single = task.Callback == 0x62A380;
                int model = single ? Word(task, 32) : -1;
                if (single && ((uint)model >= 7 || completion[model] == 0 && phase == 0))
                {
                    if ((uint)model >= 7)
                    {
                        throw new InvalidDataException("Native62A380 target index leaves the seven graphics models.");
                    }
                    return new(Put(pool, slot, task), 0);
                }
                if (single && phase == 0)
                {
                    completion = completion.SetItem(model, 0);
                }
                if (phase == 1)
                {
                    NativeBattleActionHeaderSlot header = Header(sequence);
                    int count = single ? 4 : header.ReadByte(16);
                    int first = single ? 0 : FirstContact(header);
                    for (int index = 0; index < count; index++)
                    {
                        int target = single ? model : inputs.Packets.ReadContact(first + index).Primary.ActorIndex;
                        NativeBattleModelTransform transform = inputs.ReadTransform(target);
                        NativeBattleModuleAnchor anchor = inputs.ReadModelAnchor(target);
                        int radius = Math.Min(1000, transform.Scale38 * 2800 >> 12);
                        if (radius <= 0)
                        {
                            throw new NotSupportedException("Native Fire particle distribution requires the original positive graphics WORD26 divisor.");
                        }
                        (pool, int particle) = pool.Append(0x629C10);
                        NativeBattleModulePoolTask child = pool.Slots[particle].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(Rand() % 6 + (single ? 2 * index : 0))));
                        child = Position(child, anchor);
                        child = Add(child, 16, Rand() % radius - (radius >> 1));
                        child = Add(child, 18, Rand() % radius - (radius >> 1));
                        child = Add(child, 20, Rand() % radius - (radius >> 1));
                        child = child.WriteWord(22, unchecked((ushort)transform.Angle36));
                        if (Word(child, 18) > transform.Angle36 - 550)
                        {
                            child = Add(child, 18, -500 - Rand() % 300);
                        }
                        child = child.WriteWord(28, (ushort)(Rand() % 1536 + 3328));
                        pool = Put(pool, particle, child);
                    }
                }
                if (phase == 8)
                {
                    NativeBattleActionHeaderSlot header = Header(sequence);
                    int first = FirstContact(header);
                    int count = single ? 1 : header.ReadByte(16);
                    for (int index = 0; index < count; index++)
                    {
                        contacts.Add(first + index);
                        inputs.ApplyContact?.Invoke(first + index);
                    }
                }
                if (phase == 6)
                {
                    sounds.Add(new(0xDEE130, 256, single ? null : 128, single ? inputs.ReadModelAnchor(model) : null));
                }
                task = Add(task, 12, 1);
                bool done = Word(task, 12) >= 22;
                if (single && done)
                {
                    completion = completion.SetItem(model, 1);
                }
                return new(Put(pool, slot, task), done ? 2 : 0);
            }

            if (task.Callback is not (0x629C10 or 0x629F90 or 0x62A0A0))
            {
                throw new NotSupportedException($"Native Fire callback{task.Callback:X} is unsupported.");
            }
            if (delay > 0)
            {
                return new(Put(pool, slot, frozen ? task : Add(task, 14, -1)), 0);
            }
            int scale = Word(task, task.Callback == 0x629F90 ? 32 : 28);
            NativeBattleModuleAnchor position = new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22));
            if (task.Callback == 0x629C10)
            {
                Draw(0xDEDC2C, phase, position, scale, -(scale >> 4), 0);
                int height = Math.Max(-2000, (int)Word(task, 18));
                int intensity = unchecked((byte)(160 + (160 * ((height << 12) / 2000)) / 4096));
                ImmutableArray<short> rotation = NativeBattlePoseMatrices.RotationWords(new(1024, 0, 0));
                ImmutableArray<short>.Builder ground = rotation.ToBuilder();
                for (int matrixRow = 0; matrixRow < 3; matrixRow++)
                {
                    ground[matrixRow * 3] = unchecked((short)(rotation[matrixRow * 3] * scale >> 12));
                    ground[matrixRow * 3 + 1] = unchecked((short)(rotation[matrixRow * 3 + 1] * scale >> 12));
                }
                Draw(0xDEDDD8, phase, position with { Y = Word(task, 22), W = 0 }, 4096, 0, 0xC, (uint)(intensity | intensity << 8 | intensity << 16), ground.ToImmutable());
                if (!frozen && phase == 0)
                {
                    for (int index = 0; index < 4; index++)
                    {
                        (pool, int particle) = pool.Append(0x62A0A0);
                        NativeBattleModulePoolTask child = pool.Slots[particle].WriteWord(12, 0).WriteWord(14, (ushort)(Rand() % 6 + index));
                        child = Position(child, position);
                        child = Add(child, 16, Rand() % 300 - 150);
                        child = Add(child, 18, -(Rand() % 100));
                        child = Add(child, 20, Rand() % 300 - 150);
                        child = child.WriteWord(22, unchecked((ushort)(-20 - Rand() % 30))).WriteWord(28, (ushort)(Rand() % 1280 + 2560));
                        pool = Put(pool, particle, child);
                    }
                    for (int index = 0; index < 10; index++)
                    {
                        (pool, int particle) = pool.Append(0x629F90);
                        NativeBattleModulePoolTask child = pool.Slots[particle].WriteWord(12, (ushort)(Rand() % 4 + 5)).WriteWord(14, (ushort)(Rand() % 4 + index));
                        child = Position(child, position);
                        child = Add(child, 16, Rand() % 400 - 200);
                        child = Add(child, 18, -(Rand() % 200));
                        child = Add(child, 20, Rand() % 400 - 200);
                        child = child.WriteWord(24, unchecked((ushort)(Rand() % 80 - 40))).WriteWord(26, unchecked((ushort)(-20 - Rand() % 50))).WriteWord(28, unchecked((ushort)(Rand() % 80 - 40))).WriteWord(32, (ushort)(Rand() % 1536 + 2304));
                        pool = Put(pool, particle, child);
                    }
                }
            }
            else if (task.Callback == 0x62A0A0)
            {
                Draw(0xDEDF84, phase, position, scale, -(scale >> 3), 0);
            }
            else
            {
                NativeBattleModuleResource resource = inputs.Resources.Single(resource => resource.Id == 5 && resource.Address != 0xDEDC2C && resource.Address != 0xDEDDD8 && resource.Address != 0xDEDF84);
                Draw(resource.Address, 9 - phase, position, scale, -(scale >> 3), 0x10, 0x808080, [], [4, 0, 0, 0]);
            }
            if (frozen)
            {
                return new(Put(pool, slot, task), 0);
            }
            bool released;
            if (task.Callback == 0x629F90)
            {
                task = Add(task, 32, -64);
                task = Add(task, 16, Word(task, 24));
                task = Add(task, 24, -(Word(task, 24) >> 4));
                task = Add(task, 18, Word(task, 26));
                task = Add(task, 26, Word(task, 26) >> 4);
                task = Add(task, 20, Word(task, 28));
                task = Add(task, 28, -(Word(task, 28) >> 4));
                task = Add(task, 12, -1);
                released = Word(task, 12) < 0;
            }
            else
            {
                if (task.Callback == 0x62A0A0)
                {
                    task = Add(task, 18, Word(task, 22));
                    task = Add(task, 22, Word(task, 22) >> 5);
                    task = Add(task, 28, -(Word(task, 28) >> 6));
                }
                task = Add(task, 12, 1);
                released = Word(task, 12) >= 16;
            }
            return new(Put(pool, slot, task), released ? 2 : 0);
        }

        int Rand()
        {
            random = random.Draw(out ushort value);
            return value;
        }

        NativeBattleActionHeaderSlot Header(int sequence)
        {
            int index = parameters.HeaderIndex7C + sequence;
            if ((uint)index >= NativeBattleActionPacketsState.HeaderCapacity)
            {
                throw new InvalidDataException("Native Fire continuation leaves its physical action header bank.");
            }
            return inputs.Packets.Headers[index];
        }

        void Draw(uint address, int phase, NativeBattleModuleAnchor anchor, int scale, int depthOffset, ushort flags, uint color = 0x808080, ImmutableArray<short> rotation = default, ImmutableArray<byte> paletteOffsets = default)
        {
            NativeBattleModuleResource resource = inputs.Resources.Single(resource => resource.Address == address);
            draws.Add(new(NativeBattleModuleDrawKind.Sprite, 0) { PilotIdentity = drawingIdentity is { } identity ? identity with { Primitive = unchecked((int)address) } : null, Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Anchor = anchor, Phase = unchecked((ushort)phase), Scale = scale, DepthOffset = depthOffset, Direction = 4096, SpriteFlags = flags, SpriteColor = color, SpriteWorldRotation = rotation.IsDefault ? [] : rotation, SpritePaletteOffsets = paletteOffsets.IsDefault ? [] : paletteOffsets });
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int amount) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + amount)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModuleAnchor position) => task.WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z)).WriteWord(22, unchecked((ushort)position.W));
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static int FirstContact(NativeBattleActionHeaderSlot header) => header.FirstContactIndex ?? throw new InvalidDataException("Native Fire requires its physical contact pointer.");
}
