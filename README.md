# Drastic Movie Maker

Drastic Movie Maker is an experimental video editor, written in .NET with Avalonia. It's modeled after the Windows Movie Maker for Windows Vista and Windows 7.

![macOS UI Screenshot](assets/screenshot.png)

There are tons of video editing software out there, but they are mostly aimed at "content creators" with tons of features I don't need. Windows Movie Maker let you drag clips into a timeline and make a video, which is all I need. It was also a good way for me to try writing Skia filters and fix issues in [AvaWpf](https://github.com/drasticactions/AvaWpf). Basically, I wanted a video editor I could use.

Drastic Movie Maker supports:

- Storyboard and timeline editing
- Drag and drop effects and transitions
- Various aspect ratios, including support for newer "Social Media" ratios
- Saving the video
- Windows Movie Maker-style "AutoMovie" generation, where you add clips and some preset styles to create a movie.

The application and in-app icons are from [RemixIcon](https://remixicon.com).

## License

This project is released under the GPL-3.0-or-later license, see [LICENSE.md](LICENSE.md).

This project also includes various third-party libraries and dependencies, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Implementation

While I tried to match the features within the later versions of Windows Movie Maker, it's _not_ a "Clean-Room Reimplmentation." It's mostly me eye-balling it and trying to match what the UI and features do. For AutoMovie, I used their descriptions and tried to create a similar algorithm to what their descriptions and generated movies were. 

I did try to compare it with running similar clips in Windows Movie Maker to guess what it does, but don't assume it will match it. I'm not strictly trying to match it one-to-one.

# macOS Gatekeeper

The current CI builds are not signed. To use them on macOS, you'll need to remove the quarantine on them.

- Extract the app from the DMG, either to `/Applications` or another location.
- Run `xattr -rd com.apple.quarantine /Applications/Drastic\ Movie\ Maker.app`, change the path with the location of the app.

It should then run.

# LLM Usage

I did use LLMs while working on this project.

- Tests and debugging were pushed through via Claude. Some of this was testing against Windows Vista and Windows 7 installs for the Windows Movie Maker project importer code I wrote, which could be harnessed to make it easier to compare my shaders and effects against the originals by running encodes against them.
- I wrote the majority of the backend and frontend code as I went. Some of it was edited or added by Claude while debugging through it, which I either accepted and didn't change, accepted and did change, or ignored and rewrote.
- The GitHub Actions and ffmpeg build scripts were generated via Claude.

The purpose of this project was because I wanted to make it and write code, and offload things like scripts that I was okay having generated.

## Support

Issues and PRs are welcome, but note that this is a hobby project for me so new updates will be mostly limited. If you wish to file an issue, please don't use an LLM to do it. Likewise, any PR using LLMs will be rejected. If I want an LLM to write the code, I could do it myself. And also, this is ment to be fun.

## Build

To build the project, you need to build ffmpeg for the given platform using the provided scripts in [tools/ffmpeg](tools/ffmpeg/), or your own system build.

Otherwise, you should be able to:

- Install .NET 10
- Run the package scripts in [tools/](tools/)

Or run `dotnet run` in [src/AvaMovieMaker/](src/AvaMovieMaker)