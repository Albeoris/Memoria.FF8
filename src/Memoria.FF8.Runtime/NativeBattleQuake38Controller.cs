namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleQuake38State State);

public static class NativeBattleQuake38Controller
{
    public static NativeBattleQuake38Frame Enter8D8510(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleQuake38Resources resources)
    {
        effect.Validate();
        packets.Validate();
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native Quake requires its original action parameters.");
        NativeBattleQuake38State state = memory.Quake38 ?? throw new NotSupportedException("Native Quake requires its retained module fields.");
        state.Validate();
        if (effect.Binding.Entry != 0x8D8510 || memory.Arena is null)
        {
            throw new InvalidDataException("Native Quake requires its actual entry and process effect arena.");
        }
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x278E648, 0x278E560, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x27929D0, 0x27929E0, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x2792B50, 0x2792B60, 52, 10),
            NativeBattleEffectTaskPool.Initialize(0x2792988, 0x278E658, 172, 100),
            NativeBattleEffectTaskPool.Initialize(0x278E538, 0x278C9B8, 176, 40)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D8610, 100);
        hierarchy = root.Hierarchy;
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Quake requires its first actual action contact.");
        NativeBattleModulePoolTask task = hierarchy.Read(root.Address).WriteDword(12, 0x1D99A78).WriteByte(45, packets.ReadContact(first).Primary.ActorIndex).WriteWord(90, header.ReadByte(16)).WriteWord(88, header.ReadByte(17));
        hierarchy = hierarchy.Write(root.Address, task);
        state = state.SetDword(0x2792B48, 0x20DFAB8).SetDword(0x2792B44, 0x20F3AB8).SetDword(0x2792E34, 0x2107AB8).SetDword(0x2792E1C, 0x2107AB8).SetDword(0x2792E0C, 0x21130B8).SetDword(0x2792E08, 0x2113D78);
        NativeBattleEffectTextureState textures = (memory.Textures ?? throw new NotSupportedException("Native Quake requires its retained texture owner.")).Register5099A0(resources.Camera);
        (textures, _) = textures.EnqueueTim505E30(new(resources.Texture.Name, resources.Texture.Data));
        memory = memory with { Textures = textures, Quake38 = state };
        return new(memory, effect with { Pools = hierarchy.Pools, ReturnedDescriptor = 0x278E648, DrawArenaOffset = null }, state);
    }
}
