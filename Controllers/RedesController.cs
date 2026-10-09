using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

/// <summary>Perros que tienen Instagram o TikTok, para abrir sus perfiles y copiar sus etiquetas.</summary>
public class RedesController : Controller
{
    private readonly AppDbContext _db;
    public RedesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string q)
    {
        var query = _db.Caninos.Include(c => c.Servicio)
            .Where(c => c.Activo && ((c.Instagram != null && c.Instagram != "") || (c.TikTok != null && c.TikTok != "")));
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var pat = Busqueda.Contiene(q);
            query = query.Where(c => EF.Functions.ILike(c.Nombre, pat) || EF.Functions.ILike(c.Instagram, pat) || EF.Functions.ILike(c.TikTok, pat));
        }
        ViewBag.Q = q;
        ViewBag.TotalCaninos = await _db.Caninos.CountAsync(c => c.Activo);
        return View(await query.OrderBy(c => c.Nombre).ToListAsync());
    }
}
