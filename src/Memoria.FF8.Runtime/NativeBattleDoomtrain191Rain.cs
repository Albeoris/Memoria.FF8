using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191RainServices(Func<uint> ReadFlags, Func<NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleDoomtrain191Frame, NativeBattleGfGeometryBatch, NativeBattleDoomtrain191Frame> Draw);

public static class NativeBattleDoomtrain191Rain
{
    public static NativeBattleDoomtrain191LeafStep Advance641AF0(NativeBattleDoomtrain191Frame frame, int slot, ImmutableArray<uint> colors, NativeBattleCameraProjection projection, NativeBattleDoomtrain191RainServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x641AF0)
        {
            throw new InvalidDataException("Native Doomtrain rain requires its actual task.");
        }
        NativeBattleActionMatrix camera = services.ReadCamera().ReadMatrix();
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain rain requires its retained work pointers.");
        int start = checked((int)(state.WorkPointers[5] - state.ArenaAddress));
        NativeBattleScriptedLightingMemory lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        frame = frame with { Memory = frame.Memory with { ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        bool paused = (services.ReadFlags() & 1) != 0;
        NativeBattleGfGeometryBatch batch = DrawParticles();
        frame = services.Draw(frame, batch);
        if ((services.ReadFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        if (unchecked((short)task.ReadWord(0xC)) < 308)
        {
            Spawn();
        }
        ushort age = unchecked((ushort)(task.ReadWord(0xC) + 1));
        pool = Pool(frame);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool.WriteSlot(slot, task.WriteWord(0xC, age))) } };
        return new(frame, unchecked((short)age) >= 308 && batch.AllocationBytes == 0 ? 2 : 0);

        NativeBattleGfGeometryBatch DrawParticles()
        {
            NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain rain requires its current physical particle bank.");
            using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
            NativeBattleEffectArena arena = owned?.View ?? source;
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            int allocated = 0;
            for (int index = 0; index < 590; index++)
            {
                int record = checked(start + index * 24);
                if ((BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)) & 1) == 0)
                {
                    continue;
                }
                short age = Word(4);
                if (colors.IsDefault || (uint)age >= (uint)colors.Length)
                {
                    throw new NotSupportedException($"Native Doomtrain rain requires its actual color entry {age}.");
                }
                NativeBattleVector position = new(Word(8), Word(10), Word(12));
                if (!paused)
                {
                    short next = unchecked((short)(age + 1));
                    Store(4, next);
                    if (next >= 64)
                    {
                        arena = arena.Write(record, new byte[4]);
                    }
                    else
                    {
                        Store(8, unchecked((short)(position.X + Word(16))));
                        Store(10, unchecked((short)(position.Y + Word(18))));
                        Store(12, unchecked((short)(position.Z + Word(20))));
                    }
                }
                NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Doomtrain rain requires its current depth-cue controls.");
                NativeBattleGfGeometryAttempt attempt = Emit(camera, position, Word(6), colors[age], projection, (state.LookupCounter & 1) != 0 ? (byte)14 : (byte)4, allocated, controls.Dqa, controls.Dqb, (mac, flags) =>
                {
                    NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown();
                    byte[] v0 = new byte[8];
                    BinaryPrimitives.WriteInt16LittleEndian(v0, -256);
                    BinaryPrimitives.WriteInt16LittleEndian(v0.AsSpan(2), -256);
                    registers = registers with { Mac = [mac.X, mac.Y, mac.Z], Ir = [Math.Clamp(mac.X, short.MinValue, short.MaxValue), Math.Clamp(mac.Y, short.MinValue, short.MaxValue), Math.Clamp(mac.Z, short.MinValue, short.MaxValue)], Flags = flags, NormalInput = v0.ToImmutableArray(), NormalInputKnown = [true, true, true, true, true, true, false, false] };
                    frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers } };
                });
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated += 40;
                }

                short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(checked(record + offset), 2));
                void Store(int offset, short value)
                {
                    arena = arena.Write(checked(record + offset), BitConverter.GetBytes(value));
                }
            }
            frame = frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } };
            return new(attempts.ToImmutable(), allocated);
        }

        void Spawn()
        {
            NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain rain requires its physical particle bank.");
            using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
            NativeBattleEffectArena arena = owned?.View ?? source;
            for (int count = 0; count < 28; count++)
            {
                int index = 0;
                while (index < 590 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked(start + index * 24), 4)) != 0)
                {
                    index++;
                }
                if (index == 590)
                {
                    break;
                }
                int record = checked(start + index * 24);
                arena = arena.Write(record, BitConverter.GetBytes(1u));
                Store(4, 0);
                Store(6, Random() % 1536 + 1536);
                Store(8, 2 * (Random() % 20000) - 20000);
                Store(10, Random() % 20000 - 10000);
                Store(12, 2 * (Random() % 20000) - 20000);
                Store(16, Random() % 48 - 24);
                Store(18, Random() % 48 - 24);
                Store(20, Random() % 48 - 24);

                void Store(int offset, int value)
                {
                    arena = arena.Write(checked(record + offset), BitConverter.GetBytes(unchecked((short)value)));
                }
            }
            frame = frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } };
        }

        int Random()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
    }

    public static NativeBattleGfGeometryAttempt Emit(NativeBattleActionMatrix camera, NativeBattleVector position, short size, uint color, NativeBattleCameraProjection projection, byte depthShift, int allocation = 0, short dqa = 0, int dqb = 0, Action<NativeBattleFullMacVertex, uint>? observeRegisters = null)
    {
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, position);
        NativeBattleActionMatrix billboard = new([size, 0, 0, 0, size, 0, 0, 0, size], mac.X, mac.Y, mac.Z, 4096);
        NativeBattleModuleProjectedVertex first = NativeBattleModuleProjection.Project45FE10(billboard, new(-256, -256, 0), projection);
        NativeBattleModuleProjectedVertex second = NativeBattleModuleProjection.Project45FE10(billboard, new(256, -256, 0), projection);
        NativeBattleModuleProjectedVertex third = NativeBattleModuleProjection.Project45FE10(billboard, new(-256, 256, 0), projection);
        if (observeRegisters is not null)
        {
            NativeBattleFullMacVertex finalMac = NativeBattleGfMatrices.TransformMac460840(billboard, new(-256, 256, 0));
            NativeBattleModuleProjectedVertex final = NativeBattleModuleProjection.ProjectWithCue45FAA0(billboard, new(-256, 256, 0), projection, dqa, dqb).Projected;
            observeRegisters(finalMac, first.Flags | second.Flags | final.Flags);
        }
        uint[] words = [0x09000000, color, 0, 0x3EE4C0A0, 0, 0x0036C0C0, 0, 0x0000E0A0, 0, 0x0000E0C0];
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
        Mark(0, 8);
        Mark(12, 4);
        Mark(20, 4);
        Mark(28, 4);
        Mark(36, 4);
        mask[30] = mask[31] = mask[38] = mask[39] = NativeBattleGfPacketByteState.Unknown;
        NativeBattleGeometryPrimitive? primitive = null;
        if (((first.Flags | second.Flags | third.Flags) & 0x60000) == 0)
        {
            words[2] = first.Screen;
            words[4] = second.Screen;
            words[6] = third.Screen;
            words[8] = (second.Screen & 0xFFFF) | (third.Screen & 0xFFFF0000);
            Mark(8, 4);
            Mark(16, 4);
            Mark(24, 4);
            Mark(32, 4);
            primitive = new(17 + (third.Depth >> depthShift), words.ToImmutableArray(), []) { PhysicalWriteMask = mask.ToImmutableArray() };
        }
        byte[] data = new byte[40];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
        }
        return new(allocation, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);

        void Mark(int offset, int count) => Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, count);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
}
