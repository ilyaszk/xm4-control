using System.Text.RegularExpressions;

namespace Xm4Control.Tests;

/// <summary>
/// La page de la fenêtre principale est écrite en français ; en anglais, app.js remplace le texte d'un élément par
/// son attribut data-en, et ses attributs par data-en-aria-label, data-en-alt. Rien ne doit rester en français.
/// </summary>
public class WebPageTests
{
    // Identiques dans les deux langues.
    private static readonly HashSet<string> SameInBothLanguages =
        ["XM4", "Control", "XM4 Control", "Speak-to-Chat", "DSEE Extreme", "Codec", "Firmware", "Ctrl", "Alt", "N", "Navigation"];

    // Commentaires, scripts et titre à part ; puis balises et textes.
    private static readonly Regex Tokens = new(@"<!--.*?-->|<(script|style|title)\b.*?</\1>|<[^>]+>|[^<]+", RegexOptions.Singleline);
    private static readonly Regex Attribute = new(@"([\w-]+)=""([^""]*)""");

    [Fact]
    public void EveryText_HasEnglishVersion()
    {
        var tokens = ReadTokens();
        var missing = new List<string>();
        for (var i = 1; i < tokens.Count - 1; i++)
        {
            var text = tokens[i].Trim();
            if (tokens[i].StartsWith('<') || !text.Any(char.IsLetter) || SameInBothLanguages.Contains(text)) continue;
            // Traduit : l'élément ne contient que ce texte et porte data-en.
            if (!(tokens[i - 1].Contains(" data-en=") && tokens[i + 1].StartsWith("</"))) missing.Add(text);
        }
        Assert.Empty(missing);
    }

    [Fact]
    public void TranslatedElements_ContainOnlyText()
    {
        // data-en remplace tout le contenu de l'élément : un élément enfant serait perdu en anglais.
        var tokens = ReadTokens();
        var broken = tokens
            .Select((token, i) => (token, i))
            .Where(t => t.token.Contains(" data-en=")
                && !(t.i + 2 < tokens.Count && !tokens[t.i + 1].StartsWith('<') && tokens[t.i + 2].StartsWith("</")))
            .Select(t => t.token);
        Assert.Empty(broken);
    }

    [Fact]
    public void EveryAccessibleName_HasEnglishVersion()
    {
        var missing = new List<string>();
        foreach (var tag in ReadTokens().Where(t => t.StartsWith('<') && !t.StartsWith("<!--")))
        {
            var attributes = Attribute.Matches(tag).ToDictionary(a => a.Groups[1].Value, a => a.Groups[2].Value);
            foreach (var name in new[] { "aria-label", "alt" })
            {
                if (attributes.TryGetValue(name, out var value)
                    && !SameInBothLanguages.Contains(value)
                    && !attributes.ContainsKey("data-en-" + name))
                    missing.Add(value);
            }
        }
        Assert.Empty(missing);
    }

    private static List<string> ReadTokens() =>
        [.. Tokens.Matches(File.ReadAllText(IndexPath())).Select(m => m.Value).Where(t => t.Trim().Length > 0)];

    private static string IndexPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Xm4Control.slnx"))) dir = dir.Parent!;
        return Path.Combine(dir.FullName, "src", "Xm4Control.App", "Web", "index.html");
    }
}
