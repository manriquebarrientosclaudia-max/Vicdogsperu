using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;
using VicdogsPeru.Services;

namespace VicdogsPeru.Controllers;

public class CaninosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private static readonly string[] ExtensionesFoto = { ".jpg", ".jpeg", ".png", ".webp" };

    public CaninosController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    // ---------- Listado ----------
    private IQueryable<Canino> Filtrar(string q, int? servicioId, string distrito, string sexo, string estado)
    {
        var query = _db.Caninos.Include(c => c.Servicio)
            .Include(c => c.Tutores).ThenInclude(t => t.Tutor)
            .Include(c => c.Vacunas).Include(c => c.Desparasitaciones).Include(c => c.Antipulgas)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var pat = Busqueda.Contiene(q);
            query = query.Where(c => EF.Functions.ILike(c.Nombre, pat) || EF.Functions.ILike(c.Raza, pat) ||
                                     c.Tutores.Any(t => EF.Functions.ILike(t.Tutor.Nombre, pat)));
        }
        if (servicioId.HasValue) query = query.Where(c => c.ServicioId == servicioId);
        if (!string.IsNullOrEmpty(distrito)) query = query.Where(c => c.Distrito == distrito);
        if (!string.IsNullOrEmpty(sexo)) query = query.Where(c => c.Sexo == sexo);
        if (estado == "inactivos") query = query.Where(c => !c.Activo);
        else if (estado != "todos") query = query.Where(c => c.Activo);   // por defecto: activos
        return query.OrderBy(c => c.Nombre);
    }

    private async Task CargarFiltrosAsync()
    {
        ViewBag.Servicios = new SelectList(await _db.Servicios.OrderBy(s => s.Id).ToListAsync(), "Id", "Nombre");
        ViewBag.Distritos = await _db.Caninos.Where(c => c.Distrito != null && c.Distrito != "")
            .Select(c => c.Distrito).Distinct().OrderBy(d => d).ToListAsync();
    }

    public async Task<IActionResult> Index(string q, int? servicioId, string distrito, string sexo, string estado)
    {
        await CargarFiltrosAsync();
        ViewBag.Q = q; ViewBag.ServicioId = servicioId; ViewBag.Distrito = distrito; ViewBag.Sexo = sexo; ViewBag.Estado = estado;
        return View(await Filtrar(q, servicioId, distrito, sexo, estado).ToListAsync());
    }

    // ---------- Ficha ----------
    public async Task<IActionResult> Details(int id)
    {
        var c = await _db.Caninos.Include(x => x.Servicio)
            .Include(x => x.Tutores).ThenInclude(t => t.Tutor)
            .Include(x => x.Vacunas).Include(x => x.Antipulgas).Include(x => x.Desparasitaciones)
            .Include(x => x.Pesos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }

    // ---------- Crear ----------
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await CargarFormularioAsync(new int[0]);
        return View("Form", new Canino { Activo = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Canino canino, int[] tutorIds, string nuevoTutor, IFormFile foto)
    {
        ValidarFoto(foto);
        ValidarHospedaje(canino);
        if (!ModelState.IsValid)
        {
            await CargarFormularioAsync(tutorIds ?? new int[0]);
            return View("Form", canino);
        }

        canino.Id = 0;
        canino.FechaRegistro = canino.FechaRegistro.Year > 2000 ? canino.FechaRegistro.Date : DateTime.Today;
        canino.FotoUrl = await GuardarFotoAsync(foto);
        canino.Instagram = LimpiarRed(canino.Instagram);
        canino.TikTok = LimpiarRed(canino.TikTok);
        _db.Caninos.Add(canino);
        await _db.SaveChangesAsync();

        // El peso que se escribe al registrar al perro queda como su primer registro de peso
        if (canino.PesoKg.HasValue && canino.PesoKg > 0)
        {
            _db.Pesos.Add(new PesoRegistro { CaninoId = canino.Id, Fecha = DateTime.Today, PesoKg = canino.PesoKg.Value, Nota = "Peso inicial" });
            await _db.SaveChangesAsync();
        }

        await AsignarTutoresAsync(canino.Id, tutorIds, nuevoTutor);
        TempData["Ok"] = $"{canino.Nombre} fue registrado.";
        return RedirectToAction(nameof(Details), new { id = canino.Id });
    }

    // ---------- Editar ----------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Caninos.Include(x => x.Tutores).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        await CargarFormularioAsync(c.Tutores.Select(t => t.TutorId).ToArray());
        return View("Form", c);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Canino canino, int[] tutorIds, string nuevoTutor, IFormFile foto, bool quitarFoto = false)
    {
        var c = await _db.Caninos.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        ValidarFoto(foto);
        ValidarHospedaje(canino);
        if (!ModelState.IsValid)
        {
            canino.Id = id; canino.FotoUrl = c.FotoUrl;
            await CargarFormularioAsync(tutorIds ?? new int[0]);
            return View("Form", canino);
        }

        c.Nombre = canino.Nombre; c.Sexo = canino.Sexo; c.EdadTexto = canino.EdadTexto;
        c.FechaNacimiento = canino.FechaNacimiento; c.Raza = canino.Raza;
        c.ServicioId = canino.ServicioId; c.Distrito = canino.Distrito;
        c.Instagram = LimpiarRed(canino.Instagram); c.TikTok = LimpiarRed(canino.TikTok);
        c.TipoBano = canino.TipoBano; c.AlimentacionTipo = canino.AlimentacionTipo;
        c.AlimentacionMarca = canino.AlimentacionMarca; c.Observaciones = canino.Observaciones;
        c.Activo = canino.Activo;
        c.TipoAgua = canino.TipoAgua;
        c.HospedajeDesde = canino.HospedajeDesde; c.HospedajeHasta = canino.HospedajeHasta;
        if (canino.FechaRegistro.Year > 2000) c.FechaRegistro = canino.FechaRegistro.Date;

        if (foto != null && foto.Length > 0)
        {
            BorrarFoto(c.FotoUrl);
            c.FotoUrl = await GuardarFotoAsync(foto);
        }
        else if (quitarFoto)
        {
            BorrarFoto(c.FotoUrl);
            c.FotoUrl = null;
        }

        await _db.SaveChangesAsync();
        await AsignarTutoresAsync(c.Id, tutorIds, nuevoTutor);
        TempData["Ok"] = $"Los datos de {c.Nombre} se guardaron.";
        return RedirectToAction(nameof(Details), new { id = c.Id });
    }

    // ---------- Eliminar ----------
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Caninos.Include(x => x.Servicio).FirstOrDefaultAsync(x => x.Id == id);
        return c == null ? NotFound() : View(c);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var c = await _db.Caninos.FindAsync(id);
        if (c != null)
        {
            BorrarFoto(c.FotoUrl);
            _db.Caninos.Remove(c);
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"{c.Nombre} fue eliminado.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Vacunas, antipulgas y desparasitaciones ----------
    [HttpPost]
    public async Task<IActionResult> AgregarVacuna(int caninoId, string tipo, DateTime? fechaAplicada, DateTime? fechaProxima, string observacion)
    {
        if (string.IsNullOrWhiteSpace(tipo) || (fechaAplicada == null && fechaProxima == null))
            TempData["Error"] = "Indica el tipo de vacuna y al menos una fecha (aplicada o próxima).";
        else
        {
            _db.Vacunas.Add(new Vacuna { CaninoId = caninoId, Tipo = tipo, FechaAplicada = fechaAplicada, FechaProxima = fechaProxima, Observacion = observacion });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Vacuna registrada.";
        }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> QuitarVacuna(int id, int caninoId)
    {
        var v = await _db.Vacunas.FindAsync(id);
        if (v != null) { _db.Vacunas.Remove(v); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> AgregarAntipulga(int caninoId, DateTime? fecha, string marca, int? meses)
    {
        if (fecha == null && string.IsNullOrWhiteSpace(marca))
            TempData["Error"] = "Indica la fecha o la marca del antipulgas.";
        else
        {
            _db.Antipulgas.Add(new Antipulga { CaninoId = caninoId, Fecha = fecha, Marca = marca, Meses = meses is 1 or 3 ? meses : null });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Antipulgas registrado.";
        }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> QuitarAntipulga(int id, int caninoId)
    {
        var a = await _db.Antipulgas.FindAsync(id);
        if (a != null) { _db.Antipulgas.Remove(a); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> AgregarDesparasitacion(int caninoId, DateTime? fecha, string producto)
    {
        if (fecha == null) TempData["Error"] = "Indica la fecha de la desparasitación.";
        else
        {
            _db.Desparasitaciones.Add(new Desparasitacion { CaninoId = caninoId, Fecha = fecha.Value, Producto = producto });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Desparasitación registrada.";
        }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> QuitarDesparasitacion(int id, int caninoId)
    {
        var d = await _db.Desparasitaciones.FindAsync(id);
        if (d != null) { _db.Desparasitaciones.Remove(d); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    // ---------- Registro de pesos ----------
    [HttpPost]
    public async Task<IActionResult> AgregarPeso(int caninoId, DateTime? fecha, decimal? pesoKg, string nota)
    {
        if (pesoKg == null || pesoKg <= 0 || pesoKg > 200)
            TempData["Error"] = "Escribe un peso válido en kilos (entre 0.1 y 200).";
        else
        {
            _db.Pesos.Add(new PesoRegistro { CaninoId = caninoId, Fecha = (fecha ?? DateTime.Today).Date, PesoKg = pesoKg.Value, Nota = nota });
            await _db.SaveChangesAsync();
            await SincronizarPesoAsync(caninoId);
            TempData["Ok"] = "Peso registrado.";
        }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    [HttpPost]
    public async Task<IActionResult> QuitarPeso(int id, int caninoId)
    {
        var p = await _db.Pesos.FindAsync(id);
        if (p != null)
        {
            _db.Pesos.Remove(p);
            await _db.SaveChangesAsync();
            await SincronizarPesoAsync(caninoId);
        }
        return RedirectToAction(nameof(Details), new { id = caninoId });
    }

    /// <summary>El peso que se muestra en la ficha es siempre el del último registro.</summary>
    private async Task SincronizarPesoAsync(int caninoId)
    {
        var c = await _db.Caninos.FindAsync(caninoId);
        if (c == null) return;
        var ultimo = await _db.Pesos.Where(p => p.CaninoId == caninoId)
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).FirstOrDefaultAsync();
        c.PesoKg = ultimo?.PesoKg;
        await _db.SaveChangesAsync();
    }

    private static string LimpiarRed(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // ---------- Fichas en Word ----------
    private const string TipoWord = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private IQueryable<Canino> ConDatosDeFicha(IQueryable<Canino> q) =>
        q.Include(c => c.Servicio)
         .Include(c => c.Tutores).ThenInclude(t => t.Tutor)
         .Include(c => c.Vacunas).Include(c => c.Antipulgas).Include(c => c.Desparasitaciones).Include(c => c.Pesos)
         .AsSplitQuery();

    /// <summary>Descarga la ficha de un perro en Word.</summary>
    public async Task<IActionResult> FichaWord(int id)
    {
        var c = await ConDatosDeFicha(_db.Caninos).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        var bytes = Services.FichaWord.Generar(new[] { c }, _env.WebRootPath);
        return File(bytes, TipoWord, $"ficha-{NombreArchivo(c.Nombre)}.docx");
    }

    /// <summary>Descarga en un solo Word las fichas de los perros del listado (una por página), respetando los filtros.</summary>
    public async Task<IActionResult> FichasWord(string q, int? servicioId, string distrito, string sexo, string estado)
    {
        var ids = await Filtrar(q, servicioId, distrito, sexo, estado).Select(c => c.Id).ToListAsync();
        if (!ids.Any()) { TempData["Ok"] = "No hay perros con esos filtros para descargar."; return RedirectToAction(nameof(Index)); }
        var lista = await ConDatosDeFicha(_db.Caninos.Where(c => ids.Contains(c.Id))).OrderBy(c => c.Nombre).ToListAsync();
        var bytes = Services.FichaWord.Generar(lista, _env.WebRootPath);
        return File(bytes, TipoWord, $"vicdogs-fichas-caninos-{DateTime.Today:yyyyMMdd}.docx");
    }

    private static string NombreArchivo(string nombre)
    {
        var limpio = new string((nombre ?? "perro").Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-').ToLowerInvariant();
        return limpio.Length == 0 ? "perro" : limpio;
    }

    // ---------- Exportar a Excel ----------
    public async Task<IActionResult> Exportar(string q, int? servicioId, string distrito, string sexo, string estado)
    {
        var lista = await Filtrar(q, servicioId, distrito, sexo, estado).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Caninos");
        var cab = new[] { "Nombre", "Servicio", "Distrito", "Sexo", "Edad", "Raza o cruce", "Peso (kg)", "Tipo de baño",
                          "Alimentación", "Marca", "Tutores", "Vacunas", "Última desparasitación", "Último antipulgas", "Estado", "Instagram", "TikTok" };
        for (int i = 0; i < cab.Length; i++) ws.Cell(1, i + 1).Value = cab[i];

        int fila = 2;
        foreach (var c in lista)
        {
            var vacunas = string.Join("; ", c.Vacunas.OrderBy(v => v.Tipo).Select(v =>
                v.Tipo + (v.FechaAplicada != null ? " " + v.FechaAplicada.Value.ToString("dd/MM/yyyy")
                          : v.FechaProxima != null ? " (próxima " + v.FechaProxima.Value.ToString("dd/MM/yyyy") + ")" : "")));
            var desp = c.Desparasitaciones.OrderByDescending(d => d.Fecha).FirstOrDefault();
            var pulga = c.Antipulgas.OrderByDescending(a => a.Fecha).FirstOrDefault();

            ws.Cell(fila, 1).Value = c.Nombre;
            ws.Cell(fila, 2).Value = c.Servicio?.Nombre;
            ws.Cell(fila, 3).Value = c.Distrito;
            ws.Cell(fila, 4).Value = c.Sexo;
            ws.Cell(fila, 5).Value = c.EdadTexto;
            ws.Cell(fila, 6).Value = c.Raza;
            if (c.PesoKg != null) ws.Cell(fila, 7).Value = c.PesoKg.Value;
            ws.Cell(fila, 8).Value = c.TipoBano;
            ws.Cell(fila, 9).Value = c.AlimentacionTipo;
            ws.Cell(fila, 10).Value = c.AlimentacionMarca;
            ws.Cell(fila, 11).Value = string.Join(" / ", c.Tutores.Select(t => t.Tutor.Nombre));
            ws.Cell(fila, 12).Value = vacunas;
            ws.Cell(fila, 13).Value = desp?.Fecha.ToString("dd/MM/yyyy");
            ws.Cell(fila, 14).Value = pulga == null ? "" : ((pulga.Fecha?.ToString("dd/MM/yyyy") ?? "") + " " + pulga.Marca).Trim();
            ws.Cell(fila, 15).Value = c.Activo ? "Activo" : "Inactivo";
            ws.Cell(fila, 16).Value = Ayudas.Etiqueta(c.Instagram);
            ws.Cell(fila, 17).Value = Ayudas.Etiqueta(c.TikTok);
            fila++;
        }

        var enc = ws.Range(1, 1, 1, cab.Length);
        enc.Style.Font.Bold = true;
        enc.Style.Font.FontColor = XLColor.White;
        enc.Style.Fill.BackgroundColor = XLColor.FromHtml("#0432A8");
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"vicdogs-caninos-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // ---------- Apoyo ----------
    private async Task CargarFormularioAsync(int[] tutorSeleccionados)
    {
        ViewBag.Servicios = new SelectList(await _db.Servicios.OrderBy(s => s.Id).ToListAsync(), "Id", "Nombre");
        ViewBag.Tutores = new MultiSelectList(await _db.Tutores.OrderBy(t => t.Nombre).ToListAsync(), "Id", "Nombre", tutorSeleccionados);
        ViewBag.Distritos = await _db.Caninos.Where(c => c.Distrito != null && c.Distrito != "")
            .Select(c => c.Distrito).Distinct().OrderBy(d => d).ToListAsync();
    }

    private async Task AsignarTutoresAsync(int caninoId, int[] tutorIds, string nuevoTutor)
    {
        var ids = (tutorIds ?? new int[0]).ToList();

        if (!string.IsNullOrWhiteSpace(nuevoTutor))
        {
            var nombre = nuevoTutor.Trim();
            var existente = await _db.Tutores.FirstOrDefaultAsync(t => t.Nombre == nombre);
            if (existente == null)
            {
                existente = new Tutor { Nombre = nombre };
                _db.Tutores.Add(existente);
                await _db.SaveChangesAsync();
            }
            if (!ids.Contains(existente.Id)) ids.Add(existente.Id);
        }

        var actuales = await _db.CaninoTutores.Where(x => x.CaninoId == caninoId).ToListAsync();
        _db.CaninoTutores.RemoveRange(actuales.Where(x => !ids.Contains(x.TutorId)));
        foreach (var tid in ids.Where(i => actuales.All(x => x.TutorId != i)))
            _db.CaninoTutores.Add(new CaninoTutor { CaninoId = caninoId, TutorId = tid });
        await _db.SaveChangesAsync();
    }

    private void ValidarHospedaje(Canino c)
    {
        if (c.HospedajeDesde != null && c.HospedajeHasta != null && c.HospedajeHasta < c.HospedajeDesde)
            ModelState.AddModelError("HospedajeHasta", "La fecha de salida no puede ser anterior a la de llegada.");
    }

    private void ValidarFoto(IFormFile foto)
    {
        if (foto == null || foto.Length == 0) return;
        var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
        if (!ExtensionesFoto.Contains(ext))
            ModelState.AddModelError("foto", "La foto debe ser JPG, PNG o WEBP.");
        else if (foto.Length > 8 * 1024 * 1024)
            ModelState.AddModelError("foto", "La foto no debe pesar más de 8 MB.");
    }

    private async Task<string> GuardarFotoAsync(IFormFile foto)
    {
        if (foto == null || foto.Length == 0) return null;
        var carpeta = Path.Combine(_env.WebRootPath, "uploads", "caninos");
        Directory.CreateDirectory(carpeta);
        var nombre = Guid.NewGuid().ToString("N") + Path.GetExtension(foto.FileName).ToLowerInvariant();
        using var fs = new FileStream(Path.Combine(carpeta, nombre), FileMode.Create);
        await foto.CopyToAsync(fs);
        return "/uploads/caninos/" + nombre;
    }

    private void BorrarFoto(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/caninos/")) return;
        var ruta = Path.Combine(_env.WebRootPath, "uploads", "caninos", Path.GetFileName(url));
        if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
    }
}
