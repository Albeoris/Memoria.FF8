using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlWorkspaceWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

public static class NativeBattleQuezacotlWorkspace
{
    public const int Offset = 0xD4;
    public const int Length = 0x7D4;
    public const int SecondLength = 0x7FC;
    public const int ThirdLength = 0x158;
    public const int FourthLength = 0x1A0;
    public const int FifthLength = 0x1D8;
    public const int SixthLength = 0x128;
    public const int SeventhLength = 0xB4;
    public const uint EighthAddress = 0x129F4E8;
    public const int EighthLength = 0x4BC;
    public const int NinthLength = 0x124;
    public const int TenthLength = 0x354;
    public const uint TenthSourceAddress = 0x2108954;

    public static NativeBattleGfAnimationSource Source(NativeBattleEffectMemory memory, uint address = NativeBattleQuezacotlExecutableMemory.Address)
    {
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native Quezacotl701970 requires its current executable model bytes.");
        executable.Validate();
        if (address is not (NativeBattleQuezacotlExecutableMemory.Address or 0x12A9664 or 0x12A1164 or 0x129BA94 or 0x1299718 or 0x129CD84))
        {
            throw new NotSupportedException("Native Quezacotl animation source requires its observed executable region.");
        }
        int offset = checked((int)(address - NativeBattleQuezacotlExecutableMemory.Address));
        return NativeBattleGfAnimationSource.Borrow(address, executable.Arena, offset, NativeBattleQuezacotlExecutableMemory.Length - offset);
    }

    public static NativeBattleGfAnimationSource Source(NativeBattleEffectMemory memory, uint address, int observedLength)
    {
        const uint arenaAddress = 0x20DFAB8;
        if (address < arenaAddress || address - arenaAddress >= NativeBattleEffectArena.Length || observedLength <= 0 || observedLength > NativeBattleEffectArena.Length - (address - arenaAddress))
        {
            throw new ArgumentOutOfRangeException(nameof(observedLength), "Native animation source must fit its explicitly observed current arena extent.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native701970 arena source requires its actual retained effect arena.");
        return NativeBattleGfAnimationSource.Borrow(address, arena, checked((int)(address - arenaAddress)), observedLength);
    }

    public static NativeBattleGfAnimationWorkspace Read(NativeBattleEffectMemory memory, int length = Length) => NativeBattleGfAnimationWorkspace.Borrow(memory.Arena ?? throw new NotSupportedException("Native Quezacotl701970 requires its actual child-task arena bytes."), Offset, length);

    public static NativeBattleGfAnimationWorkspace ReadExecutable(NativeBattleEffectMemory memory)
    {
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C465A requires its current129F4E8 executable workspace bytes.");
        executable.Validate();
        return NativeBattleGfAnimationWorkspace.Borrow(executable.Arena, checked((int)(EighthAddress - NativeBattleQuezacotlExecutableMemory.Address)), EighthLength);
    }

    public static NativeBattleEffectMemory WriteExecutable(NativeBattleEffectMemory memory, NativeBattleGfAnimationWorkspace workspace)
    {
        if (workspace.Length != EighthLength)
        {
            throw new InvalidDataException("Native6C46DE requires its actual4BC-byte129F4E8 executable workspace.");
        }
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C46DE requires its current129F4E8 executable workspace owner.");
        workspace.CopyTo(executable.Arena, checked((int)(EighthAddress - NativeBattleQuezacotlExecutableMemory.Address)));
        return memory;
    }

    public static NativeBattleQuezacotlWorkspaceWrite Write(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleGfAnimationWorkspace workspace)
    {
        if (workspace.Length is not (Length or SecondLength or ThirdLength or FourthLength or FifthLength or SixthLength or SeventhLength or NinthLength or TenthLength))
        {
            throw new InvalidDataException("Native Quezacotl workspace must fit its actual7D4/7FC/158/1A0/1D8/128/B4/124/354-byte child workspace.");
        }
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Quezacotl701970 requires its actual child-task arena bytes.");
        workspace.CopyTo(arena, Offset);
        child = child.WriteSlot(0, workspace.CopyTo(child.Slots[0], Offset));
        return new(memory with { Arena = arena }, effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == child.DescriptorAddress ? child : pool).ToImmutableArray() });
    }

    public static NativeBattleStageState SetStageVisibility6C6500(NativeBattleStageState stage, int value)
    {
        stage.Validate();
        return stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & ~2) | ((value & 1) << 1)) }).ToImmutableArray() };
    }

    public static NativeBattleQuezacotlWorkspaceWrite AdvancePhase6C51F1(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        ushort phase = unchecked((ushort)(child.Slots[0].ReadWord(12) + 1));
        child = child.WriteSlot(0, child.Slots[0].WriteWord(12, phase));
        NativeBattleEffectArena arena = (memory.Arena ?? throw new NotSupportedException("Native6C51F1 requires its actual child phase WORD.")).Write(12, [unchecked((byte)phase), (byte)(phase >> 8)]);
        return new(memory with { Arena = arena }, effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == child.DescriptorAddress ? child : pool).ToImmutableArray() });
    }

    public static bool SkipsGeometry6C6060(NativeBattleGfAnimationNode node) => node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0;
}
