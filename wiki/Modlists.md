# Modlists

Modlists let you export and import mod configurations — perfect for sharing with friends, server setups, or backing up your mod collection.

---

## 📤 Exporting a Modlist

Export your current mod setup to a file that others can import.

### How to Export

1. Make sure you have the correct **game instance** selected
2. Click **File** → **Export Modlist** (or the export button)
3. Choose a location and filename for your modlist
4. Click **Save**

### What Gets Exported

The modlist file (`.json`) contains:

| Data | Description |
|------|-------------|
| Mod names | Name of each installed mod |
| Versions | Version numbers (if available) |
| Download URLs | Original download sources |
| File sizes | Size of each mod |
| Checksums | For verifying file integrity |

### Example Modlist File

```json
{
  "name": "My Farm Setup",
  "gameVersion": "FS25",
  "createdDate": "2025-12-25T12:00:00Z",
  "mods": [
    {
      "name": "John Deere 8R Series",
      "version": "1.2.0",
      "downloadUrl": "https://...",
      "sizeBytes": 52428800
    }
  ]
}
```

---

## 📥 Importing a Modlist

Install mods from a shared modlist file.

### How to Import

1. Click **File** → **Import Modlist** (or the import button)
2. Select the modlist file (`.json`)
3. Review the mod list and analysis:
   - ✅ **Up-to-date** - Already installed, no action needed
   - 📥 **To Download** - New mods that will be installed
   - 🔄 **To Replace** - Mods with version differences
4. Click **Install** to begin

### Import Analysis

Before installing, the app analyzes your current mods folder:

| Status | Meaning | Action |
|--------|---------|--------|
| ✅ Up-to-date | Mod already installed with matching version | Skipped |
| 📥 To Download | Mod not installed | Will be downloaded |
| 🔄 To Replace | Different version installed | Will be updated |
| ⚠️ Not Found | Download URL unavailable | Manual install required |

### Bulk Installation

The import process:
1. Downloads all required mods
2. Shows progress for each mod
3. Extracts and installs to your mods folder
4. Reports success/failure summary

---

## 🎮 Use Cases

### Multiplayer Server Setup

1. Server admin exports their modlist
2. Shares the file with players (Discord, email, etc.)
3. Players import the modlist
4. Everyone has matching mods!

### Backup Your Setup

1. Export your modlist before reinstalling your game
2. After reinstall, import the modlist to restore your mods

### Sharing Mod Packs

Create themed mod packs:
- "Realistic American Farming" modlist
- "European Agriculture" modlist
- "Forestry Equipment" modlist

---

## 📁 Modlist File Format

Modlists use a JSON format (`.json` extension):

```json
{
  "manifestVersion": "1.0",
  "name": "Modlist Name",
  "description": "Optional description",
  "author": "Your Name",
  "gameVersion": "FS25",
  "createdDate": "2025-12-25T12:00:00Z",
  "mods": [
    {
      "id": "unique-mod-id",
      "name": "Mod Display Name",
      "version": "1.0.0",
      "author": "Mod Author",
      "downloadUrl": "https://example.com/mod.zip",
      "sizeBytes": 10485760,
      "checksum": "sha256:abc123..."
    }
  ]
}
```

### Required Fields

- `gameVersion` - Target game (FS15, FS17, FS19, FS22, FS25)
- `mods` - Array of mod entries
- `mods[].name` - Mod name
- `mods[].downloadUrl` - Where to download the mod

### Optional Fields

- `name` - Friendly name for the modlist
- `description` - Description of the mod pack
- `author` - Who created the modlist
- `mods[].version` - Mod version
- `mods[].checksum` - File hash for verification

---

## ⚠️ Important Notes

### Download Availability

- Mod download URLs may become unavailable over time
- Some mods are removed from hosting sites
- If a download fails, you may need to find the mod manually

### Version Compatibility

- Always use modlists for the **same game version**
- An FS25 modlist won't work for FS22

### Large Modlists

- Importing many mods takes time
- Ensure you have enough disk space
- A stable internet connection is recommended

---

## ⏭️ Next Steps

- Customize download behavior in [Settings](Settings)
- Having issues? Check [Troubleshooting](Troubleshooting)
