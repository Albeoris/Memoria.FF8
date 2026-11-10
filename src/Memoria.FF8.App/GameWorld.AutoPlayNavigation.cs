using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

internal sealed record AutoPlayNavigationReceipt(int Version, int FieldId, string ProgramHash, string WalkmeshHash, IReadOnlyList<int> LockedTriangles, IReadOnlyList<int> ReachableTriangles)
{
    private static readonly ConditionalWeakTable<AutoPlayNavigationReceipt, string> IdentityHashes = new();
    public IReadOnlyList<int> LockedTriangles { get; } = Array.AsReadOnly(LockedTriangles.ToArray());
    public IReadOnlyList<int> ReachableTriangles { get; } = Array.AsReadOnly(ReachableTriangles.ToArray());

    internal string Key(string visitKey)
    {
        string hash = IdentityHashes.GetValue(this, static receipt =>
        {
            string identity = $"{receipt.Version}|{receipt.FieldId}|{receipt.ProgramHash}|{receipt.WalkmeshHash}|{string.Join(',', receipt.LockedTriangles)}|{string.Join(',', receipt.ReachableTriangles)}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        });
        return visitKey + ":nav:" + hash;
    }
}

internal sealed partial class GameWorld
{
    internal AutoPlayNavigationReceipt CaptureAutoPlayNavigationReceipt()
    {
        if (Field is null || !HasCurrentFieldAllocation || Events.PlayerActor is not { } player)
        {
            throw new NotSupportedException("Navigation observation requires the actual allocated field and its native player.");
        }
        return CaptureAutoPlayNavigationReceipt(Field.Id, ReadAutoPlayFieldProgramHash(Field.Id), Field.WalkmeshHash ?? throw new NotSupportedException("Navigation observation requires the original walkmesh identity."), Field.Walkmesh, player.WalkmeshTriangle, Events.Scene.LockedTriangles);
    }

    internal static AutoPlayNavigationReceipt CaptureAutoPlayNavigationReceipt(int fieldId, string programHash, string walkmeshHash, FieldWalkmesh walkmesh, int playerTriangle, IReadOnlySet<int> locked)
    {
        int[] reachable = AutoPlayReachableTriangles(walkmesh, playerTriangle, locked).Order().ToArray();
        if (reachable.Length == 0)
        {
            throw new NotSupportedException("The native player has no unlocked connected walkmesh component.");
        }
        return new(1, fieldId, programHash, walkmeshHash, locked.Order().ToArray(), reachable);
    }

    internal bool IsAutoPlayNavigationResourceCurrent(AutoPlayNavigationReceipt receipt)
    {
        if (receipt.Version != 1)
        {
            throw new InvalidDataException("The navigation receipt has an unsupported version.");
        }
        return ReadAutoPlayFieldProgramHash(receipt.FieldId) == receipt.ProgramHash && assets.ReadFieldWalkmeshHash(receipt.FieldId) == receipt.WalkmeshHash;
    }
}
