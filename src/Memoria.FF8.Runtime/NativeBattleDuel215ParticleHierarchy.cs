using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215ParticleHierarchyStep(NativeBattleDuel215Step Step, NativeBattleLogicalOrdering Ordering);

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleActionMatrix ParticleBasis6ED1E0(short angle, short scale)
    {
        int sine = unchecked(scale * NativeBattleCameraMath.Sine(-(int)angle)) >> 12;
        int cosine = unchecked(scale * NativeBattleCameraMath.Cosine(-(int)angle)) >> 12;
        return new([unchecked((short)cosine), 0, unchecked((short)-sine), 0, scale, 0, unchecked((short)sine), 0, unchecked((short)cosine)], 0, 0, 0, 4096);
    }

    public static NativeBattleDuel215Program ParticleProgram69CB10(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = ParticleBasis6ED1E0(unchecked((short)task.ReadWord(20)), unchecked((short)task.ReadWord(18))) with { X = unchecked((short)task.ReadWord(12)), Y = unchecked((short)task.ReadWord(14)), Z = unchecked((short)task.ReadWord(16)) };
        return new(NativeBattleGfMatrices.Compose56C2F0(view, local), new(0, 0, 0), "0\0\0", -128, 0, 0, Arena + 63768);
    }

    public static NativeBattleDuel215ParticleHierarchyStep Advance69CB10(NativeBattleDuel215Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering, bool capturePresentation = false)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != Particles || task.Callback != 0x69CB10)
        {
            throw new InvalidDataException("Native Duel215 particle hierarchy requires its actual registered callback and pool.");
        }
        NativeBattleDuel215Resources resources = frame.Memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 particle hierarchy requires its entered static resources.");
        uint address = task.ReadDword(24);
        int size = address switch
        {
            0x115C4C8 => 64,
            0x115CA08 => 352,
            _ => throw new NotSupportedException("Native Duel215 particle hierarchy selected an unsupported original program.")
        };
        NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel215Resources.EndAddress - address))));
        NativeBattleDuel215Program context = ParticleProgram69CB10(task, view);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 24, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel215HierarchyStep drawn = NativeBattleDuel215Hierarchy.Draw69C220(frame, source, node, context, projection, colors, capturePresentation ? NativeBattleDuel28Presentation.Identity(215, task, source.Address, unchecked(24 * 65537 + node.ResourceOffset)) : null);
            frame = drawn.Frame;
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Duel215 particle hierarchy requires its actual private packet cursor.");
            if (capturePresentation)
            {
                frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, cursor, drawn.Batch) } };
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, drawn.Batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = Dword(frame.Staging, 0x2508988, checked(Arena + (uint)written.Cursor)) };
            ordering = written.Ordering;
            return current;
        });
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 24, advanced.Workspace);
        pool = pool.WriteSlot(slot, task);
        return new(new(frame, pool, advanced.Remaining == 0 ? 2 : 0), ordering);
    }
}
