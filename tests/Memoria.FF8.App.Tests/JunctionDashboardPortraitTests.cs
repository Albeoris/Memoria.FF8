using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardPortraitTests
{
    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, false)]
    public void CharacterHeaderUsesPortraitsAndOnlyExistingNeighbours(int selected, bool previous, bool next)
    {
        MenuData data = Fixture(3, 0);
        MenuController menu = Open(data);
        menu.Execute(new("select-character", data.Characters[selected].Id));
        UiView view = menu.BuildView();
        UiImage portrait = Assert.IsType<UiImage>(Descendants(view.Root).Single(element => element.Id == "junction-character-portrait"));
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, data.Characters[selected].NativeId!.Value), portrait.Source);
        Assert.DoesNotContain(Descendants(view.Root), element => element.Id == "command-attention");
        UiElement? before = Descendants(view.Root).SingleOrDefault(element => element.Id == "previous-character");
        UiElement? after = Descendants(view.Root).SingleOrDefault(element => element.Id == "next-character");
        Assert.Equal(previous, before is not null);
        Assert.Equal(next, after is not null);
        if (before is not null)
        {
            AssertNeighbour(before, data.Characters[selected - 1].NativeId!.Value, view);
            Assert.DoesNotContain(menu.Entries, entry => entry.Id == before.Id);
            menu.Navigate(MenuInput.PageLeft);
            Assert.Equal(data.Characters[selected - 1].Id, menu.Character?.Id);
        }
        menu.Execute(new("select-character", data.Characters[selected].Id));
        if (after is not null)
        {
            UiElement fresh = Descendants(menu.BuildView().Root).Single(element => element.Id == "next-character");
            AssertNeighbour(fresh, data.Characters[selected + 1].NativeId!.Value, menu.BuildView());
            Assert.DoesNotContain(menu.Entries, entry => entry.Id == fresh.Id);
            menu.Navigate(MenuInput.PageRight);
            Assert.Equal(data.Characters[selected + 1].Id, menu.Character?.Id);
        }
    }

    [Fact]
    public void SoleCharacterHasNoNavigationPortraits()
    {
        MenuController menu = Open(Fixture(1, 0));
        UiElement[] elements = Descendants(menu.BuildView().Root).ToArray();
        Assert.Single(elements.OfType<UiImage>(), image => image.Id == "junction-character-portrait");
        Assert.DoesNotContain(elements, element => element.Id is "previous-character" or "next-character");
    }

    [Theory]
    [InlineData(0, MenuInput.PageLeft)]
    [InlineData(2, MenuInput.PageRight)]
    public void CharacterSwitchingDoesNotWrapAtThePortraitBoundaries(int selected, MenuInput input)
    {
        MenuData data = Fixture(3, 0);
        MenuController menu = Open(data);
        menu.Execute(new("select-character", data.Characters[selected].Id));
        menu.Navigate(input);
        Assert.Equal(data.Characters[selected].Id, menu.Character?.Id);
    }

    [Theory]
    [InlineData(1280, 720, "slot-HP")]
    [InlineData(1280, 720, "slot-Elem-Atk")]
    [InlineData(1280, 720, "elemental-attack")]
    [InlineData(1280, 720, "elemental-defense")]
    [InlineData(3440, 1440, "slot-HP")]
    [InlineData(3440, 1440, "slot-Elem-Atk")]
    [InlineData(3440, 1440, "elemental-attack")]
    [InlineData(3440, 1440, "elemental-defense")]
    public void UpFromDashboardTopSelectsTheHorizontallyNearestGuardianForce(int width, int height, string sourceId)
    {
        MenuController menu = Open(Fixture(3, 7), width, height);
        UiView view = menu.BuildView();
        UiElement source = Descendants(view.Root).Single(element => element.Id == sourceId);
        Assert.True(view.Focus(source));
        view = menu.BuildView();
        source = Descendants(view.Root).Single(element => element.Id == sourceId);
        UiButton nearest = Descendants(view.Root).OfType<UiButton>()
            .Where(button => button.Id?.StartsWith("gf-portrait-", StringComparison.Ordinal) == true)
            .MinBy(button => Math.Abs(CenterX(button) - CenterX(source)))!;
        Assert.True(nearest.Bounds.Bottom < source.Bounds.Y);

        menu.Navigate(MenuInput.Up);

        Assert.Equal(nearest.Id, menu.BuildView().FocusedElement?.Id);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(3440, 1440)]
    public void UpWithinStatsKeepsMovingToThePreviousStat(int width, int height)
    {
        MenuController menu = Open(Fixture(3, 7), width, height);
        UiView view = menu.BuildView();
        Assert.True(view.Focus(Descendants(view.Root).Single(element => element.Id == "slot-Str")));

        menu.Navigate(MenuInput.Up);

        Assert.Equal("slot-HP", menu.BuildView().FocusedElement?.Id);
    }

    private static float CenterX(UiElement element) => element.Bounds.X + element.Bounds.Width / 2;

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(640, 448)]
    public void AcquiredGuardianForcePortraitsFitOneSquareRowAndReflectOwnership(int width, int height)
    {
        MenuData data = Fixture(3, 16);
        data.GuardianForces[0].Owner = new(data.Characters[0].Id, data.Characters[0].NativeId, data.Characters[0].Name);
        data.GuardianForces[1].Owner = new(data.Characters[1].Id, data.Characters[1].NativeId, data.Characters[1].Name);
        data.Characters[0].GuardianForceIds.Add(data.GuardianForces[0].Id);
        MenuController menu = Open(data, width, height);
        UiView view = menu.BuildView();
        UiButton[] controls = Descendants(view.Root).OfType<UiButton>().Where(button => button.Id?.StartsWith("gf-portrait-", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(16, controls.Length);
        Assert.DoesNotContain(Descendants(view.Root), element => element.Id == "manage-gfs");
        float firstY = controls[0].Bounds.Y;
        for (int index = 0; index < controls.Length; index++)
        {
            UiButton control = controls[index];
            UiImage image = Assert.IsType<UiImage>(Descendants(control).Single(element => element.Id == "gf-image-" + data.GuardianForces[index].Id));
            Assert.Equal(UiImageKind.GuardianForcePortrait, image.Source?.Kind);
            Assert.Equal(data.GuardianForces[index].NativeId, image.Source?.Id);
            Assert.Equal(index == 0 ? (byte)255 : (byte)128, image.Source?.Opacity);
            Assert.InRange(Math.Abs(control.Bounds.Y - firstY), 0, 0.01f);
            Assert.InRange(Math.Abs(image.Bounds.Width - image.Bounds.Height), 0, 0.01f);
            Assert.True(image.Bounds.Width > 0);
            Assert.True(control.Bounds.X >= view.Root.Bounds.X - 0.01f && control.Bounds.Right <= view.Root.Bounds.Right + 0.01f);
            if (index > 0)
            {
                Assert.True(controls[index - 1].Bounds.Right <= control.Bounds.X + 0.01f);
            }
        }
    }

    [Fact]
    public void AssignedOwnerPortraitsStayVisibleAboveTheirGuardianForces()
    {
        MenuData data = Fixture(2, 3);
        data.GuardianForces[0].Owner = new(data.Characters[0].Id, data.Characters[0].NativeId, data.Characters[0].Name);
        data.Characters[0].GuardianForceIds.Add(data.GuardianForces[0].Id);
        data.GuardianForces[1].Owner = new(data.Characters[1].Id, 7, "Same display name");
        MenuController menu = Open(data);
        UiView view = menu.BuildView();
        Assert.Equal(2, VisibleDescendants(view.Root).Count(element => element.Id?.StartsWith("gf-owner-", StringComparison.Ordinal) == true));
        UiImage selectedOwner = Assert.IsType<UiImage>(VisibleDescendants(view.Root).Single(element => element.Id == "gf-owner-" + data.GuardianForces[0].Id));
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, data.Characters[0].NativeId!.Value), selectedOwner.Source);
        Assert.True(view.Focus(Descendants(view.Root).Single(element => element.Id == "gf-portrait-" + data.GuardianForces[0].Id)));
        view = menu.BuildView();
        Assert.Equal(2, VisibleDescendants(view.Root).Count(element => element.Id?.StartsWith("gf-owner-", StringComparison.Ordinal) == true));
        UiElement foreign = Descendants(view.Root).Single(element => element.Id == "gf-portrait-" + data.GuardianForces[1].Id);
        Assert.True(view.Focus(foreign));
        view = menu.BuildView();
        UiImage owner = Assert.IsType<UiImage>(VisibleDescendants(view.Root).Single(element => element.Id == "gf-owner-" + data.GuardianForces[1].Id));
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, 7), owner.Source);
        UiElement ownerControl = Descendants(view.Root).Single(element => element.Id == "gf-portrait-" + data.GuardianForces[1].Id);
        Assert.True(owner.Bounds.Y >= ownerControl.Bounds.Y - 0.01f && owner.Bounds.Bottom <= ownerControl.Bounds.Bottom + 0.01f);
        UiImage gf = Descendants(view.Root).OfType<UiImage>().Single(image => image.Source?.Kind == UiImageKind.GuardianForcePortrait && image.Source?.Id == data.GuardianForces[1].NativeId);
        Assert.True(owner.Bounds.Bottom <= gf.Bounds.Y + 0.01f);
        UiElement unassigned = Descendants(view.Root).Single(element => element.Id == "gf-portrait-" + data.GuardianForces[2].Id);
        Assert.True(view.Focus(unassigned));
        view = menu.BuildView();
        Assert.Equal(2, VisibleDescendants(view.Root).Count(element => element.Id?.StartsWith("gf-owner-", StringComparison.Ordinal) == true));
    }

    [Fact]
    public void UndiscoveredGuardianForcesDoNotBecomePlaceholderControls()
    {
        MenuData data = Fixture(1, 3);
        MenuController menu = Open(data);
        UiButton[] controls = Descendants(menu.BuildView().Root).OfType<UiButton>().Where(button => button.Id?.StartsWith("gf-portrait-", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(data.GuardianForces.Select(force => "gf-portrait-" + force.Id), controls.Select(control => control.Id));
    }

    private static void AssertNeighbour(UiElement button, int nativeId, UiView view)
    {
        Assert.False(button.Focusable);
        Assert.IsNotType<UiButton>(button);
        UiElement? originalFocus = view.FocusedElement;
        Assert.False(view.Focus(button));
        Assert.Same(originalFocus, view.FocusedElement);
        UiImage image = Assert.Single(Descendants(button).OfType<UiImage>());
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, nativeId), image.Source);
        Assert.True(button.Bounds.X >= view.Root.Bounds.Width / 2);
        Assert.True(button.Bounds.Y < view.Root.Bounds.Y + 90);
    }

    private static MenuController Open(MenuData data, int width = 1280, int height = 720)
    {
        MenuController menu = new(() => data);
        menu.Open("junction-dashboard");
        menu.PrepareViewport(width, height);
        menu.BuildView().Resize(width, height);
        return menu;
    }

    private static MenuData Fixture(int characters, int guardianForces)
    {
        MenuData data = new();
        for (int index = 0; index < characters; index++)
        {
            MenuCharacter character = new() { Id = "character-" + index, NativeId = index == 0 ? 8 : index + 2, Name = "Character " + index, InParty = true, CurrentHp = 100, MaximumHp = 200, Level = 10 };
            character.CommandIds.AddRange(["0", "0", "0"]);
            character.Commands.AddRange(["Attack", "--", "--", "--"]);
            character.AbilityIds.AddRange(["0", "0", "0", "0"]);
            data.Characters.Add(character);
        }
        for (int index = 0; index < guardianForces; index++)
        {
            data.GuardianForces.Add(new() { Id = "gf-" + index, NativeId = index, Name = "Guardian " + index });
        }
        return data;
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<UiElement> VisibleDescendants(UiElement root)
    {
        if (!root.Visible)
        {
            yield break;
        }
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in VisibleDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
