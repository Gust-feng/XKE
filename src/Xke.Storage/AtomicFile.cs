using System.Text;

namespace Xke.Storage;

internal static class AtomicFile
{
    public static void WriteText(string path, string content)
        => Write(path, temporary => File.WriteAllText(temporary, content, new UTF8Encoding(false)));

    public static void Write(string path, Action<string> write)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            write(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
