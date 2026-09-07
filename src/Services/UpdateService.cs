using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProgramacioDocent.Services;

// Comprova si hi ha una versió nova de l'aplicació a GitHub Releases.
//
// Disseny per a equips capats:
//  - Només fa una petició HTTPS de només lectura, i NOMÉS quan l'usuari ho demana.
//  - Si no hi ha xarxa, retorna un resultat "sense connexió" sense fallar.
//  - NO descarrega ni sobreescriu res: només informa i dona l'enllaç. La
//    instal·lació la fa el professor manualment (descomprimir a sobre + reiniciar).
public class UpdateService
{
    // Repositori públic on es publiquen les releases.
    private const string Owner = "sergi-learns-devops";
    private const string Repo = "ProgramacioDocent";

    private static readonly HttpClient Http = CreaClient();

    private static HttpClient CreaClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub exigeix un User-Agent.
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ProgramacioDocent-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public enum Estat { AlDia, HiHaActualitzacio, SenseConnexio, Error }

    public record Resultat(
        Estat Estat,
        string VersioActual,
        string? VersioNova,
        string? Notes,
        string? UrlPaginaRelease,
        string? UrlZip,
        string? MissatgeError);

    // Versió actual de l'aplicació (llegida de l'assemblatge).
    public static string VersioActual()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        // Ens quedem amb Major.Minor.Patch.
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public async Task<Resultat> ComprovaAsync(CancellationToken ct = default)
    {
        var actual = VersioActual();
        var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

        try
        {
            using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return new Resultat(Estat.Error, actual, null, null, null, null,
                    $"El servidor ha respost {(int)resp.StatusCode}.");
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            var pagina = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

            // Busca l'actiu .zip per a la descàrrega directa.
            string? urlZip = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var nom = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (nom != null && nom.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        urlZip = a.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                        break;
                    }
                }
            }

            var novaVersio = NetejaVersio(tag);
            if (string.IsNullOrEmpty(novaVersio))
                return new Resultat(Estat.Error, actual, null, null, pagina, urlZip,
                    "No s'ha pogut interpretar la versió publicada.");

            var estat = EsMesNova(novaVersio!, actual) ? Estat.HiHaActualitzacio : Estat.AlDia;
            return new Resultat(estat, actual, novaVersio, notes, pagina, urlZip, null);
        }
        catch (OperationCanceledException)
        {
            return new Resultat(Estat.SenseConnexio, actual, null, null, null, null,
                "La comprovació ha trigat massa o s'ha cancel·lat.");
        }
        catch (HttpRequestException)
        {
            // Sense connexió a Internet o bloqueig de xarxa (habitual en equips capats).
            return new Resultat(Estat.SenseConnexio, actual, null, null, null, null,
                "No hi ha connexió a Internet o l'accés està bloquejat.");
        }
        catch (Exception ex)
        {
            return new Resultat(Estat.Error, actual, null, null, null, null, ex.Message);
        }
    }

    // Treu una possible 'v' inicial del tag (v1.1.0 -> 1.1.0).
    private static string? NetejaVersio(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        tag = tag.Trim();
        if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            tag = tag.Substring(1);
        return tag;
    }

    // Compara dues versions "Major.Minor.Patch". Retorna true si 'nova' > 'actual'.
    private static bool EsMesNova(string nova, string actual)
    {
        var a = ParseVersio(nova);
        var b = ParseVersio(actual);
        for (int i = 0; i < 3; i++)
        {
            if (a[i] > b[i]) return true;
            if (a[i] < b[i]) return false;
        }
        return false;
    }

    private static int[] ParseVersio(string v)
    {
        var parts = v.Split('.', '-', '+');
        var res = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++)
            int.TryParse(parts[i], out res[i]);
        return res;
    }
}
