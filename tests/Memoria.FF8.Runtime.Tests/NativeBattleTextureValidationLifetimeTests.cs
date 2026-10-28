using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureValidationLifetimeTests
{
    [Fact]
    public void Repeated_validation_of_retained_metadata_and_pixels_does_not_allocate()
    {
        ImmutableArray<NativeBattleTextureRegistration> registrations = Registrations();
        NativeBattlePcTextureWindowPixels pixels = new(0, 0, 1, 1, [0, 0]) { TextureRegistrations = registrations };
        NativeBattlePrimitiveTextureWindow window = new(0, 0, 4, 1);
        for (int index = 0; index < 1000; index++)
        {
            NativeBattleTextureCopyHistory.Validate(registrations);
            pixels.ValidateMapping(0, window);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            NativeBattleTextureCopyHistory.Validate(registrations);
            pixels.ValidateMapping(0, window);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void New_metadata_and_record_copies_require_their_own_validation()
    {
        ImmutableArray<NativeBattleTextureRegistration> valid = Registrations();
        NativeBattleTextureCopyHistory.Validate(valid);
        ImmutableArray<NativeBattleTextureRegistration> invalid = [valid[0] with { OwnedSpans = [new(1, 0, 1, 1)] }];
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate(invalid));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate(invalid));
        NativeBattlePcTextureWindowPixels original = new(0, 0, 1, 1, [0, 0]) { TextureRegistrations = valid };
        original.Validate();
        NativeBattlePcTextureWindowPixels changed = original with { Width = 2 };
        Assert.Throws<InvalidDataException>(changed.Validate);
        Assert.Throws<InvalidDataException>(changed.Validate);
        original.Validate();
        NativeBattleTextureOrigin other = valid[0].Origin;
        ImmutableArray<NativeBattleTextureRegistration> overlapping = [valid[0], new(other, 2, 0, 1, 1, []) { PaletteOwnedSpans = [new(0, 0, 1, 1)] }];
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate(overlapping));
    }

    [Fact]
    public void Validation_receipts_do_not_retain_released_pixel_owners_or_metadata()
    {
        (WeakReference pixels, WeakReference registrations) = ValidateReleasedOwners();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(pixels.IsAlive);
        Assert.False(registrations.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) ValidateReleasedOwners()
    {
        ImmutableArray<NativeBattleTextureRegistration> registrations = Registrations();
        NativeBattlePcTextureWindowPixels pixels = new(0, 0, 1, 1, [0, 0]) { TextureRegistrations = registrations };
        pixels.Validate();
        return (new(pixels), new(ImmutableCollectionsMarshal.AsArray(registrations)!));
    }

    private static ImmutableArray<NativeBattleTextureRegistration> Registrations()
    {
        NativeBattleTextureOrigin origin = new("effect.tim", 0, NativeBattleTextureRole.Pixels, 4, 1, 1);
        return [new(origin, 0, 0, 1, 1, [])];
    }
}
