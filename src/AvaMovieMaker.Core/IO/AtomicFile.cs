namespace AvaMovieMaker.IO;

public static class AtomicFile
{
    public static void Write(string path, Action<Stream> write)
    {
        IFileStore store = FileStore.Current;
        string tmp = path + ".tmp-" + Environment.ProcessId;
        try
        {
            using (Stream s = store.Create(tmp))
            {
                write(s);
                if (s is FileStream fs)
                {
                    fs.Flush(flushToDisk: true);
                }
                else
                {
                    s.Flush();
                }
            }

            store.Move(tmp, path);
        }
        finally
        {
            if (store.Exists(tmp))
            {
                store.Delete(tmp);
            }
        }
    }
}
