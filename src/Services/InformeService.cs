using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using ProgramacioDocent.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProgramacioDocent.Services;

// Genera informes de les notes agrupats per assignatura, en PDF, XLSX o CSV.
public class InformeService
{
    private readonly NotesService _notes;
    private static readonly CultureInfo Ca = new("ca-ES");
    private static readonly string[] DiesSetmana =
        { "", "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };

    public InformeService(Database db)
    {
        _notes = new NotesService(db);
    }

    public enum Format { PDF, XLSX, CSV }

    // Genera l'informe i el desa al camí indicat.
    public void Genera(Format format, DateTime desde, DateTime fins, string cursEscolar, string rutaSortida)
    {
        var files = _notes.ObteNotesPerInterval(desde, fins);
        switch (format)
        {
            case Format.PDF: GeneraPdf(files, desde, fins, cursEscolar, rutaSortida); break;
            case Format.XLSX: GeneraXlsx(files, desde, fins, cursEscolar, rutaSortida); break;
            case Format.CSV: GeneraCsv(files, rutaSortida); break;
        }
    }

    public static string ExtensioPer(Format f) => f switch
    {
        Format.PDF => "pdf",
        Format.XLSX => "xlsx",
        Format.CSV => "csv",
        _ => "txt"
    };

    private void GeneraPdf(
        List<NotesService.FilaInforme> files, DateTime desde, DateTime fins,
        string cursEscolar, string ruta)
    {
        var perAssignatura = files
            .GroupBy(f => new { f.Assignatura, f.Curs })
            .OrderBy(g => g.Key.Assignatura);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Informe de notes de classe").FontSize(18).SemiBold();
                    col.Item().Text($"Curs {cursEscolar}").FontSize(11);
                    col.Item().Text(
                        $"Període: {desde.ToString("d MMMM yyyy", Ca)} – {fins.ToString("d MMMM yyyy", Ca)}")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    if (!files.Any())
                    {
                        col.Item().PaddingTop(20).Text("No hi ha notes en aquest període.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    foreach (var grup in perAssignatura)
                    {
                        col.Item().PaddingTop(12).Text(t =>
                        {
                            t.Span(grup.Key.Assignatura).FontSize(13).SemiBold();
                            if (!string.IsNullOrWhiteSpace(grup.Key.Curs))
                                t.Span($"  ({grup.Key.Curs})").FontSize(11).FontColor(Colors.Grey.Darken1);
                        });

                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(75);  // data
                                c.ConstantColumn(70);   // dia
                                c.ConstantColumn(80);   // franja
                                c.RelativeColumn();      // nota
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(CelJustHeader).Text("Setmana");
                                h.Cell().Element(CelJustHeader).Text("Dia");
                                h.Cell().Element(CelJustHeader).Text("Hora");
                                h.Cell().Element(CelJustHeader).Text("Nota");
                            });

                            foreach (var fila in grup.OrderBy(x => x.DataDilluns).ThenBy(x => x.DiaSetmana))
                            {
                                var dia = fila.DiaSetmana >= 1 && fila.DiaSetmana <= 5
                                    ? DiesSetmana[fila.DiaSetmana] : "";
                                table.Cell().Element(Cel).Text(fila.DataDilluns.ToString("dd/MM/yyyy", Ca));
                                table.Cell().Element(Cel).Text(dia);
                                table.Cell().Element(Cel).Text(fila.Franja);
                                table.Cell().Element(Cel).Text(fila.Text);
                            }
                        });
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Programació Docent · ");
                    t.Span($"Generat el {DateTime.Now.ToString("d MMMM yyyy HH:mm", Ca)}");
                });
            });
        }).GeneratePdf(ruta);
    }

    private static IContainer CelJustHeader(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(4)
         .BorderBottom(1).BorderColor(Colors.Grey.Medium);

    private static IContainer Cel(IContainer c) =>
        c.PaddingVertical(3).PaddingHorizontal(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

    private void GeneraXlsx(
        List<NotesService.FilaInforme> files, DateTime desde, DateTime fins,
        string cursEscolar, string ruta)
    {
        using var wb = new XLWorkbook();

        // Un full per assignatura.
        var grups = files
            .GroupBy(f => f.Assignatura)
            .OrderBy(g => g.Key)
            .ToList();

        if (grups.Count == 0)
        {
            var buit = wb.Worksheets.Add("Sense notes");
            buit.Cell(1, 1).Value = "No hi ha notes en aquest període.";
        }

        foreach (var grup in grups)
        {
            var nomFull = NetejaNomFull(grup.Key);
            var ws = wb.Worksheets.Add(nomFull);
            ws.Cell(1, 1).Value = "Setmana (dilluns)";
            ws.Cell(1, 2).Value = "Dia";
            ws.Cell(1, 3).Value = "Hora";
            ws.Cell(1, 4).Value = "Curs";
            ws.Cell(1, 5).Value = "Nota";
            ws.Row(1).Style.Font.Bold = true;

            int fila = 2;
            foreach (var f in grup.OrderBy(x => x.DataDilluns).ThenBy(x => x.DiaSetmana))
            {
                var dia = f.DiaSetmana >= 1 && f.DiaSetmana <= 5 ? DiesSetmana[f.DiaSetmana] : "";
                ws.Cell(fila, 1).Value = f.DataDilluns.ToString("dd/MM/yyyy");
                ws.Cell(fila, 2).Value = dia;
                ws.Cell(fila, 3).Value = f.Franja;
                ws.Cell(fila, 4).Value = f.Curs;
                ws.Cell(fila, 5).Value = f.Text;
                fila++;
            }
            ws.Columns().AdjustToContents();
            ws.Column(5).Width = 60;
            ws.Column(5).Style.Alignment.WrapText = true;
        }

        wb.SaveAs(ruta);
    }

    private void GeneraCsv(List<NotesService.FilaInforme> files, string ruta)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Assignatura;Curs;Setmana;Dia;Hora;Nota");
        foreach (var f in files.OrderBy(x => x.Assignatura).ThenBy(x => x.DataDilluns).ThenBy(x => x.DiaSetmana))
        {
            var dia = f.DiaSetmana >= 1 && f.DiaSetmana <= 5 ? DiesSetmana[f.DiaSetmana] : "";
            sb.Append(Camp(f.Assignatura)).Append(';')
              .Append(Camp(f.Curs)).Append(';')
              .Append(Camp(f.DataDilluns.ToString("dd/MM/yyyy"))).Append(';')
              .Append(Camp(dia)).Append(';')
              .Append(Camp(f.Franja)).Append(';')
              .Append(Camp(f.Text)).AppendLine();
        }
        // BOM UTF-8 perquè Excel obri correctament els accents.
        File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));
    }

    // Escapa un camp per a CSV amb separador ';'.
    private static string Camp(string valor)
    {
        valor ??= string.Empty;
        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r'))
            return "\"" + valor.Replace("\"", "\"\"") + "\"";
        return valor;
    }

    // Els noms de full d'Excel no poden superar 31 caràcters ni tenir certs símbols.
    private static string NetejaNomFull(string nom)
    {
        var invalids = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        foreach (var c in invalids) nom = nom.Replace(c, ' ');
        nom = nom.Trim();
        if (nom.Length > 31) nom = nom.Substring(0, 31);
        return string.IsNullOrWhiteSpace(nom) ? "Assignatura" : nom;
    }
}
