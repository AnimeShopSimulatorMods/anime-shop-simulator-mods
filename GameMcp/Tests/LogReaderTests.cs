using GameMcp;
using Xunit;

namespace GameMcp.Tests;

public sealed class LogReaderTests
{
    private static string WriteLog(params string[] lines)
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Tail_returns_the_last_lines()
    {
        var path = WriteLog("a", "b", "c", "d");

        Assert.Equal("c\nd", LogReader.Read(path, lines: 2, grep: null));
    }

    [Fact]
    public void Grep_filters_before_tailing()
    {
        var path = WriteLog("[ShelfLocks] one", "noise", "[ShelfLocks] two", "noise");

        Assert.Equal("[ShelfLocks] one\n[ShelfLocks] two", LogReader.Read(path, lines: 50, grep: "ShelfLocks"));
    }

    [Fact]
    public void Missing_file_says_where_it_looked()
    {
        var text = LogReader.Read(@"C:\definitely\missing\Latest.log", 10, null);

        Assert.Contains(@"C:\definitely\missing\Latest.log", text);
    }
}
