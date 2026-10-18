using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

/// <summary>Personas que trabajan en Vicdogs (a quienes se les toma asistencia).</summary>
public class PersonalController : Controller
{
    private readonly AppDbContext _db;
    public PersonalController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Empleados.OrderByDescending(e => e.Activo).ThenBy(e => e.Nombre).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Form", new Empleado());

    [HttpPost]
    public async Task<IActionResult> Create(Empleado e)
    {
        if (!ModelState.IsValid) return View("Form", e);
        e.Id = 0;
        e.Nombre = e.Nombre.Trim();
        _db.Empleados.Add(e);
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"{e.Nombre} fue agregado al personal.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Empleados.FindAsync(id);
        return e == null ? NotFound() : View("Form", e);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Empleado datos)
    {
        var e = await _db.Empleados.FindAsync(id);
        if (e == null) return NotFound();
        if (!ModelState.IsValid) { datos.Id = id; return View("Form", datos); }
        e.Nombre = datos.Nombre.Trim(); e.Cargo = datos.Cargo; e.Telefono = datos.Telefono; e.Activo = datos.Activo;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Datos actualizados.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Elimina a la persona y todas sus asistencias.</summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Empleados.FindAsync(id);
        if (e != null)
        {
            _db.Empleados.Remove(e);
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"{e.Nombre} fue eliminado junto con sus asistencias.";
        }
        return RedirectToAction(nameof(Index));
    }
}
