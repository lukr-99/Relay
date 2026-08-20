using System.Text;

namespace DeckForge.Agent;

/// <summary>Tiny rolling logger — console + a single file under %AppData%\DeckForge\logs.</summary>
public sealed class Log
{
    private readonly object _gate = new();
    private readonly string _path;

    public Log(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string msg) => Write("INF", msg);
    public void Warn(string msg) => Write("WRN", msg);
    public void Error(string msg) => Write("ERR", msg);
    public void Error(string msg, Exception ex) => Write("ERR", $"{msg} :: {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {msg}";
        Console.WriteLine(line);
        try
        {
            lock (_gate)
                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* logging must never throw */ }
    }
}
