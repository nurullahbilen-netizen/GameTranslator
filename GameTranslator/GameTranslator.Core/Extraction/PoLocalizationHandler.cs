using System.Text;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Extraction;

/// <summary>Basit gettext .po desteği (msgid/msgstr, çok satırlı değerler). msgid_plural kapsam dışı.</summary>
public sealed class PoLocalizationHandler : ILocalizationHandler
{
    public string Name => "PO";
    public bool CanHandle(string path) => path.EndsWith(".po", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<TextEntry> Extract(string path)
    {
        var list = new List<TextEntry>();
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("msgid "))
            {
                var id = Unquote(lines[i][6..]);
                while (i + 1 < lines.Length && lines[i + 1].StartsWith('"')) id += Unquote(lines[++i]);
                if (id.Length > 0) list.Add(new TextEntry { File = path, Key = id, Source = id });
            }
        return list;
    }

    public void Write(string sourcePath, string destPath, IEnumerable<TextEntry> entries)
    {
        var map = HandlerUtil.Map(entries);
        var lines = File.ReadAllLines(sourcePath);
        var o = new List<string>(); var id = "";
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.StartsWith("msgid "))
            {
                id = Unquote(l[6..]); o.Add(l);
                while (i + 1 < lines.Length && lines[i + 1].StartsWith('"')) { id += Unquote(lines[++i]); o.Add(lines[i]); }
            }
            else if (l.StartsWith("msgstr "))
            {
                var orig = new List<string> { l };
                while (i + 1 < lines.Length && lines[i + 1].StartsWith('"')) orig.Add(lines[++i]);
                if (id.Length > 0 && map.TryGetValue(id, out var t)) o.Add("msgstr " + Quote(t)); else o.AddRange(orig);
            }
            else o.Add(l);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.WriteAllLines(destPath, o, new UTF8Encoding(false));
    }

    private static string Unquote(string s) { s = s.Trim(); return s.Length >= 2 ? s[1..^1].Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\") : s; }
    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
}
