using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

public class AlertaItem
{
    public Canino Canino { get; set; }
    public string Detalle { get; set; }
    public DateTime Fecha { get; set; }
    public int Dias { get; set; }          // negativo = vencida
}

public class AlertaRecordatorio
{
    public Recordatorio Recordatorio { get; set; }
    public DateTime Fecha { get; set; }
    public int Dias { get; set; }          // negativo = ya pasó y no se marcó como hecho
}

public class CumpleItem
{
    public string Nombre { get; set; }
    public string Detalle { get; set; }
    public bool EsPerro { get; set; }
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int Dias { get; set; }
}

public class DashboardVM
{
    public List<CumpleItem> Cumpleanos { get; set; } = new();
    public List<AlertaRecordatorio> Recordatorios { get; set; } = new();
    public int TotalActivos { get; set; }
    public int TotalTutores { get; set; }
    public List<(string Servicio, int Cantidad)> PorServicio { get; set; } = new();
    public List<(string Distrito, int Cantidad)> PorDistrito { get; set; } = new();
    public List<AlertaItem> Alertas { get; set; } = new();
    public int SinTutor { get; set; }
    public int SinPeso { get; set; }
    public int SinFechaVacuna { get; set; }
}

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var hoy = DateTime.Today;
        var caninos = await _db.Caninos.Where(c => c.Activo)
            .Include(c => c.Servicio).Include(c => c.Tutores)
            .Include(c => c.Vacunas).Include(c => c.Desparasitaciones)
            .ToListAsync();

        var vm = new DashboardVM
        {
            TotalActivos = caninos.Count,
            TotalTutores = await _db.Tutores.CountAsync(),
            PorServicio = caninos.GroupBy(c => c.Servicio?.Nombre ?? "Sin servicio")
                .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList(),
            PorDistrito = caninos.GroupBy(c => string.IsNullOrWhiteSpace(c.Distrito) ? "Sin distrito" : c.Distrito)
                .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Item1).ToList(),
            SinTutor = caninos.Count(c => !c.Tutores.Any()),
            SinPeso = caninos.Count(c => c.PesoKg == null),
            SinFechaVacuna = caninos.Count(c => !c.Vacunas.Any(v => v.Vencimiento != null))
        };

        foreach (var c in caninos)
        {
            // Vacunas: por cada tipo se toma el vencimiento más lejano (la vacuna más reciente)
            foreach (var g in c.Vacunas.Where(v => v.Vencimiento != null).GroupBy(v => v.Tipo))
            {
                var venc = g.Max(v => v.Vencimiento.Value);
                var dias = (venc.Date - hoy).Days;
                if (dias <= 30)
                    vm.Alertas.Add(new AlertaItem { Canino = c, Detalle = "Vacuna " + g.Key, Fecha = venc, Dias = dias });
            }
            // Desparasitación: se repite cada 3 meses
            if (c.Desparasitaciones.Any())
            {
                var prox = c.Desparasitaciones.Max(d => d.Fecha).AddMonths(3);
                var dias = (prox.Date - hoy).Days;
                if (dias <= 14)
                    vm.Alertas.Add(new AlertaItem { Canino = c, Detalle = "Desparasitación", Fecha = prox, Dias = dias });
            }
        }
        vm.Alertas = vm.Alertas.OrderBy(a => a.Dias).ThenBy(a => a.Canino.Nombre).ToList();

        // Recordatorios: avisan desde N días antes y siguen apareciendo hasta marcarlos como hechos (7 días)
        var recordatorios = await _db.Recordatorios.Where(r => r.Activo).ToListAsync();
        foreach (var r in recordatorios)
        {
            var ant = r.Anterior(hoy);
            var ventana = r.Frecuencia == "Semanal" ? 3 : 7;
            if (ant.HasValue && (hoy - ant.Value).Days <= ventana && r.HechoFecha?.Date != ant.Value)
                vm.Recordatorios.Add(new AlertaRecordatorio { Recordatorio = r, Fecha = ant.Value, Dias = (ant.Value - hoy).Days });

            var prox = r.Proxima(hoy);
            if (prox.HasValue && r.HechoFecha?.Date != prox.Value)
            {
                var dias = (prox.Value - hoy).Days;
                if (dias <= r.AvisarDiasAntes)
                    vm.Recordatorios.Add(new AlertaRecordatorio { Recordatorio = r, Fecha = prox.Value, Dias = dias });
            }
        }
        // Cumpleaños de los próximos 14 días (tutores y perros con fecha de nacimiento)
        var conCumple = await _db.Tutores.Where(t => t.Cumpleanos != null)
            .Include(t => t.Caninos).ThenInclude(x => x.Canino).ToListAsync();
        foreach (var t in conCumple)
        {
            var f = t.Cumpleanos.Value.ProximoCumple(hoy);
            var dias = (f - hoy).Days;
            if (dias <= 14)
                vm.Cumpleanos.Add(new CumpleItem
                {
                    Nombre = t.Nombre, Id = t.Id, Fecha = f, Dias = dias, EsPerro = false,
                    Detalle = t.Caninos.Any() ? "Tutor de " + string.Join(", ", t.Caninos.Select(x => x.Canino.Nombre)) : "Tutor"
                });
        }
        foreach (var c in caninos.Where(c => c.FechaNacimiento != null))
        {
            var f = c.FechaNacimiento.Value.ProximoCumple(hoy);
            var dias = (f - hoy).Days;
            if (dias <= 14)
                vm.Cumpleanos.Add(new CumpleItem { Nombre = c.Nombre, Id = c.Id, Fecha = f, Dias = dias, EsPerro = true, Detalle = "Perro · " + c.Servicio?.Nombre });
        }
        vm.Cumpleanos = vm.Cumpleanos.OrderBy(x => x.Dias).ThenBy(x => x.Nombre).ToList();

        vm.Recordatorios = vm.Recordatorios.OrderBy(a => a.Dias).ThenBy(a => a.Recordatorio.Titulo).ToList();

        return View(vm);
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Error() => View();
}
