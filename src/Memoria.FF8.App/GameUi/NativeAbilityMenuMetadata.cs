using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public static class NativeAbilityMenuMetadata
{
    public static UiImageSource? Icon(int ability)
    {
        // Native4A62E0 uses the same seven category boundaries emitted by Native4ACB70.
        return ability switch
        {
            >= 0 and < 20 => new UiImageSource(UiImageKind.Icon, 216),
            >= 20 and < 39 => new UiImageSource(UiImageKind.Icon, 217),
            >= 39 and < 58 => new UiImageSource(UiImageKind.Icon, 218),
            >= 58 and < 78 => new UiImageSource(UiImageKind.Icon, 219),
            >= 78 and < 83 => new UiImageSource(UiImageKind.Icon, 220),
            >= 83 and < 92 => new UiImageSource(UiImageKind.Icon, 221),
            >= 92 and < 116 => new UiImageSource(UiImageKind.Icon, 222),
            _ => null
        };
    }
}
