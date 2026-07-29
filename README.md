# GDMENU Organizer

Index your local Dreamcast GD ROM library, organize titles into **Cards**, and write a Card to an SD card for use with GDEMU / GDmenu / openMenu.

GDEMU expects a specific folder layout on the SD card. Getting that wrong slows boots or can prevent them entirely. GDMENU Organizer builds that layout correctly so your console boots quickly.

Forked from [GDMENU Card Manager](https://github.com/sonik-br/GDMENUCardManager) by Sonik.

![Main window](docs/capture1.png)
![Info window](docs/capture2.png)

## How it works

The app is organized into three tabs:

* **Library** — scan a folder of Dreamcast images into a local SQLite index. Search, view info/covers, rename titles, and export the list. Sync status shows which games are present, newly found, or missing from disk.
* **Cards** — named playlists drawn from the library. Create / rename / delete cards, add or remove games, and reorder them. **Write SD Card** opens a drive picker and copies the selected card to the SD card in GDEMU-friendly order.
* **Settings** — library folder, temporary folder, and menu kind (gdMenu or openMenu).

App data lives under `%AppData%/GDMENUOrganizer` (or the equivalent Application Data folder on other platforms):

* `app.db` — library games, cards, and cached PlayStation serial catalog
* `settings.json` — library path, temp folder, and menu preference
* `gamedb.yaml` — cached DuckStation game database used for PlayStation disc metadata

## Features

* Multi platform: Windows / Linux / macOS
* Local SQLite library index with present / new / missing sync status
* Multiple named Cards with ordered game lists
* Write a Card to an SD drive via a dedicated dialog
* Supports both GDmenu and openMenu
* Supports GDI, CDI, MDS and CCD files; also archives (zip / rar / 7z)
* Rename from folder name, file name, or internal name (IP.BIN)
* Show cover image (`0GDTEX.PVR`)
* CodeBreaker image detection when applicable
* Writes `name.txt` per folder for compatibility with other managers
* Menu built as GDI (works on consoles that cannot boot MIL-CD)
* GDI shrinking (removes dummy data to reduce size)

### GDI Shrinking

Shrinking can break some titles. A blacklist of known-problematic games is used by default so those games are left unshrunk.

### openMenu

openMenu can show custom icons, box art, and text per title, but needs extra DAT files.

Place openMenu DAT files under this app's `tools\openMenu\menu_data` folder.  
Get them from mrneo240's repos: [imagedb](https://github.com/mrneo240/openMenu_imagedb) and [metadb](https://github.com/mrneo240/openMenu_metadb).

### Windows: .NET 10 Desktop Runtime

Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0/runtime) for your system.

![Dot Net Runtime](docs/dotnetruntime.png)

### Limitations

* macOS: if the app will not run, see upstream issue [#4](https://github.com/sonik-br/GDMENUCardManager/issues/4) for a workaround

## Typical workflow

1. Set **Library Path** and menu kind (gdMenu / openMenu) in Settings.
2. On the Library tab, click **Refresh** to scan and index your ROMs.
3. On the Cards tab, create a Card and add games from the library.
4. Click **Write SD Card**, choose the SD drive, and confirm.

## Building

### Linux x64 (CLI)

1. Install the .NET 10 SDK ([Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux))
2. Clone this repository and `cd` into `src`
3. Publish:

```bash
# Framework-dependent
dotnet publish GDMENUOrganizer.AvaloniaUI/GDMENUOrganizer.AvaloniaUI.csproj -c Release

# Single-file, self-contained (bundles the runtime)
dotnet publish GDMENUOrganizer.AvaloniaUI/GDMENUOrganizer.AvaloniaUI.csproj -c Release \
  --self-contained true -r linux-x64 \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

4. Run the binary from the publish output, for example:

```bash
./GDMENUOrganizer.AvaloniaUI/bin/Release/net10.0/linux-x64/publish/GDMENUOrganizer
```

### macOS

1. Install [.NET Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0/runtime) and the SDK (`brew install dotnet`)
2. Build and run:

```bash
cd src/
dotnet publish GDMENUOrganizer.AvaloniaUI/GDMENUOrganizer.AvaloniaUI.csproj -c Release
cd GDMENUOrganizer.AvaloniaUI/bin/Release/net10.0/publish/
./GDMENUOrganizer
```

### Windows (SDK)

```bash
cd src/
dotnet run --project GDMENUOrganizer.AvaloniaUI/GDMENUOrganizer.AvaloniaUI.csproj
```

## Credits

Based on **GDMENU Card Manager** by Sonik.

This software also relies on third-party tools:

GDmenu by neuroacid  
[openMenu](https://github.com/mrneo240/openmenu/),
[GdiTools](https://sourceforge.net/projects/dcisotools/),
[GdiBuilder](https://github.com/Sappharad/GDIbuilder/),
[Aaru](https://github.com/aaru-dps/Aaru/),
[PuyoTools](https://github.com/nickworonekin/puyotools/),
[7-zip](https://www.7-zip.org/),
[SevenZipSharp](https://github.com/squid-box/SevenZipSharp/)

Special thanks to megavolt85 and everyone in the Dreamcast scene.
