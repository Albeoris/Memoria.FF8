using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191ParticleResources(ImmutableArray<NativeBattleModuleResource> Sprites)
{
    public NativeBattleModuleResource RequireSprite(uint address)
    {
        if (Sprites.IsDefault || Sprites.Select(sprite => sprite.Address).Distinct().Count() != Sprites.Length)
        {
            throw new InvalidDataException("Native Doomtrain particles require their distinct original sprite owners.");
        }
        return Sprites.SingleOrDefault(sprite => sprite.Address == address) ?? throw new NotSupportedException($"Native Doomtrain sprite {address:X8} requires its original resource.");
    }
}

public sealed record NativeBattleDoomtrain191SteamDraw(NativeBattleActionMatrix Matrix, ushort Phase)
{
    public uint ResourceAddress { get; init; } = 0xE33E40;
}
public sealed record NativeBattleDoomtrain191SteamServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191SteamDraw, NativeBattleDoomtrain191Frame> Draw);

public static class NativeBattleDoomtrain191Steam
{
    public static NativeBattleDoomtrain191Frame Spawn(NativeBattleDoomtrain191Frame frame, bool pair)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        (pool, int slot) = pool.Append(pair ? 0x6418E0u : 0x641580u);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0xE, pair ? (ushort)2 : (ushort)1).WriteWord(0x10, 0).WriteWord(0x12, pair ? unchecked((ushort)-200) : (ushort)0).WriteWord(0x14, pair ? (ushort)0x8AD0 : (ushort)0xF830).WriteWord(0x1E, pair ? (ushort)2048 : (ushort)4096);
        if (!pair)
        {
            task = task.WriteWord(0x18, 0).WriteWord(0x1C, 2000).WriteWord(0x20, unchecked((ushort)-90));
        }
        return PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task) });
    }

    public static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleModuleResource resource, NativeBattleDoomtrain191SteamServices services)
    {
        NativeBattleModulePoolTask task = Pool(frame).Slots[slot];
        bool pair = task.Callback == 0x6418E0;
        if (!pair && task.Callback != 0x641580)
        {
            throw new InvalidDataException("Native Doomtrain steam requires its actual task callback.");
        }
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain steam requires its retained view and work pointers.");
        NativeBattleActionMatrix camera = state.BaseView.ReadMatrix();
        NativeBattleScriptedLightingMemory lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain steam requires its actual particle bank.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena, ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        int start = checked((int)(state.WorkPointers[0] - state.ArenaAddress));
        int mask = Word(0xE);
        bool paused = (services.ReadFlags() & 1) != 0;
        for (int index = 0; index < 170; index++)
        {
            int record = checked(start + index * 24);
            if ((Dword(record) & unchecked((uint)mask)) == 0)
            {
                continue;
            }
            NativeBattleVector point = new(ParticleWord(record + 8), ParticleWord(record + 10), ParticleWord(record + 12));
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, point);
            int x = pair ? mac.X : Math.Clamp(mac.X, short.MinValue, short.MaxValue);
            int y = pair ? mac.Y : Math.Clamp(mac.Y, short.MinValue, short.MaxValue);
            int z = pair ? mac.Z : Math.Clamp(mac.Z, short.MinValue, short.MaxValue);
            short size = ParticleWord(record + 6);
            ushort phase = unchecked((ushort)ParticleWord(record + 4));
            NativeBattleModuleSpriteFrame sprite = resource.ProjectedFrame571C80(phase);
            frame = services.Draw(frame, new(new([size, 0, 0, 0, size, 0, 0, 0, size], x, y, z, 4096), phase));
            if (!paused)
            {
                StoreWord(record + 4, sprite.NextOffset < 0 ? 0 : unchecked((ushort)(phase + 1)));
            }
        }
        int result = 0;
        if (!paused)
        {
            if (Word(0xC) <= (pair ? 20 : 18))
            {
                for (int iteration = 0; iteration < (pair ? 2 : 1); iteration++)
                {
                    int index = 0;
                    while (index < 170 && Dword(start + index * 24) != 0)
                    {
                        index++;
                    }
                    if (index == 170)
                    {
                        break;
                    }
                    int record = checked(start + index * 24);
                    if (pair)
                    {
                        Seed(record);
                        Add(record + 8, -900);
                        Seed(record + 24);
                        Add(record + 32, 900);
                        task = task.WriteWord(0x14, unchecked((ushort)(Word(0x14) + 1500))).WriteWord(0x1E, unchecked((ushort)(Word(0x1E) - 16)));
                    }
                    else
                    {
                        int angle = unchecked(NativeBattleCameraMath.Sine(Word(0x18)) * 180) >> 12;
                        Seed(record);
                        Offset(record, angle - 1024);
                        Seed(record + 24);
                        Offset(record + 24, angle + 1024);
                        short step = Word(0x20);
                        task = task.WriteWord(0x10, unchecked((ushort)(Word(0x10) + (unchecked(NativeBattleCameraMath.Sine(angle) * 2000) >> 12)))).WriteWord(0x12, unchecked((ushort)(Word(0x12) - 20))).WriteWord(0x14, unchecked((ushort)(Word(0x14) + (unchecked(NativeBattleCameraMath.Cosine(angle) * 2000) >> 12)))).WriteWord(0x18, unchecked((ushort)(Word(0x18) + 160))).WriteWord(0x1E, unchecked((ushort)(Word(0x1E) - 88))).WriteWord(0x1C, unchecked((ushort)(Word(0x1C) + step))).WriteWord(0x20, unchecked((ushort)(step - 1)));
                    }
                }
            }
            ushort age = unchecked((ushort)(task.ReadWord(0xC) + 1));
            task = task.WriteWord(0xC, age);
            if (unchecked((short)age) >= (pair ? 90 : 18))
            {
                for (int index = 0; index < 170; index++)
                {
                    int record = checked(start + index * 24);
                    if ((Dword(record) & unchecked((uint)mask)) != 0)
                    {
                        arena = arena.Write(record, new byte[4]);
                    }
                }
                result = 2;
            }
            NativeBattleEffectTaskPool pool = Pool(frame);
            frame = PutPool(frame, pool with { Slots = pool.Slots.SetItem(slot, task) });
        }
        return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, result);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        short ParticleWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
        void StoreWord(int offset, int value)
        {
            arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        }
        void Add(int offset, int value) => StoreWord(offset, unchecked(ParticleWord(offset) + value));
        void Seed(int record)
        {
            arena = arena.Write(record, BitConverter.GetBytes(unchecked((uint)mask))).Write(record + 4, new byte[2]).Write(record + 6, BitConverter.GetBytes(task.ReadWord(0x1E))).Write(record + 8, BitConverter.GetBytes(task.ReadDword(0x10))).Write(record + 12, BitConverter.GetBytes(task.ReadDword(0x14)));
        }
        void Offset(int record, int angle)
        {
            Add(record + 8, unchecked(NativeBattleCameraMath.Sine(angle) * Word(0x1C)) >> 12);
            Add(record + 12, unchecked(NativeBattleCameraMath.Cosine(angle) * Word(0x1C)) >> 12);
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
