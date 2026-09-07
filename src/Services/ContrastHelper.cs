using System;
using System.Globalization;
using Avalonia.Media;

namespace ProgramacioDocent.Services;

// Decideix un color de text llegible (fosc o blanc) sobre un color de fons,
// segons la luminància relativa (fórmula WCAG). Així el color d'assignatura
// triat per l'usuari és sempre llegible, en tema clar i en tema fosc.
public static class ContrastHelper
{
    private static readonly Color TextFosc = Color.Parse("#1F2933");
    private static readonly Color TextClar = Colors.White;

    public static Color TextSobre(string colorFonsHex)
    {
        var fons = ParseColor(colorFonsHex);
        return LuminanciaRelativa(fons) > 0.5 ? TextFosc : TextClar;
    }

    public static IBrush BrushTextSobre(string colorFonsHex)
        => new SolidColorBrush(TextSobre(colorFonsHex));

    private static Color ParseColor(string hex)
    {
        try { return Color.Parse(hex); }
        catch { return Color.Parse("#4F86C6"); }
    }

    // Luminància relativa WCAG amb correcció gamma (0 = negre, 1 = blanc).
    private static double LuminanciaRelativa(Color c)
    {
        double R = CanalLineal(c.R / 255.0);
        double G = CanalLineal(c.G / 255.0);
        double B = CanalLineal(c.B / 255.0);
        return 0.2126 * R + 0.7152 * G + 0.0722 * B;
    }

    private static double CanalLineal(double c)
        => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
