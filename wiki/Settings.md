# Settings

Configure FS Mod Downloader to work the way you want.

---

## ⚙️ Accessing Settings

Click the **⚙️ Settings** button in the top-right corner of the main window.

---

## 🎮 Game Instances

Manage your Farming Simulator installations.

### Automatic Detection

Click **🔍 Scan for Games** to automatically find your FS installations.

The app searches:
| Location | Description |
|----------|-------------|
| Documents | `My Games\FarmingSimulatorXXXX\mods` |
| Steam | Via registry and `libraryfolders.vdf` |
| GIANTS | Registry entries from GIANTS Software |

### Manual Configuration

If auto-detection misses your installation:

1. Click **➕ Add Manual Entry**
2. Select the game version (FS15-FS25)
3. Enter a friendly name (e.g., "FS25 - Steam")
4. Browse to your mods folder
5. Click **Add**

### Editing Game Instances

- **Edit**: Change the name or path of an existing instance
- **Remove**: Delete an instance from the list (doesn't affect actual game files)
- **Set Default**: Choose which game loads on startup

### Mods Folder Location

Default mods folder locations:

| Game | Default Path |
|------|--------------|
| FS25 | `Documents\My Games\FarmingSimulator2025\mods` |
| FS22 | `Documents\My Games\FarmingSimulator2022\mods` |
| FS19 | `Documents\My Games\FarmingSimulator2019\mods` |
| FS17 | `Documents\My Games\FarmingSimulator2017\mods` |
| FS15 | `Documents\My Games\FarmingSimulator2015\mods` |

---

## 📂 Download Settings

### Download Path

Where mod files are temporarily downloaded before installation.

- **Default**: `%USERPROFILE%\Downloads\FSMods`
- Click **Browse** to choose a different location

> **Note:** Downloaded files are automatically cleaned up after successful installation.

### Auto-Install After Download

When enabled (default), mods are automatically installed after downloading. Disable to review downloads before installing.

### Delete After Install

When enabled (default), temporary download files are deleted after successful installation to save disk space.

---

## 🌐 Network Settings

### Request Timeout

How long to wait for mod websites to respond.

- **Default**: 30 seconds
- Increase if you have a slow connection
- Decrease if you want faster failure detection

### Max Concurrent Downloads

Number of mods that can download simultaneously.

- **Default**: 3
- Increase for faster bulk downloads (uses more bandwidth)
- Decrease if experiencing network issues

---

## 🔔 Notification Settings

### Auto Check for Updates

Automatically check for new versions of FS Mod Downloader on startup.

### Notify on Mod Updates

Show notifications when installed mods have updates available.

---

## 🖥️ Application Settings

### Start Minimized

Launch the app minimized to the system tray.

### Minimize to Tray

When closing the window, minimize to tray instead of exiting.

### Confirm on Exit

Show a confirmation dialog before closing the application.

---

## 💾 Settings Storage

Settings are saved to:
```
%AppData%\FSModDownloader\settings.json
```

### Backing Up Settings

To backup your settings:
1. Navigate to `%AppData%\FSModDownloader\`
2. Copy `settings.json` to a safe location

### Resetting Settings

To reset to defaults:
1. Close FS Mod Downloader
2. Delete `%AppData%\FSModDownloader\settings.json`
3. Relaunch the app

---

## 📋 Settings Reference

| Setting | Default | Description |
|---------|---------|-------------|
| Repository URL | mod-network.com | Primary mod source |
| Download Path | Downloads\FSMods | Temp download location |
| Auto Check Updates | ✅ Enabled | Check for app updates |
| Notify Mod Updates | ✅ Enabled | Alert for mod updates |
| Auto Install | ✅ Enabled | Install after download |
| Delete After Install | ✅ Enabled | Clean up temp files |
| Start Minimized | ❌ Disabled | Launch to tray |
| Minimize to Tray | ❌ Disabled | Tray on close |
| Confirm Exit | ✅ Enabled | Exit confirmation |
| Request Timeout | 30 seconds | Network timeout |
| Max Downloads | 3 | Parallel downloads |

---

## ⏭️ Next Steps

- Learn about [Managing Mods](Managing-Mods)
- Having issues? Check [Troubleshooting](Troubleshooting)
