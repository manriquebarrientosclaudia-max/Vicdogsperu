using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

public class TutoresController : Controller
{
    private readonly AppDbContext _db;
    public TutoresController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string q)
    {
        // Se incluyen también los demás tutores de cada perro, para mostrarlos juntos
        var query = _db.Tutores
            .Include(t => t.Caninos).ThenInclude(x => x.Canino).ThenInclude(c => c.Tutores).ThenInclude(ct => ct.Tutor)
            .AsSplitQuery().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var pat = Busqueda.Contiene(q);
            query = query.Where(t => EF.Functions.ILike(t.Nombre, pat) || EF.Functions.ILike(t.Telefono, pat) ||
                                     t.Caninos.Any(x => EF.Functions.ILike(x.Canino.Nombre, pat)));
        }
        ViewBag.Q = q;
        return View(await query.OrderBy(t => t.Nombre).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await _db.Tutores
            .Include(x => x.Caninos).ThenInclude(c => c.Canino).ThenInclude(c => c.Servicio)
            .Include(x => x.Caninos).ThenInclude(c => c.Canino).ThenInclude(c => c.Tutores).ThenInclude(ct => ct.Tutor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? NotFound() : View(t);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new Tutor());

    [HttpPost]
    public async Task<IActionResult> Create(Tutor tutor)
    {
        if (!ModelState.IsValid) return View("Form", tutor);
        tutor.Id = 0;
        LimpiarRedes(tutor);
        _db.Tutores.Add(tutor);
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"{tutor.Nombre} fue registrado.";
        return RedirectToAction(nameof(Details), new { id = tutor.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await _db.Tutores.FindAsync(id);
        return t == null ? NotFound() : View("Form", t);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Tutor tutor)
    {
        var t = await _db.Tutores.FindAsync(id);
        if (t == null) return NotFound();
        if (!ModelState.IsValid) { tutor.Id = id; return View("Form", tutor); }

        t.Nombre = tutor.Nombre; t.Telefono = tutor.Telefono; t.Cumpleanos = tutor.Cumpleanos;
        t.Direccion = tutor.Direccion; t.Notas = tutor.Notas;
        t.Instagram = Limpiar(tutor.Instagram); t.TikTok = Limpiar(tutor.TikTok); t.Facebook = Limpiar(tutor.Facebook);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Los datos del tutor se guardaron.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string Limpiar(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static void LimpiarRedes(Tutor t)
    {
        t.Instagram = Limpiar(t.Instagram); t.TikTok = Limpiar(t.TikTok); t.Facebook = Limpiar(t.Facebook);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.Tutores.Include(x => x.Caninos).ThenInclude(c => c.Canino).FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? NotFound() : View(t);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await _db.Tutores.FindAsync(id);
        if (t != null)
        {
            _db.Tutores.Remove(t);   // solo se quita la relación; los perros no se borran
            await _db.SaveChangesAsync();
            TempData["Ok"] = $"{t.Nombre} fue eliminado. Sus perros siguen registrados.";
        }
        return RedirectToAction(nameof(Index));
    }
}
