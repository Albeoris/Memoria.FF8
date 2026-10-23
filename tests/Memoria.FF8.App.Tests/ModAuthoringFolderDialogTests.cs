namespace Memoria.FF8.App.Tests;

public sealed class ModAuthoringFolderDialogTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8-authoring-folder-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void NativeShellAcceptsTheDialogEventContractWithoutOpeningAWindow()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            object? dialog = null;
            uint cookie = 0;
            Type shell = typeof(ModAuthoringFolderDialog);
            Type contract = shell.GetNestedType("IFileDialog", System.Reflection.BindingFlags.NonPublic)!;
            try
            {
                dialog = Activator.CreateInstance(shell.GetNestedType("FileOpenDialog", System.Reflection.BindingFlags.NonPublic)!);
                object callback = Activator.CreateInstance(shell.GetNestedType("FolderEvents", System.Reflection.BindingFlags.NonPublic)!, [temporary])!;
                object?[] arguments = [callback, cookie];
                contract.GetMethod("Advise")!.Invoke(dialog, arguments);
                cookie = (uint)arguments[1]!;
                Assert.NotEqual(0u, cookie);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (cookie != 0)
                {
                    contract.GetMethod("Unadvise")!.Invoke(dialog, [cookie]);
                }
                if (dialog is not null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(dialog);
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Shell COM registration did not return.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void PackageAcceptanceRequiresOneDirectChildAndRejectsEscapingPaths()
    {
        string root = Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Memoria")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(root, "package")).FullName;
        Assert.True(ModAuthoringFolderDialog.IsDirectPackage(root, child));
        Assert.True(ModAuthoringFolderDialog.IsDirectPackage(root + Path.DirectorySeparatorChar, child + Path.DirectorySeparatorChar));
        Assert.True(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(root, "new-package")));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, root));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, root + Path.DirectorySeparatorChar));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, root + "-sibling"));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(root + "-sibling", "package")));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(child, "nested")));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(root, "..", "outside")));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(child, "..")));
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, Path.Combine(child, "..", "..", "outside")));
    }

    [Fact]
    public void PackageAcceptanceRejectsExistingFiles()
    {
        string root = Directory.CreateDirectory(Path.Combine(temporary, "Memoria")).FullName;
        string file = Path.Combine(root, "ordinary-file");
        File.WriteAllText(file, "synthetic");
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, file));
    }

    [Fact]
    public void PackageAcceptanceRejectsLinksAtDestinationRootAndAboveRoot()
    {
        string root = Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Memoria")).FullName;
        string outside = Directory.CreateDirectory(Path.Combine(temporary, "outside")).FullName;
        string destination = Path.Combine(root, "linked-package");
        CreateLink(destination, outside);
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, destination));
        string rootLink = Path.Combine(temporary, "linked-root");
        CreateLink(rootLink, root);
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(rootLink, Path.Combine(rootLink, "package")));
        string parentLink = Path.Combine(temporary, "linked-parent");
        CreateLink(parentLink, Path.Combine(temporary, "Mods"));
        string inheritedRoot = Path.Combine(parentLink, "Memoria");
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(inheritedRoot, Path.Combine(inheritedRoot, "package")));
    }

    [Fact]
    public void PackageAcceptanceRejectsOrphanLinksAndDeletedTargets()
    {
        string root = Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Memoria")).FullName;
        string missing = Path.Combine(temporary, "missing-target");
        string orphan = Path.Combine(root, "orphan-package");
        CreateLink(orphan, missing);
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, orphan));
        string rootLink = Path.Combine(temporary, "orphan-root");
        CreateLink(rootLink, missing);
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(rootLink, Path.Combine(rootLink, "package")));
        string outside = Directory.CreateDirectory(Path.Combine(temporary, "removed-target")).FullName;
        string removed = Path.Combine(root, "removed-package");
        CreateLink(removed, outside);
        Directory.Delete(outside);
        Assert.False(ModAuthoringFolderDialog.IsDirectPackage(root, removed));
    }

    private static void CreateLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException || exception is IOException && (exception.HResult & 0xFFFF) == 1314)
        {
            Assert.Skip("Creating directory symbolic links is unavailable in this test environment.");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(temporary))
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
