using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>Copies bounded scalar owners only; no snapshot, resource payload or script bytes are retained.</summary>
    public object ObserveDiagnosticEffectPreviewOwners()
    {
        NativeBattleGraphicsState? graphics = NativeGraphics;
        NativeBattleOriginalEffectState? effect = graphics?.OriginalEffect;
        return new
        {
            LifecycleId = DiagnosticLifecycleId,
            Callback = NativeHostCallbacks,
            SimulationFrame = Frame,
            Outcome,
            NativeOutcome = graphics?.NativeOutcome,
            GraphicsCallback = graphics?.Callback,
            GraphicsFlags = graphics?.Flags,
            ScriptPassPaused = graphics is null ? (bool?)null : (graphics.Flags & 1) != 0,
            NativeActive = NativeEffectMemory?.Active,
            Files = NativeFileIo?.ActiveCount,
            CameraMask = NativeCamera?.Mask,
            Owners = graphics is not null && NativeEffectMemory is not null ? CaptureDiagnosticInvocationOwnerState(graphics) : null,
            SelectedEffect = effect?.RequestedEffect,
            ReturnedDescriptor = effect?.ReturnedDescriptor,
            DescriptorCount = graphics?.Descriptors.Queue.Length,
            ActiveDescriptorTaskCount = graphics?.Descriptors.Tasks.Count(task => (task.Flags & 1) != 0),
            Descriptors = graphics?.Descriptors.Queue.Take(30).Select(index => new { Slot = index, graphics.Descriptors.Packets[index].Command, graphics.Descriptors.Packets[index].Argument, graphics.Descriptors.Packets[index].Serial, graphics.Descriptors.Packets[index].PriorityState }).ToArray(),
            DescriptorTasks = graphics?.Descriptors.Tasks.Select((task, slot) => new { Slot = slot, task.Kind, task.Flags, task.State }).Where(task => (task.Flags & 1) != 0).Take(32).ToArray(),
            Auxiliary = graphics is null ? null : CaptureDiagnosticTextureQuiescence(graphics),
            Models = graphics?.Models.Take(7).Select(model => new
            {
                model.Index,
                model.Flags0,
                model.ResourceFlags44,
                model.Sequence,
                model.SequenceFrame,
                SequenceState = model.SequenceState is { } sequence ? new { sequence.Offset46, sequence.Wait1, sequence.Pending3, sequence.Callback42, sequence.Header40 } : null
            }).ToArray(),
            Effect101 = effect?.RequestedEffect == 101 ? ObserveEffect101PreviewOwners(effect) : null,
            ScriptedEffect = effect?.RequestedEffect is 77 or 211 ? ObserveScriptedPreviewOwners() : null
        };
    }

    private object ObserveEffect101PreviewOwners(NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffect101State? state = NativeEffectMemory?.Effect101;
        uint? target = state is null ? null : PreviewScalar<uint>(() => state.ReadDword(NativeBattleEffect101Profile.TargetGlobal));
        uint? working = state is null ? null : PreviewScalar<uint>(() => state.ReadDword(NativeBattleEffect101Profile.WorkingGlobal));
        uint relative = target is uint pointer ? unchecked(pointer - 0x1D972C0u) : uint.MaxValue;
        int? targetIndex = relative % 156 == 0 && relative / 156 < 7 ? (int)(relative / 156) : null;
        return new
        {
            TargetPointer = target,
            TargetIndex = targetIndex,
            Working = working,
            Pools = effect.Pools.Take(2).Select(pool => new
            {
                pool.DescriptorAddress,
                pool.BankAddress,
                pool.Head,
                pool.Tail,
                Rows = pool.Slots.Take(1).Select(task => new { task.Flags, task.Callback, Age = PreviewScalar<ushort>(() => task.ReadWord(12)), Started = pool.DescriptorAddress == NativeBattleEffect101Profile.RootDescriptor ? PreviewScalar<byte>(() => task.ReadByte(15)) : null }).ToArray()
            }).ToArray()
        };
    }

    private object ObserveScriptedPreviewOwners()
    {
        NativeBattleScriptedEffectMemory? core = NativeEffectMemory?.ScriptedEffects;
        uint? Core(uint address)
        {
            if (core is null || address < NativeBattleScriptedEffectMemory.Address || (ulong)address + 4 > NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length)
            {
                return null;
            }
            return PreviewScalar<uint>(() =>
            {
                (byte[] bytes, bool[] known) = core.ReadSnapshot(address, 4);
                return known.All(value => value) ? (uint?)BinaryPrimitives.ReadUInt32LittleEndian(bytes) : null;
            });
        }
        uint? Arena(uint? address, int relative)
        {
            if (address is not uint pointer || NativeEffectMemory?.Arena is not { } arena)
            {
                return null;
            }
            long offset = (long)pointer + relative - NativeBattleEffectNodeMemory.ArenaAddress;
            if (offset < 0 || offset + 4 > NativeBattleEffectArena.Length)
            {
                return null;
            }
            return PreviewScalar<uint>(() =>
            {
                (byte[] bytes, bool[] known) = arena.ReadSnapshot((int)offset, 4);
                return known.All(value => value) ? (uint?)BinaryPrimitives.ReadUInt32LittleEndian(bytes) : null;
            });
        }
        uint? manager = Core(0x27973EC);
        uint? node = Core(0x27973E8);
        byte[]? ArenaKnown(uint? address, int relative, int length)
        {
            if (address is not uint pointer || NativeEffectMemory?.Arena is not { } arena)
            {
                return null;
            }
            long offset = (long)pointer + relative - NativeBattleEffectNodeMemory.ArenaAddress;
            if (offset < 0 || offset + length > NativeBattleEffectArena.Length)
            {
                return null;
            }
            (byte[] bytes, bool[] known) = arena.ReadSnapshot((int)offset, length);
            return known.All(value => value) ? bytes : null;
        }
        ushort? ArenaWord(uint? address, int relative) => ArenaKnown(address, relative, 2) is { } bytes ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null;
        object? Breakup()
        {
            if (NativeGraphics?.OriginalEffect?.RequestedEffect != 211 || ArenaKnown(node, 0x1C, 1) is not { } draw || draw[0] != 22)
            {
                return null;
            }
            uint? payload = Arena(node, 0xB8);
            ushort? mode = ArenaWord(payload, 8);
            if (mode is not (2 or 7))
            {
                return null;
            }
            uint? work = Arena(node, 0xC0);
            uint? pool = Arena(node, 0xA0);
            ushort? capacity = ArenaWord(payload, 0x2E);
            int? active = null;
            int? knownFlags = null;
            if (capacity is >= 0 and <= 512 && pool is uint pointer && NativeEffectMemory?.Arena is { } arena)
            {
                long offset = (long)pointer - NativeBattleEffectNodeMemory.ArenaAddress;
                int length = capacity.Value * NativeBattleCinematicBreakupAuxiliary.RecordSize;
                if (offset >= 0 && offset + length <= NativeBattleEffectArena.Length)
                {
                    (byte[] bytes, bool[] known) = arena.ReadSnapshot((int)offset, length);
                    int flags = 0;
                    int live = 0;
                    for (int index = 0; index < capacity.Value; index++)
                    {
                        int flag = index * NativeBattleCinematicBreakupAuxiliary.RecordSize + 24;
                        if (known[flag])
                        {
                            flags++;
                            live += bytes[flag] == 0 ? 0 : 1;
                        }
                    }
                    knownFlags = flags;
                    active = flags == capacity.Value ? live : null;
                }
            }
            return new { Scope = "current-draw-node", Mode = mode, Payload = payload, Work = work, AuxiliaryPool = pool, Capacity = capacity, KnownActiveFlags = knownFlags, Active = active, EmissionCounter = ArenaWord(work, 0x1C) };
        }
        uint? Manager(int relative) => manager is uint pointer && (ulong)pointer + (uint)relative <= uint.MaxValue ? Core(pointer + (uint)relative) : null;
        byte? ManagerByte(int relative)
        {
            if (manager is not uint pointer || core is null || (ulong)pointer + (uint)relative < NativeBattleScriptedEffectMemory.Address || (ulong)pointer + (uint)relative >= NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length)
            {
                return null;
            }
            return PreviewScalar<byte>(() =>
            {
                (byte[] bytes, bool[] known) = core.ReadSnapshot(pointer + (uint)relative, 1);
                return known[0] ? (byte?)bytes[0] : null;
            });
        }
        return new { Manager = manager, Node = node, GlobalPc = Core(0x2797450), Bank = Manager(0x90), MainRoot = Manager(0x94), HeaderIndex = ManagerByte(0xD0), HeaderCount = ManagerByte(0xD1), Pause35 = ManagerByte(0x35), NodePc0 = Arena(node, 0), NodePc1 = Arena(node, 4), NodePc2 = Arena(node, 8), CurrentNodeBreakup = Breakup() };
    }

    private static T? PreviewScalar<T>(Func<T?> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
