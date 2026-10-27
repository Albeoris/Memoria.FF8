using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    public const uint Root = 0x25565C8;
    public const uint Body = 0x25565B8;
    public const uint Sprites = 0x25565A8;
    public const uint Beams = 0x2556598;
    private const uint ArenaAddress = 0x20DFAB8;

    public static NativeBattleRinoa89Frame Enter6F9D00(NativeBattleRinoa89Frame frame, NativeBattleRinoa89Resources resources)
    {
        if (frame.Effect.Binding is not { Effect: 89, Entry: 0x6F9CF0, Initializer: 0x5755E0 } || frame.Effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native6F9D00 requires its actual Wishing Star binding and parameter owner.");
        }
        resources.Validate();
        NativeBattleRinoa89State state = frame.Memory.Rinoa89 ?? throw new NotSupportedException("Native6F9D00 requires its retained process owner; migrate the selected standalone explicitly.");
        if (!state.GlobalsPublished)
        {
            frame = frame with { Staging = frame.Staging.Write(NativeBattleRinoa89Resources.StaticAddress, resources.StaticImage.Data.AsSpan()).Write(0x2556584, new byte[160]) };
        }
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native6F9D00 requires its actual texture queue.");
        foreach (NativeBattleEffectHeapAllocation texture in resources.InitialTextures)
        {
            (textures, _) = textures.EnqueueTim505E30(texture);
        }
        frame = frame with { Staging = Dword(frame.Staging, 0x2556600, ArenaAddress) };
        frame = frame with { Staging = Dword(frame.Staging, 0x2556584, 0x1D99A78) };
        frame = frame with { Staging = Dword(frame.Staging, 0x2556588, ModelAddress(parameters.Actor78)) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Root, 0x25565D8, 20, 1);
        (root, int slot) = root.Append(0x6F9D80);
        root = root.WriteSlot(slot, root.Slots[slot].WriteWord(12, 0));
        return frame with { Memory = frame.Memory with { Rinoa89 = state with { GlobalsPublished = true, Resources = resources }, Textures = textures }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = Root, DrawArenaOffset = 0 } };
    }

    public static NativeBattleRinoa89Frame SelectRootPacketBank(NativeBattleRinoa89Frame frame, int slot)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Root);
        NativeBattleModulePoolTask task = root.Slots[slot];
        if (task.Callback != 0x6F9D80)
        {
            throw new InvalidDataException("Native6F9D80 requires its actual 20-byte root task.");
        }
        bool prior = task.ReadDword(16) != 0;
        frame = frame with { Staging = Dword(frame.Staging, 0x25565EC, ArenaAddress + (prior ? 108684u : 174220u)) };
        frame = frame with { Staging = Dword(frame.Staging, 0x25565F0, ArenaAddress + (prior ? 174220u : 239756u)) };
        root = root.WriteSlot(slot, task.WriteDword(16, prior ? 0u : 1u));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Root)), root) } };
    }

    private static uint ModelAddress(byte actor) => checked(0x1D972C0u + actor * 156u);
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
