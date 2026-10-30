using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleHeavyAllocatedCaptureAssertions(ushort selectedEffect)
{
    private readonly HashSet<uint> banks = [];
    private readonly HashSet<long> resources = [];
    private int frames;

    internal void Observe(BattleSnapshot prior, BattleSnapshot current)
    {
        if (current.NativeGraphics?.OriginalEffect is not { } effect || effect.Binding.Effect != selectedEffect || current.NativeGraphics.PrimitiveFrame is not { } primitive || !(effect.ReturnedDescriptor is > 0 || prior.NativeGraphics?.OriginalEffect?.ReturnedDescriptor is > 0))
        {
            return;
        }
        ImmutableHashSet<NativeBattlePrimitiveReference> references = effect.PresentationPackets.Select(NativeBattlePrimitiveProcess.NormalizeCommonReference).ToImmutableHashSet();
        NativeBattleLogicalCommand[] commands = primitive.LastCommands.Select(item => item.Command).Where(command => references.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet)) && (command.Opcode is >= 0x20 and < 0x40 or >= 0x60 and < 0x80)).ToArray();
        foreach (NativeBattleLogicalCommand command in commands)
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(command.PilotGeometry);
            Assert.Equal(selectedEffect, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.True(geometry.CornerCount is 3 or 4);
            banks.Add(geometry.Identity.Bank);
            resources.Add(geometry.Identity.ResourceGeneration);
        }
        if (commands.Length > 0)
        {
            frames++;
        }
    }

    internal void Accept()
    {
        Assert.True(frames > 1, "The allocated component must reach captured physical geometry on multiple native callbacks.");
        if (selectedEffect == 64)
        {
            Assert.Contains(NativeBattleEffect64Profile.MainDescriptor, banks);
            Assert.Contains(NativeBattleEffect64Profile.ProgrammeDescriptor, banks);
            Assert.Contains(NativeBattleEffect64Profile.ParticleDescriptor, banks);
        }
        else
        {
            Assert.Contains(NativeBattleEffect66Profile.ChildDescriptor, banks);
            Assert.Contains(0xDE90CCL, resources);
            Assert.Contains(142L, resources);
        }
    }
}
