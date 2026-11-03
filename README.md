# <img src="NbtStudio/Resources/nbt_studio_icon_256.png" width=48> NBT Studio

### [Download link](https://github.com/tryashtar/nbt-studio/releases)

NBT Studio is an [NBT](https://wiki.vg/NBT) editing application, the spiritual successor to [NBTExplorer](https://github.com/jaquadro/NBTExplorer). It has been rewritten completely from scratch to support a vast array of new features, while keeping the familiar layout of controls. New features include Bedrock support, SNBT support, undo/redo functionality, drag and drop, multiselect, and more! It's called Studio to make it sound more important than it really is.

## <img src="NbtStudio/Resources/amber/action_open_file.png" width=16> Files
NBT Studio supports reading and writing the following NBT formats. Features marked with a star are new NBT Studio features not present in NBTExplorer.

* Java NBT files, such as `level.dat`
* Java region files (`.mca` and `.mcr`)
* ☆ Amulet [construction](https://github.com/Amulet-Team/construction-specification/blob/master/specifications/version_0/readme.md) files (`.construction`, spec v0)
* ☆ Bedrock NBT files, such as `.mcstructure` files (little-endian NBT)
* ☆ SNBT files (stringified NBT, like in commands: `{Enchantments:[{id:sharpness,lvl:10s}]}`)

<img src="NbtStudio/Resources/amber/action_save.png" width=16> ☆ NBT files can be exported to any of these formats using <kbd>Save as</kbd>. You can load an SNBT file, then export it as a little-endian g-zipped binary file if you wish.

<img src="NbtStudio/Resources/amber/action_new_file.png" width=16> ☆ NBT Studio also allows the creation of blank NBT files. You can start from scratch and export to any format. Additionally, you can use <kbd>Ctrl</kbd> <kbd>Alt</kbd> <kbd>V</kbd> to create a file from SNBT data on your clipboard.

## <img src="NbtStudio/Resources/amber/action_edit_snbt.png" width=16> SNBT Support
NBT Studio is designed around the easy transfer and conversion between textual SNBT data and structured NBT data. As mentioned, SNBT can be opened as a file or pasted as a new document.

* <img src="NbtStudio/Resources/amber/action_add_snbt.png" width=16> You can add a tag as SNBT anywhere in the document. Whatever type is parsed will be the resulting type of the tag. Compounds and lists will be fully parsed with all of their children.
* <img src="NbtStudio/Resources/amber/action_edit_snbt.png" width=16> Existing tags can also be inspected and edited as SNBT. You can modify entire compounds in-place, or simpler tags.
* <img src="NbtStudio/Resources/amber/action_paste.png" width=16> The cut, copy, and paste actions add the tag to your clipboard as SNBT. You can copy multiple tags as text, or import SNBT data obtained elsewhere.

## <img src="NbtStudio/Resources/amber/action_edit.png" width=16> Tag Editing
NBT Studio adds an assortment of convenience features to make navigating and editing NBT data easier.

* <img src="NbtStudio/Resources/amber/tag_byte.png" width=16> The tag creation and edit menus have fields for both tag name and value, so you don't have to do it in two steps
* <img src="NbtStudio/Resources/amber/tag_int.png" width=16> Hold <kbd>Shift</kbd> while adding a tag to skip the menu and select its name automatically
* <img src="NbtStudio/Resources/amber/tag_list.png" width=16> NBT lists of type `byte`, `short`, `int`, or `long` can be edited as hex just like arrays
* <img src="NbtStudio/Resources/amber/action_cut.png" width=16> Tags can be selected, dragged, and dropped to move them to a different parent
* <img src="NbtStudio/Resources/amber/action_undo.png" width=16> Undo and redo functionality with <kbd>Ctrl</kbd> <kbd>Z</kbd> and <kbd>Ctrl</kbd> <kbd>Shift</kbd> <kbd>Z</kbd>
* <img src="NbtStudio/Resources/amber/action_delete.png" width=16> Deleting tags automatically selects the next tag for easy obliteration
* <img src="NbtStudio/Resources/amber/action_search.png" width=16> New and improved search window allows searching by [regular expression](https://en.wikipedia.org/wiki/Regular_expression), as well as an option to select all matching tags
* <img src="NbtStudio/Resources/amber/action_edit.png" width=16> Press <kbd>Enter</kbd> to edit the selected tag
* <img src="NbtStudio/Resources/amber/action_sort.png" width=16> Press <kbd>Space</kbd> to expand/contract the selected tag, or <kbd>Ctrl</kbd> <kbd>Space</kbd> to expand all
* <img src="NbtStudio/Resources/amber/action_save.png" width=16> Right-click on a file to see options to save it, or open it in File Explorer
* <img src="NbtStudio/Resources/amber/tag_compound.png" width=16> Right-click on a container tag to see options to add a child tag
* <img src="NbtStudio/Resources/amber/file_file.png" width=16> Files display an asterisk (`*`) to indicate there are unsaved changes

## Building and publishing

The solution requires the .NET 6 SDK or newer and the Windows Desktop / Windows Forms components. The repository uses git submodules, so clone it recursively or initialize them manually:

```text
git clone --recursive https://github.com/Max-RM/nbt-studio.git
cd nbt-studio
git submodule update --init --recursive
dotnet build NbtStudio.sln -c Release
```

The repository includes PowerShell scripts for publishing:

```text
scripts\Publish-All.ps1
scripts\Publish-All.ps1 -Rids win-x64
scripts\Publish-All.ps1 -SelfContained
scripts\Package-All.ps1
```

Framework-dependent builds are written to `Publish\NbtStudio_<rid>`. Self-contained builds are written to `Publish\NbtStudio_SelfContained_<rid>`. Supported targets are `win-x64`, `win-x86`, and `win-arm64`. `Package-All.ps1` creates `Archives\NbtStudio-<rid>.zip` and `Archives\NbtStudio-SelfContained-<rid>.zip` for each published target; pass `-SkipSelfContained` to package only the framework-dependent builds. `scripts\Clean-BuildOutput.ps1` removes `bin`, `obj`, and `Publish` (`-IncludeArchives` also removes `Archives`).

Double-clickable `.cmd` launchers are provided next to the scripts, and per-architecture wrappers such as `scripts\Publish-win-arm64.cmd` publish a single target.

# Credits
This application was written from scratch by myself, tryashtar. However, it would never have existed without these amazing projects that came before me:

### Design
* [NBTExplorer by jaquadro](https://github.com/jaquadro/NBTExplorer)

### Technologies
* [fNbt by mstefarov](https://github.com/mstefarov/fNbt)
* [TreeViewAdv by agaman](https://sourceforge.net/projects/treeviewadv)
* [Be.HexEditor by bernhardelbl](https://sourceforge.net/projects/hexbox)

### Icons
* [Yusuke Kamiyamane](https://p.yusukekamiyamane.com)
* [AmberW](https://github.com/AmberWat)
