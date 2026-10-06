namespace AvaMovieMaker.Audio;

public sealed record AudioDeviceInfo(int Index, string Name, bool IsDefault, bool IsCapture);
