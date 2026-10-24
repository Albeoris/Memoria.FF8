using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void CompleteNativeEffect79File(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect79 file completion requires its current process memory.");
        NativeBattleEffect79State state = memory.Effect79 ?? throw new NotSupportedException("Effect79 file completion requires its current control owner.");
        if (callback == 0x7BE1E0)
        {
            NativeEffectMemory = memory with { Effect79 = state.Write(0x25DF340, [1]) };
            return;
        }
        uint control = state.ReadDword(0x25EAD84);
        if (control != NativeBattleEffect79Profile.ControlAddress)
        {
            throw new InvalidDataException("Effect79 completion requires its actual control pointer.");
        }
        if (callback is 0x7B53C0 or 0x7B53F0)
        {
            NativeBattleOriginalEffectState effect = NativeGraphics?.OriginalEffect is { Binding.Entry: NativeBattleEffect79Profile.Entry } selected ? selected : throw new NotSupportedException("Effect79 file completion lost its selected resources.");
            NativeBattleEffect79Resources resources = effect.Binding.Effect79Resources ?? throw new NotSupportedException("Effect79 completion requires its original file resources.");
            uint source = state.ReadDword(state.ReadDword(0x25EA348) + 0xC);
            NativeBattleEffect79Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect79 completion requires its current random stream."), state) { Staging = NativeFileStaging };
            if (callback == 0x7B53C0)
            {
                NativeBattleEffectHeapAllocation tim = new("effect79-file-texture", NativeBattleEffect79Controller.ReadBytes(frame, source, resources.ReadFile(400).Data.Length).Read(0, resources.ReadFile(400).Data.Length).ToImmutableArray());
                memory = memory with { Textures = (memory.Textures ?? throw new NotSupportedException("Effect79 completion requires its current texture queue.")).EnqueueTim505E30(tim).State };
            }
            else
            {
                int number = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(NativeBattleEffect79Controller.ReadBytes(frame, source, 4).Read(0, 4));
                memory = memory with { SummonStream = (memory.SummonStream ?? throw new NotSupportedException("Effect79 requires its current stream owner.")).Publish501860(source, number) };
                state = state.Write(control + 0x16A, [1]);
            }
        }
        else if (callback is not (0x7B5420 or 0x7B5430 or 0x7B5440 or 0x7B5450))
        {
            throw new NotSupportedException("Effect79 selected an unsupported file completion callback.");
        }
        NativeEffectMemory = memory with { Effect79 = state.WriteWord(control + 0x164, 0) };
    }

    private bool TryWriteNativeEffect79Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (address == 0x25DF340 || address == NativeBattleEffect79Profile.ControlAddress + 0x16A)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect79 completion requires its current process memory.");
            NativeEffectMemory = memory with { Effect79 = (memory.Effect79 ?? throw new NotSupportedException("Effect79 completion requires its retained byte owner.")).Write(address, [value]) };
            return true;
        }
        uint delta = unchecked(address - NativeBattleEffect79Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleEffect79Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = Write(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect79Profile.Entry } current ? current : throw new NotSupportedException("Effect79 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = Write(effect) };
        }
        return true;

        NativeBattleOriginalEffectState Write(NativeBattleOriginalEffectState effect)
        {
            uint taskAddress = address - 0x62;
            NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
            return effect with { Pools = hierarchy.Write(taskAddress, hierarchy.Read(taskAddress).WriteByte(0x62, value)).Pools };
        }
    }

    private byte[] ReadNativeMusicSource501A70(uint address, int length)
    {
        if (length >= 0 && address >= NativeBattleEffect344Profile.MusicAddress && (ulong)address + (uint)length <= (ulong)NativeBattleEffect344Profile.MusicAddress + NativeBattleEffect344Profile.MusicLength)
        {
            NativeBattleOriginalEffectState effect = NativeGraphics?.OriginalEffect is { Binding.Entry: NativeBattleEffect344Profile.Entry } selected ? selected : throw new NotSupportedException("Native501A70 static source requires its selected original binding.");
            return (effect.Binding.Effect344Resources ?? throw new NotSupportedException("Native501A70 static source requires its original resource.")).ReadStatic(address, length);
        }
        uint offset = unchecked(address - 0x20DFAB8);
        if (offset < NativeBattleEffectArena.Length && (ulong)offset + (uint)length <= NativeBattleEffectArena.Length)
        {
            return (NativeEffectMemory?.Arena ?? throw new NotSupportedException("Native501A70 requires its current COMMON source.")).Read(checked((int)offset), length);
        }
        return (NativeFileStaging ?? throw new NotSupportedException("Native501A70 requires its current file source.")).Read(address, length);
    }
}
