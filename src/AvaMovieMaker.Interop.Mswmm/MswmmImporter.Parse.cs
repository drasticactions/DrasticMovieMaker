using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Interop.Mswmm;

public static partial class MswmmImporter
{
    internal static Project Parse(byte[] data, List<string> warnings)
    {
        if (!CompoundFile.IsCompoundFile(data))
        {
            throw new InvalidDataException(Strings.NotAMovieMakerProject);
        }

        try
        {
            var cfb = new CompoundFile(data);
            return MswmmReader.Read(cfb, warnings);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException(Strings.ProjectFileDamaged, ex);
        }
    }
}
