using System.Globalization;

namespace Xm4Control.Core;

/// <summary>
/// Langue des textes visibles : français si Windows est en français, anglais sinon ; l'appli peut imposer l'une
/// ou l'autre. Chaque texte s'écrit dans les deux langues, côte à côte : <c>T("Bruit", "Noise")</c>.
/// </summary>
public static class Lang
{
    public static bool IsFrench { get; private set; } = SystemIsFrench;

    public static bool SystemIsFrench => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    /// <summary>Langue en cours : "fr" ou "en".</summary>
    public static string Code => IsFrench ? "fr" : "en";

    /// <summary>Impose "fr" ou "en" ; null (ou tout autre code) suit la langue de Windows.</summary>
    public static void Set(string? code) => IsFrench = code switch
    {
        "fr" => true,
        "en" => false,
        _ => SystemIsFrench,
    };

    /// <summary>Texte visible dans la langue en cours.</summary>
    public static string T(string french, string english) => IsFrench ? french : english;

    /// <summary>Pourcentage, avec une espace avant le signe en français seulement.</summary>
    public static string Percent(int value) => IsFrench ? $"{value} %" : $"{value}%";
}
