namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedContactFreeRetirement(long Token, BattleActionHeaderReference HeaderReference, NativeBattleActionHeaderSlot Header, NativeBattleContactFreeRing Ring);

    private HostedContactFreeRetirement? hostedContactFreeRetirement;

    private void ObserveHostedContactFreeSelection(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        if (hostedPresentationInvocation is not { } invocation || invocation.Header != context.Header || context.Callback != 0x50BB00)
        {
            return;
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(context.Header);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(context.ResolveActor(graphics));
        if (header.Sequence == 0 || header.Source != context.ResolveActor(graphics) || (model.Flags0 & 2) == 0 || model.ReadLinkedModel() is not null ||
            model.ModelId4 is null or 0x8F || model.Flags8 is not uint selectorFlags || (selectorFlags & 0x18) != 0 ||
            model.Sequence != header.Sequence || model.ResourceFlags44 != 1 || model.SequenceState is not { Offset46: 0, Pending3: 0, Alternate43: 0 })
        {
            return;
        }
        NativeBattleContactFreeSequence? proof = NativeBattleContactFreeSequence.Read(NativeActionResource(graphics, header.Source), header.Sequence);
        hostedPresentationInvocation = invocation with { ContactFreeSequence = proof };
    }

    private HostedContactFreeRetirement? ReadHostedContactFreeRetirement(NativeBattleGraphicsState graphics, HostedPresentationInvocation invocation)
    {
        return ReadHostedContactFreeRetirement(graphics, invocation.Token, invocation.Header, invocation.ContactFreeSequence);
    }

    private HostedContactFreeRetirement? ReadHostedContactFreeRetirement(NativeBattleGraphicsState graphics, long token, BattleActionHeaderReference reference, NativeBattleContactFreeSequence? selected)
    {
        if (graphics.ActionGraphics is not { Callback: 0x50BB00 } context || context.Header != reference)
        {
            return null;
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(reference);
        if (header.Sequence == 0 || header.Source != context.ResolveActor(graphics))
        {
            return null;
        }
        // Older private continuations derive the nonprogress proof from their retained selector and execution cursor.
        NativeBattleContactFreeRing? proof = NativeBattleContactFreeRing.Read(graphics.ActorModels, header.Source, header.Sequence, owner => NativeActionResource(graphics, owner), selected);
        return proof is not null ? new(token, reference, header, proof) : null;
    }

    internal bool HasHostedContactFreeRetirement(long token, BattleActionHeaderReference reference, NativeBattleActionHeaderPreparation expected)
    {
        if (token <= 0 || token != lastHostedActionCompleted || token != lastHostedActionIssued || hostedPresentationInvocation is not null || NativeGraphics is not { } graphics)
        {
            return false;
        }
        HostedContactFreeRetirement? retirement = hostedContactFreeRetirement ?? ReadHostedContactFreeRetirement(graphics, token, reference, null);
        if (retirement is null || retirement.Token != token || retirement.HeaderReference != reference)
        {
            return false;
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(reference);
        return header == retirement.Header && header.Source == expected.Source && header.Command == expected.Command &&
            header.Action == expected.Action && header.Effect == expected.Effect && header.Sequence == expected.Sequence && header.Data == expected.Data &&
            graphics.ActionGraphics is { Callback: 0x50BB00 } context && context.Header == reference && context.ResolveActor(graphics) == header.Source &&
            retirement.Ring.Source == header.Source && retirement.Ring.Matches(graphics.ActorModels, owner => NativeActionResource(graphics, owner));
    }
}
