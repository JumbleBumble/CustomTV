# 📺 CustomTV - Play Your Own Videos on the TV

**Ever wanted to hijack the TV and play your own videos? Now you can.**

## 🧩 Features
- ✅ Full MP4 playback support using Unity's built-in Video Player
- ✅ Multi-Video support
- ✅ Manual pause/resume & previous/skip controls via hotkeys
- ✅ Automatically plays next video
- ✅ Shuffles video list on mod load.
- ✅ Play YouTube videos directly from URLs
- ✅ Video caching system for YouTube videos to improve performance
- ✅ Automatic management of temporary files

## ⌨️ Controls
While holding **Shift + Ctrl**:

- **-** or **Numpad Minus** → Pause video
- **+** or **Numpad Plus** → Resume video
- **]** → Skip video
- **[** → Previous video
- **Right Arrow** → Seek Forwards (10 seconds by default)
- **Left Arrow** → Seek Backwards (10 seconds by default)
- **V** → Paste YouTube URL from clipboard

## 📁 How to Install
1. Make sure you have **Melon Loader** installed for *Schedule I*.  
2. Drop the appropriate **CustomTV.dll** into your `Mods` folder:
   - Use **CustomTV.IL2CPP.dll** for the main/`none` and `beta` versions
   - Use **CustomTV.Mono.dll** for the Steam `alternate` and `alternate-beta` versions
3. Create a folder called **TV** inside `Mods` if it doesn't exist.  
4. Place your **mp4** files inside `Mods/TV`.

```
📁 Schedule I
 └── 📁 Mods
      ├── 📄 CustomTV.IL2CPP.dll (or CustomTV.Mono.dll)
      └── 📁 TV
           └── 📄 example.mp4
```

## 🧪 Compatibility
- **Game Versions:** 
  - **IL2CPP** (main version of Schedule I)
  - **Mono** (alternate version available through Steam beta branch)
- **Framework:** Melon Loader

### Build Configurations
The mod has two different build configurations:
- **IL2CPP Build:** Use with the main version of Schedule I
- **Mono Build:** Use with the Mono version available through Steam beta branch

To switch between versions:
1. Right-click Schedule I in your Steam library
2. Select Properties → Betas
3. Select the appropriate branch for your desired version
4. Wait for Steam to update the game
5. Use the corresponding version of CustomTV.dll

## ⚙️ Configuration
You can tweak the keybinds and adjust the audio volume to your liking through the **MelonPreferences.cfg** file:

1. **Where to Find the Config File**  
   The configuration settings are now stored in the default **MelonPreferences.cfg** file in your game's `UserData` directory, not in a separate CustomTVConfig.ini file.

2. **Config Example (in *MelonPreferences.cfg*):**

```
; Valid key names: https://docs.unity3d.com/ScriptReference/KeyCode.html

[CustomTV]
PauseKey = "Minus"
ResumeKey = "Equals"
NextVideoKey = "RightBracket"
PreviousVideoKey = "LeftBracket"
SeekForwardKey = "RightArrow"
SeekBackwardKey = "LeftArrow"
VolumePercent = 100
SeekAmount = 10.0
Shuffle = true
YoutubeURLKey = "V"
MaxCachedYoutubeVideos = 25
DeleteYoutubeVideosOnExit = true
UseFirefoxCookies = false
```

*You still must press both Ctrl and Shift before the set keybind.

**Note:** Valid key names can be found in the [Unity KeyCode Enum Reference](https://docs.unity3d.com/ScriptReference/KeyCode.html)

## 🔀 CustomTV Sorting Guide

If shuffle mode is disabled, CustomTV sorts your video files in a specific order to keep episodes and seasons in a logical sequence. To ensure your videos are sorted correctly, follow these simple naming rules:

**Recommended Naming Format:**

1. **Season and Episode Numbers First**  
Name your video files starting with the season and episode numbers, formatted as *SxxExx*.  
Example: *S01E01 - ShowName.mp4*

2. **Optional Leading Numbers**  
If you have files with a leading number (like special episodes, bonuses, or collections), place the number at the start of the file name.  
Example: *001 - SpecialEpisode.mp4* or *10 - BonusEpisode.mp4*

3. **Natural Sorting**  
After following the above formats, CustomTV will handle sorting your files in a natural order as usually displayed in file explorer (e.g., *S01E01* will come before *S01E10*).

**Important Notes:**
- **Season and episode number (*SxxExx*)** is the most important for sorting.
- Leading numbers are used for episodes or specials outside the main series.
- Files will be ordered by season first, then episode number, then any numbers at the start of the filename.

By following these simple naming conventions, CustomTV will automatically sort your episodes in a way that makes sense for watching!

## ❓ FAQ
**Q: Can I use .webm or other formats?**  
A: No, Unity's VideoPlayer works best with `.mp4` (H.264/AAC).

**Q: The screen is black!**  
A: Make sure your video has the **mp4** extension and uses a supported codec.

**Q: Keybinds aren't working.**  
A: Be sure to press both ctrl and shift before the keybind, for example ctrl-shift-] to skip video.

## 🎥 YouTube Functionality

The mod now supports playing videos directly from YouTube URLs.

### How to use:

1. Copy a YouTube URL to your clipboard
2. Press `Ctrl+Shift+V` in-game to paste and process the URL
3. The video will be downloaded and played on the TV

Note: For YouTube playlists, the mod will download and queue all videos in the playlist. However it must be a playlist link, and not a link to a video in a playlist. For example, `https://www.youtube.com/playlist?list=PLjB_8hSS2lEPSOivtbvDDugFuCeqC4_xm` will work, while `https://www.youtube.com/watch?v=U8F5G5wR1mk&list=PLjB_8hSS2lEPSOivtbvDDugFuCeqC4_xm&index=10` would only play that specific video from the playlist.

### Configuration

You can configure the following YouTube-related settings in the `CustomTVConfig.ini` file:

- `Youtube URL = V` - The key to paste YouTube URL from clipboard (default: V)
- `Max Cached Youtube Videos = 25` - Number of YouTube videos to keep cached (default: 25)
- `Delete Youtube Videos On Exit = True` - Whether to delete cached videos when the game exits (default: True)

## 🎉 Credits

- Created by Jumble
- YouTube & Mono functionality added by ifBars
