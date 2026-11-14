using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void UploadNativeStatusPalette509E00(byte physicalIndex, uint flags)
    {
        NativeBattleModelCpuResources resources = NativeModelCpuResources ?? throw new NotSupportedException("Native509E00 requires the saved original party palettes.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native509E00 requires its process texture and packet owners.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native509E00 requires the actual505DF0 queue.");
        var material = SelectNativeModelTextureBank(physicalIndex, textures);
        byte[] original;
        int textureSlot = physicalIndex;
        if (material is not null)
        {
            if (resources.Cpu.PresentationRoster is not { } paletteRoster || !paletteRoster.Rows.SequenceEqual(NativeGraphics!.PresentationRoster!.Rows))
            {
                throw new InvalidDataException("Hosted status palette requires the same actor roster as its saved palettes.");
            }
            original = resources.ReadPartyStatusPalette(physicalIndex);
            textureSlot = physicalIndex < NativeBattleSlots.AllyCount ? physicalIndex : 0;
        }
        else
        {
            original = resources.Palettes.Read(physicalIndex * 1024, 1024);
        }
        NativeBattleEffectHeapAllocation source;
        if ((flags & 0x410) != 0)
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native509E00 requires its retained scratch packet cursor.");
            string bank = process.PacketBank ?? throw new NotSupportedException("Native509E00 requires the allocated native packet bank.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Native509E00 requires the observed native packet cursor.");
            NativeBattlePrimitiveProcess next = process with { ByteCursor = checked(cursor + 1024) };
            next.Validate();
            source = new($"509E00:{bank}:{process.PacketBuffer}:{cursor}", NativeBattleStatusPalette.Transform509E00(original, flags));
            next = next.ApplyCommonWrite(cursor, source.Data, Enumerable.Repeat(NativeBattleGfPacketByteState.Known, source.Data.Length).ToImmutableArray());
            memory = memory with { PrimitiveProcess = next };
        }
        else
        {
            source = new($"1D98B60:{physicalIndex}", NativeBattleStatusPalette.Transform509E00(original, flags));
        }
        NativeBattleEffectTextureState updated = (material?.Bank.Textures ?? textures).EnqueueRectangle505DF0(new(0, checked((short)(224 + 2 * textureSlot)), 256, 2), source);
        NativeEffectMemory = memory with { Textures = PublishNativeModelTextures(textures, material, updated) };
    }
}
