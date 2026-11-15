using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeSelectedModuleTests
{
    [Fact]
    public void Repeated_target_contacts_keep_seventeen_physical_occurrences_under_one_selected_header()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(OriginalRoster, NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]), OriginalAliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(0), 250, 0, -1, ushort.MaxValue, 165, null));
        ImmutableArray<CrossoverNativeMagicTarget>.Builder contacts = ImmutableArray.CreateBuilder<CrossoverNativeMagicTarget>();
        for (int ordinal = 0; ordinal < 17; ordinal++)
        {
            NativeBattleContactHalf contact = new(OriginalOwner(3), 4, 0, 0, 0, checked((ushort)(ordinal + 1)), 0);
            contacts.Add(new(4, contact));
            (packets, _) = packets.AppendContact(new(contact, null));
        }
        (packets, _) = packets.Publish(17);
        NativeBattleActionGraphicsState normalized = NativeBattleActionGraphics.Normalize(packets, header);

        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)17, packets.ContactCount);
        Assert.Equal((byte)17, normalized.FirstHeaderTargetCount);
        Assert.True(normalized.TargetActors.Resolve(packets.ModelAliases).IsSingle(OriginalOwner(3)));
        ImmutableArray<CrossoverNativeMagicTarget> bound = contacts.ToImmutable();
        ImmutableArray<NativeBattleContactPacket> physical = packets.ReadHeaderContacts(header);
        ImmutableArray<int> ordinals = CrossoverNativeMagicPresentation.ReadContactOrdinals(bound, [], physical);
        Assert.Equal(Enumerable.Range(0, 17), ordinals);
        Assert.Throws<InvalidDataException>(() => CrossoverNativeMagicPresentation.ReadContactOrdinals(bound, ordinals, [packets.ReadContact(new BattleContactOccurrence(16))]));
    }

    [Fact]
    public void Selected_module_cast_preserves_the_inherited_source_program_until_the_service_completes()
    {
        NativeBattleGraphicsModel model = new(2)
        {
            ModelId4 = 0,
            ResourceMagicId = 160,
            ResourceFlags44 = 0,
            Sequence = 7,
            SequenceFrame = 13,
            SequenceState = new() { Offset46 = 17 },
            ActiveAnimationDirectory = 0x600000,
            ActiveAnimationBank = "source-program"
        };
        NativeBattleGraphicsResource resource = new([]);
        CrossoverNativeCastState state = CrossoverNativeCastPresentation.Begin(model, resource, ImmutableArray.CreateRange(new byte[39]), 250, 3, 0x50BC20);

        Assert.Same(model, state.Model);
        Assert.True(state.ImmediateInvocation);
        Assert.False(state.ReturnedToStanding);
        CrossoverNativeCastPoll entered = CrossoverNativeCastPresentation.Poll(state, false, false);
        Assert.True(entered.InvokeEffect);
        Assert.Same(model, entered.State.Model);
        Assert.False(CrossoverNativeCastPresentation.Poll(entered.State, false, false).State.Complete);
        CrossoverNativeCastPoll completed = CrossoverNativeCastPresentation.Poll(entered.State, false, true);
        Assert.True(completed.State.Complete);
        Assert.False(completed.ReloadSourceResources);
        Assert.False(completed.State.ReturnedToStanding);
        Assert.Same(model, completed.State.Model);
    }

    [Fact]
    public void Original_selected_limit_family_separates_dispatch_selection_from_the_graphics_header_argument()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Select the original game installation.");
        using BattleAssets assets = new(directory!);
        BattleNativeAction[] family = assets.NativeActions.Where(action => action.Command == 249 && action.KernelSection == 5).ToArray();
        Assert.NotEmpty(family);
        foreach (BattleNativeAction action in family)
        {
            Assert.Equal(action.KernelIndex, action.ActionId);
            BattleNativePresentationInvocation invocation = action.ReadPresentationInvocation();
            Assert.Equal((byte)250, invocation.Command);
            Assert.Equal(ushort.MaxValue, invocation.Argument);
            Assert.Equal((byte)0, invocation.SourceSequence);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(OriginalRoster, NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]), OriginalAliases);
            (packets, BattleActionHeaderReference index) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(0), invocation.Command, invocation.SourceSequence!.Value, -1, invocation.Argument, action.MagicId, null));
            Assert.Equal(0x50BC20u, NativeBattleActionGraphics.SelectService(packets.ReadHeader(index)).Callback);
            Assert.Equal(action.MagicId, packets.ReadHeader(index).Effect);
        }
    }
}
