using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Memoria.FF8.Mods;

namespace Memoria.Launcher;

internal sealed class LauncherModPreviewTree : TreeView
{
    protected override Type StyleKeyOverride => typeof(TreeView);

    public LauncherModPreviewTree()
    {
        AutoScrollToSelectedItem = true;
        ItemTemplate = new FuncTreeDataTemplate<ModPreviewTreeNode>((branch, _) => new TextBlock { Text = branch.Label, TextTrimming = TextTrimming.CharacterEllipsis }, branch => branch.Children);
    }

    public void Load(ModPreviewTree model)
    {
        ItemsSource = model.Branches;
        IReadOnlyList<ModPreviewTreeNode> path = model.InitialPath;
        if (path.Count == 0)
        {
            return;
        }

        ItemsControl parent = this;
        int index = 0;
        LayoutUpdated += RevealSelection;

        void RevealSelection(object? sender, EventArgs args)
        {
            if (SelectedItem is not null)
            {
                LayoutUpdated -= RevealSelection;
                return;
            }
            while (parent.ContainerFromItem(path[index]) is TreeViewItem container)
            {
                if (++index == path.Count)
                {
                    LayoutUpdated -= RevealSelection;
                    SelectedItem = path[^1];
                    container.BringIntoView();
                    return;
                }
                parent = container;
                container.IsExpanded = true;
            }
        }
    }
}
