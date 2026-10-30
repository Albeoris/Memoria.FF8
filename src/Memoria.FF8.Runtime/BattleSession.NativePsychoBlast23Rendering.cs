using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattlePsychoBlast23Frame Frame) DrawNativePsychoBlast23Act(NativeBattleGraphicsState graphics, NativeBattlePsychoBlast23Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        NativeBattlePsychoBlast23Resources resources = current.Effect.Binding.PsychoBlast23Resources ?? throw new NotSupportedException("Psycho Blast requires its original ACT directory.");
        NativeBattleGfAnimationSource source = resources.Act(task.ReadDword(0x94));
        int length = source.Address == resources.ActDelayed.Address ? 88 : 216;
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, length);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Psycho Blast ACT requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Psycho Blast ACT requires its current projection.");
        NativeBattlePsychoBlast23ActContext root = NativeBattlePsychoBlast23Rendering.PrepareAct(task, projection.View ?? throw new NotSupportedException("Psycho Blast ACT requires its published view matrix."));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (retained, node) =>
        {
            if (!NativeBattlePsychoBlast23Rendering.Visible(node))
            {
                return retained;
            }
            (uint temporary, NativeBattleEffectArena context) = AllocateNativePsychoBlast23Temporary(104);
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Psycho Blast ACT requires its packet process.");
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Psycho Blast ACT requires its current projection controls.");
            NativeBattlePsychoBlast23NodeDraw drawn = NativeBattlePsychoBlast23Rendering.DrawNode(source, node, root, context, current.State.MorphScratch, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Psycho Blast ACT requires its geometry controls."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Psycho Blast ACT requires its geometry data registers.")), process.ModelRendererRegisters ?? throw new NotSupportedException("Psycho Blast ACT requires its retained far colors."), projection, controls.Dqa, controls.Dqb, controls.Zsf3, controls.Zsf4, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Psycho Blast ACT requires its geometry constants."), NativeBattlePilotVisualLifetimes.Identity(23, task) with { Primitive = node.ResourceOffset, ResourceGeneration = unchecked(((long)source.Address << 16) ^ node.GeometryIndex) });
            current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } }, State = current.State with { MorphScratch = drawn.MorphScratch } };
            (graphics, current) = AppendNativePsychoBlast23Private(graphics, current, drawn.Batch);
            ReleaseNativePsychoBlast23Temporary(temporary, drawn.Temporary104, 104);
            return retained;
        });
        NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(hierarchy.Read(address), 0x94, advanced.Workspace);
        hierarchy = hierarchy.Write(address, task);
        return (graphics, current with { Effect = current.Effect with { Pools = hierarchy.Pools } });
    }

    private (NativeBattleGraphicsState Graphics, NativeBattlePsychoBlast23Frame Frame) DrawNativePsychoBlast23Debris(NativeBattleGraphicsState graphics, NativeBattlePsychoBlast23Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return (graphics, current);
        }
        NativeBattlePsychoBlast23Resources resources = current.Effect.Binding.PsychoBlast23Resources ?? throw new NotSupportedException("Psycho Blast debris requires its original sprite resource.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Psycho Blast debris requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Psycho Blast debris requires its published projection.");
        (uint temporary, NativeBattleEffectArena context) = AllocateNativePsychoBlast23Temporary(216);
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(current.Memory, task, camera.CapturePackedView1D97778(), context);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(prepared, selected => selected == resources.Sprite.Address ? resources.Sprite : throw new NotSupportedException("Psycho Blast debris selected an unavailable original resource."), projection, camera.Roll, 17, NativeBattlePilotVisualLifetimes.Identity(23, task) with { ResourceGeneration = resources.Sprite.Address });
        current = current with { Memory = drawn.Memory };
        (graphics, current) = AppendNativePsychoBlast23Private(graphics, current, drawn.Batch);
        ReleaseNativePsychoBlast23Temporary(temporary, drawn.TemporaryD8, 216);
        return (graphics, current);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattlePsychoBlast23Frame Frame) AppendNativePsychoBlast23Private(NativeBattleGraphicsState graphics, NativeBattlePsychoBlast23Frame current, NativeBattleGfGeometryBatch batch)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Psycho Blast requires its ordering table.");
        NativeBattleCuragaState nodes = current.State.Nodes ?? throw new NotSupportedException("Psycho Blast requires its actual module cursor owner.");
        int cursor = nodes.PrimaryDrawCursor ?? throw new NotSupportedException("Psycho Blast requires its selected private cursor.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, current with { Memory = written.Memory, State = current.State with { Nodes = nodes with { PrimaryDrawCursor = written.Cursor } } });
    }

    private (uint Address, NativeBattleEffectArena Arena) AllocateNativePsychoBlast23Temporary(int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Psycho Blast requires its retained physical temporary memory."), length);
        NativeFileStaging = allocation.Staging;
        NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, length);
        return (allocation.Address, NativeBattleEffectNodeContextMemory.Copy(physical, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length));
    }

    private void ReleaseNativePsychoBlast23Temporary(uint address, NativeBattleEffectArena arena, int length)
    {
        NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging!, address, arena, length);
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, length);
    }
}
