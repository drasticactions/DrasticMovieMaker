# Drastic Movie Maker

Drastic Movie Maker is an experimental video editor, written in .NET with Avalonia. It's modeled after the Windows Movie Maker for Windows Vista and Windows 7.

![macOS UI Screenshot](assets/screenshot.png)

There are tons of video editing software out there, but they are mostly aimed at "content creators" with tons of features I don't need. Windows Movie Maker let you drag clips into a timeline and make a video, which is all I need. It was also a good way for me to try writing Skia filters and fix issues in [AvaWpf](https://github.com/drasticactions/AvaWpf).

While I tried to match the features within the later versions of Windows Movie Maker, it's _not_ a "Clean-Room Reimplmentation." It's mostly me eye-balling it and trying to match what the UI and features do. I did try to compare it with running similar clips in Windows Movie Maker to guess what it does, but don't assume it will match it. I'm not strictly trying to.

# macOS Gatekeeper

The current CI builds are not signed. To use them on macOS, you'll need to remove the quarantine on them.

- Extract the app from the DMG, either to `/Applications` or another location.
- Run `xattr -rd com.apple.quarantine /Applications/DrasticMovieMaker.app`, change the path with the location of the app.

It should then run.

# LLM Usage

I did use LLMs while working on this project.

- Tests and debugging were pushed through via Claude. Some of this was testing against Windows Vista and Windows 7 installs for the Windows Movie Maker project importer code I wrote, which could be harnessed to make it easier to compare my shaders and effects against the originals by running encodes against them.
- I wrote the majority of the backend and frontend code as I went. Some of it was edited or added by Claude while debugging through it, which I either accepted and didn't change, accepted and did change, or ignored and rewrote.
- Some of the icons were generated via Claude. I've replaced most of them either with CC0 versions or my own.
- The GitHub Actions and ffmpeg build scripts were generated via Claude.

# Icons

Several of the toolbar icons, menu and dialog icons and the app icon are from the Windows XP High Resolution Icon Pack by marchmountain,
release 02 (https://github.com/marchmountain/-Windows-XP-High-Resolution-Icon-Pack/releases/tag/02).
The pack is under CC0 1.0 (`LICENSE-CC0.txt`).

The playback icons and timeline icons are rough interpretations by me based on Windows Vista and 7 designs.

The transition and effects icons are LLM generated via Claude.

