using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Workspaces
{
    public static NativeBattleDuel274Frame Initialize7016B0(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, uint destination, uint sourceAddress, int size)
    {
        _ = Offset(destination, size);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, destination, size);
        workspace = workspace.Initialize7016B0(resources.Hierarchy(sourceAddress), size);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, destination, workspace);
        return frame;
    }

    public static NativeBattleDuel274Frame Advance701970(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, uint address, NativeBattleDuel274HierarchyContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleDuel274Frame, NativeBattleGfGeometryBatch, NativeBattleDuel274Frame> submit)
    {
        _ = Offset(address, 8);
        uint resourceAddress = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address, 8).ResourceAddress;
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
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel274HierarchyStep drawn = NativeBattleDuel274Hierarchy.Draw68A400(frame, resources, source, node, context, projection, colors);
            frame = submit(drawn.Frame, drawn.Batch);
            return current;
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address, advanced.Workspace);
        return frame;
    }

    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
}
