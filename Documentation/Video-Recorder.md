# Built-in Windows recorder

The editor and Windows player can record MP4 clips with H.264 video and AAC audio. No external encoder executable is required.

| Control | Action |
| --- | --- |
| F9 | Start or stop recording |
| Shift+F9 | Open recorder settings |
| Editor **Recorder** menu | Start/stop and open settings |

In the editor, the recorder captures **Game View**, including the game's UI. Selecting Start opens/focuses Game View. Hiding or closing that viewport stops the recording. The player records the rendered game window, including its HUD. The player's recorder notification is drawn after capture and stays out of the video.

The output folder defaults to the current Windows user's **Videos** known folder, including a redirected Videos folder. Choose another folder in recorder settings or use **Windows Videos** to restore the default. The selection is saved locally under `%LOCALAPPDATA%/ByteEngine/Recorder/settings.json`; it is not embedded into exported games or scenes. Another player defaults to their own Videos folder.

Playback audio is enabled by default. It records the default Windows playback device selected when recording starts, including sounds from other applications. It does not capture the microphone. Disable **Record playback audio** for a silent video. Changing folders, quality or audio mode is disabled until the recording has stopped and finished saving.

Choose **Video quality** in recorder settings (Shift+F9) before starting:

| Preset | Maximum resolution | Target video bitrate |
| --- | --- | --- |
| Low | 1280×720 | 4 Mbps |
| Standard | 1920×1080 | 12 Mbps |
| High (default) | 1920×1080 | 25 Mbps |
| Ultra | 1920×1080 | 50 Mbps |

Higher presets retain more detail and use more disk space. Existing preference files without a quality setting default to High. Quality is saved for both the editor and Windows player and fixed when each recording starts. Resolution never upscales a smaller source. In the editor, the recording renders at the project's output resolution capped by the preset, independently of the Game View panel's on-screen size; the preview fits that aspect ratio. The normal 720p preview cap applies again when recording stops. The Windows player uses its rendered window resolution capped by the preset.

Clips use 30 fps capture scheduling, at the initial recording resolution up to the chosen preset limit, with even pixel dimensions. Encoding and file writes run on a worker thread with a bounded frame queue. Frames are skipped if encoding cannot keep up; timestamps retain elapsed time. Resizing the game during recording keeps the original video dimensions and fits the new view with black bars as needed.

Press F9 to stop and wait for **Video saved** before opening the file. Normal application shutdown also drains/finalizes the recording. Keep the application open while it says **Saving**. An OS crash or forced process termination can leave an unfinished MP4.

New Windows exports inherit the recorder from the player runtime. Existing exported executables need to be rebuilt to obtain it. Updating the editor/player runtime does not itself export any game. Browser recording is outside this feature.

Recording errors appear in Game View/the recorder panel or the Windows player's notification. Check folder permissions, available disk space and the playback device. Windows N installations may need Microsoft's Media Feature Pack for H.264/AAC encoding.

Implementation references: Microsoft's [Media Foundation encoding tutorial](https://learn.microsoft.com/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video) and [WASAPI loopback recording](https://learn.microsoft.com/windows/win32/coreaudio/loopback-recording). Native encoding, MP4 decoding, captured audio, timestamps, resize behavior, error handling and settings are exercised by `--video-recorder` in the console test runner.
