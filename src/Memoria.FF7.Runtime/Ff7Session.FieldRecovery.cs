using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private IReadOnlyList<KernelSection> FieldRecoveryKernel => battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));

    public bool SupportsFieldItem(int itemId) => Ff7NativeFieldRecovery.SupportsItem(itemId, FieldRecoveryKernel);

    public IReadOnlyList<Ff7BattleSpell> ReadFieldMagic(int casterId)
    {
        return Ff7NativeFieldRecovery.Resident(Progress, casterId, GuestFieldVitals?.StoryCharacters) is { } caster
            ? Ff7NativeFieldRecovery.ReadMagic(caster, FieldRecoveryKernel)
            : [];
    }

    public bool CanUseFieldItem(int itemId, int targetId)
    {
        return CanUsePlayerMenu && SupportsFieldItem(itemId) && Progress.Inventory.Any(slot => slot.ItemId == itemId && slot.Quantity > 0)
            && Ff7NativeFieldRecovery.Resident(Progress, targetId, GuestFieldVitals?.StoryCharacters) is { } target
            && Ff7NativeFieldRecovery.CanUseItem(target, itemId);
    }

    public bool CanCastFieldMagic(int casterId, Ff7BattleSpell spell, int targetId)
    {
        IReadOnlyCollection<int>? guests = GuestFieldVitals?.StoryCharacters;
        return CanUsePlayerMenu && Ff7NativeFieldRecovery.Resident(Progress, casterId, guests) is { } caster
            && caster.MagicPoints >= spell.Attack.CastingCost && Ff7NativeFieldRecovery.Resident(Progress, targetId, guests) is { } target
            && Ff7NativeFieldRecovery.CanCastMagic(target, spell.AttackId);
    }

    public bool TryUseFieldItem(int itemId, int targetId)
    {
        if (!CanUsePlayerMenu || !Ff7NativeFieldRecovery.TryUseItem(Progress, itemId, targetId, FieldRecoveryKernel, GuestFieldVitals?.StoryCharacters, out Ff7Progress result))
        {
            return false;
        }

        Progress = result;
        Host.SubmitNativeSound(new(5, 263));
        return true;
    }

    public bool TryCastFieldMagic(int casterId, int spellId, int targetId)
    {
        if (!CanUsePlayerMenu || !Ff7NativeFieldRecovery.TryCastMagic(Progress, casterId, spellId, targetId, FieldRecoveryKernel, executable.NativeMateriaStatModifiers, GuestFieldVitals?.StoryCharacters, out Ff7Progress result))
        {
            return false;
        }

        Progress = result;
        Host.SubmitNativeSound(new(5, 263));
        return true;
    }
}
