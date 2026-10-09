using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using VicdogsPeru.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace VicdogsPeru.Services;

/// <summary>Genera archivos Word (.docx) con la ficha de uno o varios caninos: una ficha por página.</summary>
public static class FichaWord
{
    private const string Azul = "0432A8";
    private const string AzulSuave = "E8EEFB";
    private const string Linea = "D5D9E3";
    private const string Gris = "5B6272";
    private const int AnchoTotal = 10200;   // A4 con márgenes de 1,5 cm, en veinteavos de punto

    public static byte[] Generar(IEnumerable<Canino> caninos, string carpetaWeb)
    {
        var lista = caninos.ToList();
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());
            uint idImagen = 1;

            for (int i = 0; i < lista.Count; i++)
            {
                AgregarFicha(body, main, lista[i], carpetaWeb, ref idImagen, i < lista.Count - 1);
            }

            body.Append(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 850, Bottom = 850, Left = 850, Right = 850, Header = 400, Footer = 400, Gutter = 0 }));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- ficha
    private static void AgregarFicha(Body body, MainDocumentPart main, Canino c, string carpetaWeb, ref uint idImagen, bool saltoDePagina)
    {
        // Banda azul con la marca
        var banda = Tabla(new[] { AnchoTotal }, sinBordes: true);
        var filaBanda = new TableRow();
        var celdaBanda = Celda(AnchoTotal, relleno: Azul, margen: 140);
        celdaBanda.Append(Parrafo(new[] { Texto("VICDOGS PERÚ", 30, true, "FFFFFF"), Texto("   ·   Ficha del canino", 22, false, "FFFFFF") }));
        filaBanda.Append(celdaBanda);
        banda.Append(filaBanda);
        body.Append(banda);
        body.Append(Espacio(120));

        // Foto + datos principales
        var principal = Tabla(new[] { 3300, AnchoTotal - 3300 }, sinBordes: true);
        var fila = new TableRow();
        var celdaFoto = Celda(3300, margen: 0);
        var rutaFoto = RutaFoto(c, carpetaWeb);
        if (rutaFoto != null && TryImagen(main, rutaFoto, ref idImagen, out var run))
            celdaFoto.Append(new Paragraph(run));
        else
            celdaFoto.Append(Parrafo(new[] { Texto("Sin foto", 20, false, Gris) }));
        fila.Append(celdaFoto);

        var celdaDatos = Celda(AnchoTotal - 3300, margen: 0);
        celdaDatos.Append(Parrafo(new[] { Texto(c.Nombre ?? "", 52, true, Azul) }, despues: 40));
        var sub = new List<string>();
        if (c.Servicio != null) sub.Add(c.Servicio.Nombre);
        sub.Add(c.Activo ? "Activo" : "Inactivo");
        sub.Add(c.EstadoVacunas().Texto);
        celdaDatos.Append(Parrafo(new[] { Texto(string.Join("  ·  ", sub), 21, false, Gris) }, despues: 120));
        celdaDatos.Append(TablaDatos(new[]
        {
            ("Sexo", c.Sexo), ("Edad", c.EdadTexto), ("Fecha de nacimiento", c.FechaNacimiento.Fecha()),
            ("Raza o cruce", c.Raza), ("Distrito", c.Distrito),
            ("Peso referencial", c.PesoKg.HasValue ? c.PesoKg.Value.ToString("0.##") + " kg" : null),
        }, AnchoTotal - 3300 - 200));
        celdaDatos.Append(Espacio(0));
        fila.Append(celdaDatos);
        principal.Append(fila);
        body.Append(principal);

        // Cuidado diario
        body.Append(Titulo("Cuidado y alimentación"));
        body.Append(TablaDatos(new[]
        {
            ("Tipo de baño", string.IsNullOrEmpty(c.TipoBano) ? null : "Baño " + c.TipoBano.ToLower()),
            ("Alimentación", c.AlimentacionTipo), ("Marca de alimento", c.AlimentacionMarca),
            ("Tipo de agua", c.TipoAgua), ("Hospedaje", c.HospedajeTexto),
            (c.EtiquetaFechaRegistro, c.FechaRegistro.Fecha()),
        }, AnchoTotal));

        // Tutores
        body.Append(Titulo(c.Tutores.Count == 1 ? "Tutor" : "Tutores"));
        if (!c.Tutores.Any()) body.Append(Parrafo(new[] { Texto("Sin tutor registrado.", 20, false, Gris) }));
        else
        {
            var t = Tabla(new[] { 2600, 1900, 2000, 3700 });
            t.Append(Encabezado(new[] { "Tutor", "Teléfono", "Cumpleaños", "Redes y dirección" }, new[] { 2600, 1900, 2000, 3700 }));
            foreach (var tutor in c.Tutores.Select(x => x.Tutor).OrderBy(x => x.Nombre))
            {
                var redes = new List<string>();
                if (Ayudas.TextoRed(tutor.Instagram, "instagram") is string ig) redes.Add("Instagram: " + ig);
                if (Ayudas.TextoRed(tutor.TikTok, "tiktok") is string tt) redes.Add("TikTok: " + tt);
                if (Ayudas.TextoRed(tutor.Facebook, "facebook") is string fb) redes.Add("Facebook: " + fb);
                if (!string.IsNullOrEmpty(tutor.Direccion)) redes.Add(tutor.Direccion);
                t.Append(Fila(new[] { tutor.Nombre, tutor.Telefono, tutor.Cumpleanos?.DiaYMes(), redes.Any() ? string.Join("\n", redes) : null },
                    new[] { 2600, 1900, 2000, 3700 }, negritaPrimera: true));
            }
            body.Append(t);
        }

        // Redes del perro
        var igPerro = Ayudas.Etiqueta(c.Instagram);
        var ttPerro = Ayudas.Etiqueta(c.TikTok);
        if (igPerro != null || ttPerro != null)
        {
            body.Append(Titulo("Redes sociales del perro"));
            body.Append(TablaDatos(new[] { ("Instagram", igPerro), ("TikTok", ttPerro) }, AnchoTotal));
        }

        // Vacunas
        body.Append(Titulo("Vacunas"));
        if (!c.Vacunas.Any()) body.Append(Parrafo(new[] { Texto("Sin vacunas registradas.", 20, false, Gris) }));
        else
        {
            var w = new[] { 2400, 2400, 2400, 2800 };
            var t = Tabla(w);
            t.Append(Encabezado(new[] { "Vacuna", "Aplicada", "Próxima", "Nota" }, w));
            foreach (var v in c.Vacunas.OrderBy(x => x.Tipo).ThenByDescending(x => x.FechaAplicada))
                t.Append(Fila(new[] { v.Tipo, v.FechaAplicada.Fecha(), v.Vencimiento.Fecha(), v.Observacion }, w, negritaPrimera: true));
            body.Append(t);
        }

        // Antipulgas y desparasitación
        body.Append(Titulo("Antipulgas y desparasitación"));
        body.Append(Parrafo(new[] { Texto("Antipulgas", 21, true) }, despues: 40));
        AgregarRegistros(body,
            new[] { "Fecha", "Marca", "Duración", "Próxima" }, new[] { 2400, 3600, 2000, 2200 },
            c.Antipulgas.OrderByDescending(a => a.Fecha).Select(a => new[]
            {
                a.Fecha.Fecha(), a.Marca,
                a.Meses.HasValue ? a.Meses + (a.Meses == 1 ? " mes" : " meses") : null,
                a.Proxima.Fecha()
            }));
        body.Append(Parrafo(new[] { Texto("Desparasitación", 21, true) }, antes: 100, despues: 40));
        AgregarRegistros(body,
            new[] { "Fecha", "Marca", "Próxima" }, new[] { 2400, 5600, 2200 },
            c.Desparasitaciones.OrderByDescending(d => d.Fecha).Select(d => new[]
            {
                d.Fecha.Fecha(), d.Producto, d.Proxima.Fecha()
            }));

        // Peso
        var pesos = c.Pesos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Take(4).ToList();
        if (pesos.Any())
        {
            body.Append(Titulo("Últimos pesos"));
            var w = new[] { 2200, 2000, 6000 };
            var t = Tabla(w);
            t.Append(Encabezado(new[] { "Fecha", "Peso", "Nota" }, w));
            foreach (var p in pesos)
                t.Append(Fila(new[] { p.Fecha.Fecha(), p.PesoKg.ToString("0.##") + " kg", p.Nota }, w));
            body.Append(t);
        }

        // Observaciones
        if (!string.IsNullOrWhiteSpace(c.Observaciones))
        {
            body.Append(Titulo("Observaciones"));
            var t = Tabla(new[] { AnchoTotal });
            var f = new TableRow();
            var cel = Celda(AnchoTotal, relleno: AzulSuave, margen: 120);
            cel.Append(Parrafo(new[] { Texto(c.Observaciones, 21) }));
            f.Append(cel); t.Append(f);
            body.Append(t);
        }

        // El salto de página va dentro del último párrafo para no dejar páginas en blanco
        var pie = Parrafo(new[] { Texto("Ficha generada el " + DateTime.Today.Fecha() + " · Vicdogs Perú", 16, false, Gris) }, antes: 120);
        if (saltoDePagina) pie.Append(new Run(new Break { Type = BreakValues.Page }));
        body.Append(pie);
    }

    private static void AgregarRegistros(OpenXmlElement destino, string[] titulos, int[] w, IEnumerable<string[]> registros)
    {
        var lista = registros.ToList();
        if (!lista.Any()) { destino.Append(Parrafo(new[] { Texto("Sin registros.", 20, false, Gris) })); return; }
        var t = Tabla(w);
        t.Append(Encabezado(titulos, w));
        foreach (var r in lista.Take(6)) t.Append(Fila(r, w));
        destino.Append(t);
    }

    // ---------------------------------------------------------------- piezas de Word
    private static Paragraph Titulo(string texto)
    {
        var p = Parrafo(new[] { Texto(texto.ToUpper(), 22, true, Azul) }, antes: 160, despues: 60);
        p.ParagraphProperties.Append(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 8, Color = Azul, Space = 2 }));
        p.ParagraphProperties.KeepNext = new KeepNext();
        return p;
    }

    private static Paragraph Espacio(int despues) =>
        Parrafo(new[] { new Run(new RunProperties(new FontSize { Val = "4" })) }, despues: despues);

    private static Run Texto(string texto, int mediosPuntos, bool negrita = false, string color = null)
    {
        var rp = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (negrita) rp.Append(new Bold());
        if (color != null) rp.Append(new Color { Val = color });
        rp.Append(new FontSize { Val = mediosPuntos.ToString() });
        var run = new Run(rp);
        // Los saltos de línea del texto se convierten en saltos de línea de Word
        var partes = (texto ?? "").Split('\n');
        for (int i = 0; i < partes.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            run.Append(new Text(partes[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    private static Paragraph Parrafo(IEnumerable<Run> runs, int antes = 0, int despues = 0)
    {
        var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = antes.ToString(), After = despues.ToString() }));
        foreach (var r in runs) p.Append(r);
        return p;
    }

    private static Table Tabla(int[] anchos, bool sinBordes = false)
    {
        var props = new TableProperties(
            new TableWidth { Width = anchos.Sum().ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed });
        if (!sinBordes)
            props.Append(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = Linea },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linea },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Linea },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = Linea },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Linea },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Linea }));
        var t = new Table(props);
        var grid = new TableGrid();
        foreach (var a in anchos) grid.Append(new GridColumn { Width = a.ToString() });
        t.Append(grid);
        return t;
    }

    private static TableCell Celda(int ancho, string relleno = null, int margen = 80)
    {
        var pr = new TableCellProperties(new TableCellWidth { Width = ancho.ToString(), Type = TableWidthUnitValues.Dxa });
        if (relleno != null) pr.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = relleno, Color = "auto" });
        pr.Append(new TableCellMargin(
            new TopMargin { Width = margen.ToString(), Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = margen.ToString(), Type = TableWidthUnitValues.Dxa },
            new LeftMargin { Width = Math.Max(margen, 60).ToString(), Type = TableWidthUnitValues.Dxa },
            new RightMargin { Width = Math.Max(margen, 60).ToString(), Type = TableWidthUnitValues.Dxa }));
        return new TableCell(pr);
    }

    private static TableRow Encabezado(string[] titulos, int[] anchos)
    {
        var f = new TableRow(new TableRowProperties(new TableHeader()));
        for (int i = 0; i < titulos.Length; i++)
        {
            var c = Celda(anchos[i], relleno: Azul, margen: 60);
            c.Append(Parrafo(new[] { Texto(titulos[i], 19, true, "FFFFFF") }));
            f.Append(c);
        }
        return f;
    }

    private static TableRow Fila(string[] valores, int[] anchos, bool negritaPrimera = false)
    {
        var f = new TableRow(new TableRowProperties(new CantSplit()));
        for (int i = 0; i < valores.Length; i++)
        {
            var c = Celda(anchos[i], margen: 60);
            var v = string.IsNullOrWhiteSpace(valores[i]) ? "—" : valores[i];
            c.Append(Parrafo(new[] { Texto(v, 20, negritaPrimera && i == 0) }));
            f.Append(c);
        }
        return f;
    }

    /// <summary>Tabla de «etiqueta → valor» en dos columnas de pares.</summary>
    private static Table TablaDatos((string Etiqueta, string Valor)[] datos, int ancho)
    {
        int et = ancho / 6, va = ancho / 2 - et;   // etiqueta | valor | etiqueta | valor
        var w = new[] { et, va, et, va };
        var t = Tabla(w);
        for (int i = 0; i < datos.Length; i += 2)
        {
            var f = new TableRow(new TableRowProperties(new CantSplit()));
            for (int j = 0; j < 2; j++)
            {
                var par = i + j < datos.Length ? datos[i + j] : ("", "");
                var ce = Celda(et, relleno: AzulSuave, margen: 60);
                ce.Append(Parrafo(new[] { Texto(par.Item1, 19, true, Azul) }));
                var cv = Celda(va, margen: 60);
                cv.Append(Parrafo(new[] { Texto(string.IsNullOrWhiteSpace(par.Item2) ? (par.Item1 == "" ? "" : "—") : par.Item2, 20) }));
                f.Append(ce, cv);
            }
            t.Append(f);
        }
        return t;
    }

    // ---------------------------------------------------------------- foto
    private static string RutaFoto(Canino c, string carpetaWeb)
    {
        if (string.IsNullOrEmpty(c.FotoUrl)) return null;
        var ruta = Path.GetFullPath(Path.Combine(carpetaWeb, c.FotoUrl.TrimStart('/', '\\')));
        // Solo se leen fotos que estén dentro de wwwroot
        return ruta.StartsWith(Path.GetFullPath(carpetaWeb), StringComparison.OrdinalIgnoreCase) && File.Exists(ruta) ? ruta : null;
    }

    /// <summary>Inserta la foto en el documento conservando su proporción. Devuelve false si no se pudo leer.</summary>
    private static bool TryImagen(MainDocumentPart main, string ruta, ref uint id, out Run run)
    {
        run = null;
        try
        {
            var bytes = File.ReadAllBytes(ruta);
            var ext = Path.GetExtension(ruta).ToLowerInvariant();
            var tipo = ext switch
            {
                ".png" => ImagePartType.Png,
                ".jpg" or ".jpeg" => ImagePartType.Jpeg,
                _ => (ImagePartType?)null     // Word no admite WEBP: se omite
            };
            if (tipo == null) return false;

            var (w, h) = Dimensiones(bytes, ext);
            if (w <= 0 || h <= 0) { w = 1; h = 1; }

            // 1 cm = 360 000 EMU; la foto cabe en 5,3 cm de ancho y 6 cm de alto como máximo
            long maxAncho = 1_908_000L, maxAlto = 2_160_000L;
            double escala = Math.Min((double)maxAncho / w, (double)maxAlto / h);
            long cx = (long)(w * escala), cy = (long)(h * escala);

            var parte = main.AddImagePart(tipo.Value);
            using (var s = new MemoryStream(bytes)) parte.FeedData(s);
            var rel = main.GetIdOfPart(parte);
            var n = id++;

            var dibujo = new Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = n, Name = "Foto " + n },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = "foto" + n + ext },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = rel }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

            run = new Run(dibujo);
            return true;
        }
        catch
        {
            run = null;
            return false;   // si la foto está dañada, la ficha se genera igual sin ella
        }
    }

    private static (int, int) Dimensiones(byte[] b, string ext)
    {
        try
        {
            if (ext == ".png" && b.Length > 24)
                return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);

            // JPEG: se busca el marcador SOF que guarda el alto y el ancho
            int i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                int m = b[i + 1];
                if (m == 0xFF) { i++; continue; }
                if ((m >= 0xC0 && m <= 0xCF) && m != 0xC4 && m != 0xC8 && m != 0xCC)
                    return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                i += 2 + ((b[i + 2] << 8) | b[i + 3]);
            }
        }
        catch { }
        return (0, 0);
    }
}
