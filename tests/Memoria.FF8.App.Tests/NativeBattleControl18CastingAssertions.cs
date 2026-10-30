using System.Collections.Immutable;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleControl18CastingAssertions
{
    internal static bool HasCasting(BattleSnapshot snapshot)
    {
        return snapshot.NativeGraphics!.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && NativeBattleControl18Presentation.IsCasting(task.Kind));
    }

    internal static void AssertTasks(BattleSnapshot snapshot)
    {
        NativeBattleModuleState modules = snapshot.NativeGraphics!.ModuleEffects;
        NativeBattleCastingPresentationState? presentation = modules.CastingPresentation;
        foreach ((NativeBattleModuleTask task, int slot) in modules.Slots.Select((task, slot) => (task, slot)).Where(item => (item.task.Flags & 1) != 0))
        {
            Assert.True(NativeBattleControl18Presentation.IsCasting(task.Kind), $"Unrelated shared module {task.Kind} is outside the isolated original18 component.");
            Assert.NotNull(presentation);
            Assert.True(presentation!.Cinematic18Birth > 0);
            Assert.True(presentation.Tasks.TryGetValue(slot, out NativeBattleCastingTaskProvenance? entry), "The selected older active18 casting source requires explicit presentation provenance before use.");
            Assert.Equal(task.Kind, entry!.Kind);
            Assert.Equal((ushort)18, entry.Identity.Effect);
            Assert.Equal(NativeBattleModuleState.DescriptorAddress, entry.Identity.Bank);
            Assert.Equal(slot, entry.Identity.Slot);
            Assert.True(entry.Identity.Generation > 0 && entry.Identity.Generation < presentation.NextBirth);
            Assert.Equal((ushort)18, entry.Parent.Effect);
            NativeBattleControl18Presentation.Validate(entry.Source);
            Assert.Equal((byte)0, entry.Source.Actor);
            Assert.Equal(30, entry.Source.Sequence);
            Assert.Equal(0x90, entry.Source.ScriptOffset);
            Assert.Equal((ushort)18, entry.Source.Identity.Effect);
            NativeBattleOriginalEffectState selected = snapshot.NativeGraphics.OriginalEffect!;
            NativeBattleEffectResourcePointer pointer = Assert.Single(selected.ResourcePointers, item => NativeBattleEffectNodeMemory.ArenaAddress + item.ArenaOffset == entry.Source.FileAddress);
            NativeBattleEffectInitializerAsset asset = Assert.Single(NativeBattleEffectInitializerPolicy.Assets(selected.Binding), item => item.Name == "mag017_b.00");
            Assert.Equal(0x42, BinaryPrimitives.ReadUInt16LittleEndian(asset.Data.AsSpan(entry.Source.ScriptOffset, 2)));
            Assert.Equal(entry.Source.Sequence, BinaryPrimitives.ReadInt16LittleEndian(asset.Data.AsSpan(entry.Source.ScriptOffset + 2, 2)));
            Assert.Equal(entry.Source.FileAddress + (uint)entry.Source.ScriptOffset, entry.Source.ScriptAddress);
            if (task.Kind == NativeBattleModuleKind.CastingSeed574C80)
            {
                Assert.Equal(0x1D972C0u, BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan(20, 4)));
            }
        }
    }

    internal static NativeBattleLogicalCommand[] Commands(BattleSnapshot snapshot)
    {
        NativeBattleOriginalEffectState effect = snapshot.NativeGraphics!.OriginalEffect!;
        NativeBattleCinematicPacketScope? scope = effect.PresentationPacketScope;
        ImmutableHashSet<NativeBattlePrimitiveReference> auxiliary = effect.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
        return snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(Owned).ToArray();

        bool Owned(NativeBattleLogicalCommand command)
        {
            if (command.Opcode is not (>= 0x20 and < 0x80) && command.Opcode != 0xE1)
            {
                return false;
            }
            NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet);
            if (auxiliary.Contains(normalized))
            {
                return true;
            }
            if (scope is null)
            {
                return false;
            }
            if (command.Packet.Bank == "20DFAB8")
            {
                uint address = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)command.Packet.ByteOffset);
                return command.Packet.Buffer == 0 && scope.PrivateStart >= NativeBattleEffectNodeMemory.ArenaAddress && address >= scope.PrivateStart && address < scope.PrivateEnd;
            }
            NativeBattlePrimitiveReference start = NativeBattlePrimitiveProcess.NormalizeCommonReference(new(scope.CommonBank, scope.CommonBuffer, scope.CommonStart));
            NativeBattlePrimitiveReference end = NativeBattlePrimitiveProcess.NormalizeCommonReference(new(scope.CommonBank, scope.CommonBuffer, scope.CommonEnd));
            return normalized.Bank == start.Bank && normalized.Buffer == start.Buffer && normalized.ByteOffset >= start.ByteOffset && normalized.ByteOffset < end.ByteOffset;
        }
    }

    internal static int AssertFrame(BattleSnapshot snapshot)
    {
        AssertTasks(snapshot);
        NativeBattleLogicalCommand[] geometry = Commands(snapshot).Where(command => command.Opcode is >= 0x20 and < 0x80).ToArray();
        foreach (NativeBattleLogicalCommand command in geometry)
        {
            NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal((ushort)18, visual.Identity.Effect);
            Assert.True(visual.Identity.Generation > 0);
            if (visual.ScreenSpace)
            {
                Assert.False(visual.Continuous);
            }
        }
        return geometry.Length;
    }
}
