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
            Effect77 = effect?.RequestedEffect == 77 ? ObserveEffect77PreviewOwners() : null
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

    private object ObserveEffect77PreviewOwners()
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
        return new { Manager = manager, Node = node, GlobalPc = Core(0x2797450), Bank = Manager(0x90), MainRoot = Manager(0x94), HeaderIndex = ManagerByte(0xD0), HeaderCount = ManagerByte(0xD1), NodePc0 = Arena(node, 0), NodePc1 = Arena(node, 4), NodePc2 = Arena(node, 8) };
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
