namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    public MenuSound FieldMagicUseSound(int magicId, bool changed) => NativeMenuUseFeedback.Magic(kernel.MagicSpells[magicId].AttackType, changed);
}
