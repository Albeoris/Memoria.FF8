using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Workspaces
{
    public static NativeBattleDuel274Frame Initialize7016B0(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, uint destination, uint sourceAddress, int size)
    {
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[size]));
        workspace = workspace.Initialize7016B0(resources.Hierarchy(sourceAddress), size);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel workspace requires its actual arena owner.");
        arena = workspace.CopyTo(arena, Offset(destination, size));
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    public static NativeBattleDuel274Frame Advance701970(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, uint address, NativeBattleDuel274HierarchyContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleDuel274Frame, NativeBattleGfGeometryBatch, NativeBattleDuel274Frame> submit)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel workspace requires its actual arena owner.");
        int start = Offset(address, 8);
        uint resourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start, 4));
        int size = resourceAddress switch
        {
            0x10EA030 => 332,
            0x10EC7D4 => 100,
            0x10ED700 => 1480,
            0x10F3808 => 864,
            0x10F6BE0 => 100,
            0x10F10F8 => 572,
            0x10F73B0 => 296,
            0x10F89F4 => 2532,
            0x10FE310 => 224,
            0x110198C => 904,
            _ => throw new NotSupportedException("Native Duel workspace selected an unsupported original animation resource.")
        };
        NativeBattleGfAnimationSource source = resources.Hierarchy(resourceAddress);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(arena, start, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel274HierarchyStep drawn = NativeBattleDuel274Hierarchy.Draw68A400(frame, resources, source, node, context, projection, colors);
            frame = submit(drawn.Frame, drawn.Batch);
            return current;
        });
        arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel animation lost its physical workspace owner.");
        return frame with { Memory = frame.Memory with { Arena = advanced.Workspace.CopyTo(arena, start) } };
    }

    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
}
