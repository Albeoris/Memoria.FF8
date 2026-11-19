using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal BattleComponentTextureQuiescenceObservation? ObserveComponentTerminalTextures(ushort effect, BattleDiagnosticInvocationOwners owners, ImmutableArray<string> blockers)
    {
        if (NativeGraphics is not { OriginalEffect: { ReturnedDescriptor: 0 } selected } graphics || selected.RequestedEffect != effect || selected.Binding.Effect != effect ||
            NativeEffectMemory is not { Active: 0 } memory)
        {
            return null;
        }
        nativeActionPackets.Validate();
        BattleDiagnosticTextureQuiescence textures = CaptureDiagnosticTextureQuiescence(graphics);
        BattleComponentTextureQuiescenceObservation observation = new(effect, graphics.Callback, memory.Active, checked((uint)memory.ReadReturnedTask5044B0()), owners, textures, "", blockers);
        if (!observation.IsEligible)
        {
            return null;
        }
        return observation with { ResidentFingerprint = CaptureDiagnosticResidentTextureFingerprint(owners, textures) };
    }

    internal string CaptureDiagnosticResidentTextureFingerprint(BattleDiagnosticInvocationOwners owners, BattleDiagnosticTextureQuiescence textures)
    {
        NativeBattleGraphicsState graphics = Graphics;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        HashValue(hash, owners.ActionHeaders);
        HashValue(hash, owners.ContactRows);
        HashValue(hash, owners.RetainedActiveHeader ?? -1);
        HashValue(hash, owners.RetainedActiveContact ?? -1);
        HashValue(hash, textures.Head!.Value);
        HashValue(hash, textures.Tail!.Value);
        foreach (BattleDiagnosticTextureTask row in textures.Tasks)
        {
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[row.Slot];
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(task.ModelOwner ?? throw new InvalidDataException("Observed texture task lost its live model owner."));
            HashValue(hash, row.Slot);
            HashValue(hash, task.Flags);
            HashValue(hash, task.State);
            HashValue(hash, task.Next ?? -1);
            HashValue(hash, (int)task.Kind);
            HashValue(hash, task.UnknownByteMask);
            hash.AppendData(task.Payload.AsSpan());
            HashValue(hash, task.Program!.Offset);
            HashValue(hash, task.Program.Source.Length);
            hash.AppendData(task.Program.Source.AsSpan());
            HashValue(hash, row.ModelIndex ?? -1);
            if (row.ModelActorId is { } actorId)
            {
                byte[] identity = Encoding.UTF8.GetBytes(actorId);
                HashValue(hash, identity.Length);
                hash.AppendData(identity);
            }
            HashValue(hash, model.Flags0 & 2);
            HashValue(hash, model.ResourceFlags44!.Value & 0x40);
            HashValue(hash, model.Sequence!.Value);
            HashValue(hash, model.ActiveResourceRow100 ?? -1);
            HashValue(hash, model.ActiveStagedResourceRow100 is uint staged ? staged : -1L);
            HashValue(hash, model.ResourceMagicId is ushort magic ? magic : -1L);
            if (model.BodyTextureSlots is { } binding)
            {
                foreach (byte? slot in binding.PaletteRows)
                {
                    HashValue(hash, slot is byte value ? value : -1);
                }
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void HashValue(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    internal static (int? Index, NativeBattleGraphicsModel? Model) ResolveDiagnosticTextureModel(NativeBattleAuxiliaryTask task, NativeBattleGraphicsState graphics, uint? pointer)
    {
        if (task.ModelOwner is not { } owner || !graphics.ActorModels.Contains(owner))
        {
            return (null, null);
        }
        int? index = pointer is uint original && NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(original, out NativeBattleModelSlot slot) &&
            graphics.NativeBindings.FindOriginalOwner(slot, graphics.ActorModels) == owner ? slot.Offset : null;
        return (index, graphics.ActorModels.Get(owner));
    }

    internal static BattleDiagnosticTextureQuiescence CaptureDiagnosticTextureQuiescence(NativeBattleGraphicsState graphics)
    {
        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary;
        ImmutableArray<NativeBattleAuxiliaryTask> slots = auxiliary.Slots;
        if (slots.IsDefault)
        {
            return new(0, 0, auxiliary.Head, auxiliary.Tail, false, []);
        }
        bool complete = slots.Length == 16;
        int active = 0;
        foreach (NativeBattleAuxiliaryTask task in slots)
        {
            if (task is null)
            {
                complete = false;
            }
            else if ((task.Flags & 1) != 0)
            {
                active++;
            }
        }
        ImmutableArray<BattleDiagnosticTextureTask>.Builder rows = ImmutableArray.CreateBuilder<BattleDiagnosticTextureTask>();
        HashSet<int> visited = [];
        int? next = auxiliary.Head;
        while (next is int slot)
        {
            if ((uint)slot >= (uint)slots.Length || rows.Count >= 16 || !visited.Add(slot) || slots[slot] is not { } task)
            {
                complete = false;
                break;
            }
            bool known = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 && !task.Payload.IsDefault && task.Payload.Length == 32;
            uint? pointer = known && (task.UnknownByteMask & 0xF) == 0 ? BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan()) : null;
            byte? sequence = known && (task.UnknownByteMask & 0x200) == 0 ? task.Payload[9] : null;
            (int? index, NativeBattleGraphicsModel? model) = ResolveDiagnosticTextureModel(task, graphics, pointer);
            NativeBattleAuxiliaryProgram? program = task.Kind == NativeBattleAuxiliaryKind.TextureAnimation5057D0 ? task.Program : null;
            int? length = program is not null && !program.Source.IsDefault ? program.Source.Length : null;
            byte? deadline = program is not null && !program.Source.IsDefault && (uint)program.Offset < program.Source.Length ? program.Source[program.Offset] : null;
            rows.Add(new(slot, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Payload.IsDefault ? -1 : task.Payload.Length, pointer, sequence, program?.Offset, length, deadline, index, model?.Flags0, model?.ResourceFlags44, model?.Sequence)
            {
                ModelActorId = model is not null && task.ModelOwner is { } owner && owner is BattlePartyActorModel or BattleEnemyActorModel ? ReadGraphicsPresentationId(owner) : null
            });
            complete &= (task.Flags & 1) != 0;
            next = task.Next;
        }
        complete &= next is null && rows.Count == active && (rows.Count == 0 ? auxiliary.Head is null && auxiliary.Tail is null : rows[rows.Count - 1].Slot == auxiliary.Tail);
        return new(slots.Length, active, auxiliary.Head, auxiliary.Tail, complete, rows.ToImmutable());
    }
}
