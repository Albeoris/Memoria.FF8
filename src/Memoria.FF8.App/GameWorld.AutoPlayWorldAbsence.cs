using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record AutoPlayWorldAbsenceReceipt(int Version, byte EntityType, ushort Story, string AllocationFingerprint);

internal sealed partial class GameWorld
{
    internal AutoPlayWorldAbsenceReceipt? InspectAutoPlayWorldAbsence(NativeWorldTransitionRequest request)
    {
        ValidateWorldDeparture(request);
        byte entityType = unchecked((byte)request.Transition.Mode);
        if (entityType > 2)
        {
            return null;
        }
        NativeWorldEntryMemory memory = session.State.WorldEntryMemory ?? throw new NotSupportedException("World absence inspection requires the actual typed native story memory.");
        ushort story = memory.StoryWord;
        return CreateAutoPlayWorldAbsenceReceipt(entityType, story, ReadAutoPlayWorldAbsenceClause(story));
    }

    internal string ReadAutoPlayWorldAbsenceFingerprint(AutoPlayWorldAbsenceReceipt receipt)
    {
        if (receipt.Version != 1 || receipt.EntityType > 2 || string.IsNullOrEmpty(receipt.AllocationFingerprint))
        {
            throw new InvalidDataException("The world absence receipt has an unsupported version, entity type or allocation identity.");
        }
        NativeWorldEntryMemory memory = session.State.WorldEntryMemory ?? throw new NotSupportedException("World absence inspection requires the actual typed native story memory.");
        ushort story = memory.StoryWord;
        if (story != receipt.Story)
        {
            return "changed-story:" + story.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return ComputeAutoPlayWorldAllocationFingerprint(receipt.EntityType, story, assets.ReadNativeWorldAllocationClause(story));
    }

    private NativeWorldAllocationClause? ReadAutoPlayWorldAbsenceClause(ushort story)
    {
        try
        {
            return assets.ReadNativeWorldAllocationClause(story);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static AutoPlayWorldAbsenceReceipt? CreateAutoPlayWorldAbsenceReceipt(byte entityType, ushort story, NativeWorldAllocationClause? clause)
    {
        if (entityType > 2 || clause is null || story < clause.MinimumStory || story >= clause.MaximumStory || clause.HasRequestedFootObject(entityType) != false)
        {
            return null;
        }
        return new(1, entityType, story, ComputeAutoPlayWorldAllocationFingerprint(entityType, story, clause));
    }

    internal static string ComputeAutoPlayWorldAllocationFingerprint(byte entityType, ushort story, NativeWorldAllocationClause clause)
    {
        if (entityType > 2 || story < clause.MinimumStory || story >= clause.MaximumStory)
        {
            throw new InvalidDataException("The world allocation fingerprint requires its selected story clause and normal foot entity type.");
        }
        using MemoryStream bytes = new();
        using (BinaryWriter writer = new(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("ff8-autoplay-world-absence-v1");
            writer.Write(entityType);
            writer.Write(story);
            writer.Write(clause.Index);
            writer.Write(clause.Offset);
            writer.Write(clause.Length);
            writer.Write(clause.MinimumStory);
            writer.Write(clause.MaximumStory);
            writer.Write(clause.Words.Length);
            foreach (ushort word in clause.Words)
            {
                writer.Write(word);
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
}
