using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

public class RecordatoriosController : Controller
{
    private readonly AppDbContext _db;
    public RecordatoriosController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var lista = await _db.Recordatorios.ToListAsync();
        var hoy = DateTime.Today;
        // Primero los activos más cercanos; al final los inactivos o ya vencidos
        var ordenada = lista
            .OrderBy(r => !r.Activo)
            .ThenBy(r => r.Proxima(hoy) ?? DateTime.MaxValue)
            .ThenBy(r => r.Titulo)
            .ToList();
        return View(ordenada);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new Recordatorio { DiaMes = DateTime.Today.Day });

    [HttpPost]
    public async Task<IActionResult> Create(Recordatorio r)
    {
        Validar(r);
        if (!ModelState.IsValid) return View("Form", r);
        r.Id = 0;
        r.HechoFecha = null;
        _db.Recordatorios.Add(r);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Recordatorio guardado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        return r == null ? NotFound() : View("Form", r);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Recordatorio datos)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        if (r == null) return NotFound();
        Validar(datos);
        if (!ModelState.IsValid) { datos.Id = id; return View("Form", datos); }

        r.Titulo = datos.Titulo; r.Notas = datos.Notas; r.Frecuencia = datos.Frecuencia;
        r.DiaMes = datos.DiaMes; r.DiaSemana = datos.DiaSemana; r.Fecha = datos.Fecha;
        r.AvisarDiasAntes = datos.AvisarDiasAntes; r.Activo = datos.Activo;
        r.HechoFecha = null;   // si cambió la fecha, vuelve a avisar
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Recordatorio actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        if (r != null)
        {
            _db.Recordatorios.Remove(r);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Recordatorio eliminado.";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Marca una fecha como hecha (por ejemplo, ya pagué el internet).</summary>
    [HttpPost]
    public async Task<IActionResult> Hecho(int id, DateTime fecha, string volver)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        if (r != null)
        {
            r.HechoFecha = fecha.Date;
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"«{r.Titulo}» marcado como hecho.";
        }
        return volver == "lista" ? RedirectToAction(nameof(Index)) : RedirectToAction("Index", "Home");
    }

    private void Validar(Recordatorio r)
    {
        var validas = new[] { "Mensual", "Semanal", "Anual", "UnaVez" };
        if (!validas.Contains(r.Frecuencia)) { ModelState.AddModelError("Frecuencia", "Elige cada cuánto se repite."); return; }

        // Quita los datos que no corresponden a la frecuencia elegida
        if (r.Frecuencia != "Mensual") { r.DiaMes = null; ModelState.Remove("DiaMes"); }
        if (r.Frecuencia != "Semanal") r.DiaSemana = null;
        if (r.Frecuencia != "Anual" && r.Frecuencia != "UnaVez") r.Fecha = null;

        if (r.Frecuencia == "Mensual" && r.DiaMes == null)
            ModelState.AddModelError("DiaMes", "Indica qué día del mes.");
        if (r.Frecuencia == "Semanal" && (r.DiaSemana == null || r.DiaSemana < 0 || r.DiaSemana > 6))
            ModelState.AddModelError("DiaSemana", "Elige el día de la semana.");
        if ((r.Frecuencia == "Anual" || r.Frecuencia == "UnaVez") && r.Fecha == null)
            ModelState.AddModelError("Fecha", "Elige la fecha.");
    }
}
