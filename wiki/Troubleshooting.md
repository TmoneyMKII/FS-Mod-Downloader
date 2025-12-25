# Troubleshooting

Solutions to common issues with FS Mod Downloader.

---

## 🚀 Startup Issues

### App Won't Start

**Symptoms:** Double-clicking the exe does nothing, or it crashes immediately.

**Solutions:**

1. **Install .NET 8.0 Runtime** (if using the smaller download)
   - Download from [Microsoft .NET](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Or use the standalone package which includes the runtime

2. **Run as Administrator**
   - Right-click `FSModDownloader.exe`
   - Select **Run as administrator**

3. **Check Windows Defender/Antivirus**
   - The app may be quarantined as it's not code-signed
   - Add an exception for the app folder

4. **Check the log file**
   - Navigate to `%AppData%\FSModDownloader\logs\`
   - Open the latest log file for error details

### Windows SmartScreen Warning

**Message:** "Windows protected your PC"

**Solution:**
1. Click **More info**
2. Click **Run anyway**

This appears because the app isn't code-signed (yet).

---

## 🎮 Game Detection Issues

### "No game instances configured"

**Solutions:**

1. **Run auto-detection**
   - Open Settings → Click **Scan for Games**

2. **Add manually**
   - Settings → **Add Manual Entry**
   - Browse to your game's mods folder

3. **Check mods folder exists**
   - The folder might not exist until you run the game once
   - Launch your game, then try detection again

### Game Not Found by Auto-Detect

**Common causes:**

| Cause | Solution |
|-------|----------|
| Non-standard install location | Add manually |
| Mods folder doesn't exist | Launch the game once first |
| Portable/external drive | Add manually with full path |
| Different user account | Mods are in that user's Documents |

### Wrong Mods Folder Detected

If mods install to the wrong location:

1. Open **Settings**
2. Find the incorrect game instance
3. Click **Edit** or **Remove**
4. Add the correct path manually

---

## 📥 Download Issues

### Downloads Fail / Timeout

**Solutions:**

1. **Check internet connection**
   - Try opening mod-network.com in your browser

2. **Increase timeout**
   - Settings → Request Timeout → Set to 60 seconds

3. **Try again later**
   - Mod websites may be temporarily down

4. **Check firewall**
   - Ensure the app can access the internet

### "Download failed with status code 403/404"

**Cause:** The mod has been removed from the hosting site.

**Solution:** Search for the mod on another website manually.

### Downloads Stuck at 0%

**Solutions:**

1. Check if the download URL is still valid
2. The mod site may be blocking automated downloads
3. Try a different mod to verify your connection works

---

## 📦 Installation Issues

### Mods Not Appearing in Game

**Solutions:**

1. **Verify the mods folder path**
   - Check Settings to confirm the path is correct
   - The path should end with `\mods`

2. **Check mod format**
   - Mods should be `.zip` files in the mods folder
   - Some mods need to stay zipped, others need extraction

3. **Restart the game**
   - Mods are loaded on game startup

4. **Check mod compatibility**
   - Ensure the mod is for your game version

### "Access Denied" During Install

**Cause:** The app doesn't have permission to write to the mods folder.

**Solutions:**

1. **Run as Administrator**
2. **Check folder permissions**
   - Right-click mods folder → Properties → Security
   - Ensure your user has write permission

3. **Disable folder protection**
   - Some antivirus software protects Documents folder

### Mod Extraction Failed

**Cause:** The downloaded file may be corrupt or in an unsupported format.

**Solutions:**

1. **Try downloading again**
2. **Extract manually**
   - Find the downloaded file in your temp folder
   - Extract using 7-Zip or WinRAR
   - Copy to your mods folder

---

## 🔄 Update Issues

### App Update Failed

**Solution:** Download the latest version manually from [GitHub Releases](https://github.com/TmoneyMKII/FS-Mod-Downloader/releases).

### Mod Updates Not Showing

**Possible causes:**
- The mod source doesn't provide version information
- Cache needs to refresh (wait 10 minutes or restart app)

---

## 💾 Data & Settings Issues

### Settings Not Saving

**Solutions:**

1. **Check write permissions**
   - Ensure you can write to `%AppData%\FSModDownloader\`

2. **Run as Administrator**

3. **Reset settings**
   - Delete `%AppData%\FSModDownloader\settings.json`
   - Restart the app

### Lost My Game Instances After Update

**Cause:** Settings file may have been reset.

**Solution:** Re-run **Scan for Games** or add instances manually.

---

## 📋 Log Files

Log files help diagnose issues:

**Location:** `%AppData%\FSModDownloader\logs\`

**What to look for:**
- `ERROR` entries indicate problems
- Timestamps help identify when issues occurred
- Stack traces show where code failed

**Sharing logs for support:**
1. Open the logs folder
2. Find the log file from when the issue occurred
3. Attach to your GitHub issue or support request

---

## 🔧 Advanced Troubleshooting

### Clear All Data

To completely reset the application:

1. Close FS Mod Downloader
2. Delete the folder: `%AppData%\FSModDownloader\`
3. Restart the app

### Check Network Connectivity

Test if mod sites are accessible:

1. Open a web browser
2. Try visiting:
   - https://mod-network.com
   - https://farmingsimulator25mods.com

### Verify .NET Installation

Open PowerShell and run:
```powershell
dotnet --list-runtimes
```

Look for `Microsoft.WindowsDesktop.App 8.x.x`

---

## ❓ Still Having Issues?

If none of these solutions work:

1. **Search existing issues:** [GitHub Issues](https://github.com/TmoneyMKII/FS-Mod-Downloader/issues)
2. **Open a new issue** with:
   - Description of the problem
   - Steps to reproduce
   - Your Windows version
   - Log file contents
3. **Ask in Discussions:** [GitHub Discussions](https://github.com/TmoneyMKII/FS-Mod-Downloader/discussions)
