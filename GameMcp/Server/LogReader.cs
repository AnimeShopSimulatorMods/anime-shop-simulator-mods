using System.Text.RegularExpressions;

namespace GameMcp;

// Reads MelonLoader's log straight from disk, so it works with the game closed -- which is exactly
// when a crash log is wanted.
public static class LogReader
{
    public static string Read(string path, int lines, string grep)
    {
        if (!File.Exists(path)) return $"No log at {path}.";

        // The game keeps the file open for writing; share it instead of failing.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var all = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');

        IEnumerable<string> picked = all.Where(line => line.Length > 0);
        if (!string.IsNullOrEmpty(grep))
        {
            var pattern = new Regex(grep, RegexOptions.IgnoreCase);
            picked = picked.Where(line => pattern.IsMatch(line));
        }

        var list = picked.ToList();
        return string.Join("\n", list.Skip(Math.Max(0, list.Count - Math.Max(1, lines))));
    }
}
