using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

public class FilaAsistencia
{
    public int EmpleadoId { get; set; }
    public string Estado { get; set; }   // vacío = sin registrar
    public TimeSpan? HoraEntrada { get; set; }
    public TimeSpan? HoraSalida { get; set; }
    public string Nota { get; set; }
}

public class AsistenciaDiaVM
{
    public DateTime Fecha { get; set; }
    public List<(Empleado Empleado, Asistencia Registro)> Filas { get; set; } = new();
    public int Presentes => Filas.Count(f => f.Registro?.Estado == Asistencia.Presente);
    public int Faltas => Filas.Count(f => f.Registro?.Estado == Asistencia.Falta);
    public int Permisos => Filas.Count(f => f.Registro?.Estado == Asistencia.Permiso);
    public int SinRegistrar => Filas.Count(f => f.Registro == null);
}

public class FilaResumen
{
    public Empleado Empleado { get; set; }
    public Dictionary<int, Asistencia> PorDia { get; set; } = new();
    public int Presentes => PorDia.Values.Count(a => a.Estado == Asistencia.Presente);
    public int Faltas => PorDia.Values.Count(a => a.Estado == Asistencia.Falta);
    public int Permisos => PorDia.Values.Count(a => a.Estado == Asistencia.Permiso);
    public int Registrados => PorDia.Count;
    public double Horas => PorDia.Values.Sum(a => a.Horas ?? 0);
    /// <summary>Porcentaje de días en que asistió, sobre los días con marca.</summary>
    public int? Porcentaje => Registrados == 0 ? null : (int)Math.Round(100.0 * Presentes / Registrados);
}

public class AsistenciaMesVM
{
    public DateTime Mes { get; set; }          // primer día del mes
    public List<FilaResumen> Filas { get; set; } = new();
    public int DiasEnMes => DateTime.DaysInMonth(Mes.Year, Mes.Month);
}

public class AsistenciasController : Controller
{
    private readonly AppDbContext _db;
    public AsistenciasController(AppDbContext db) => _db = db;

    // ---------- Lista del día ----------
    public async Task<IActionResult> Index(DateTime? fecha)
    {
        var dia = (fecha ?? DateTime.Today).Date;
        var personal = await _db.Empleados.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync();
        var ids = personal.Select(e => e.Id).ToList();
        var registros = await _db.Asistencias
            .Where(a => a.Fecha == dia && ids.Contains(a.EmpleadoId))
            .ToDictionaryAsync(a => a.EmpleadoId);

        return View(new AsistenciaDiaVM
        {
            Fecha = dia,
            Filas = personal.Select(e => (e, registros.GetValueOrDefault(e.Id))).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Guardar(DateTime fecha, List<FilaAsistencia> filas)
    {
        var dia = fecha.Date;
        filas ??= new List<FilaAsistencia>();
        var ids = filas.Select(f => f.EmpleadoId).Distinct().ToList();

        var existentes = await _db.Asistencias
            .Where(a => a.Fecha == dia && ids.Contains(a.EmpleadoId))
            .ToDictionaryAsync(a => a.EmpleadoId);
        var validos = (await _db.Empleados.Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync()).ToHashSet();

        int guardados = 0;
        foreach (var f in filas)
        {
            if (!validos.Contains(f.EmpleadoId)) continue;
            existentes.TryGetValue(f.EmpleadoId, out var reg);

            if (string.IsNullOrEmpty(f.Estado) || !Asistencia.Estados.Contains(f.Estado))
            {
                if (reg != null) _db.Asistencias.Remove(reg);   // se quitó la marca
                continue;
            }

            var nota = string.IsNullOrWhiteSpace(f.Nota) ? null : f.Nota.Trim();
            if (nota != null && nota.Length > 200) nota = nota[..200];

            // Las horas solo tienen sentido si la persona asistió
            var entrada = f.Estado == Asistencia.Presente ? f.HoraEntrada : null;
            var salida = f.Estado == Asistencia.Presente ? f.HoraSalida : null;

            if (reg == null)
                _db.Asistencias.Add(new Asistencia { EmpleadoId = f.EmpleadoId, Fecha = dia, Estado = f.Estado, HoraEntrada = entrada, HoraSalida = salida, Nota = nota });
            else
            {
                reg.Estado = f.Estado; reg.HoraEntrada = entrada; reg.HoraSalida = salida; reg.Nota = nota;
            }
            guardados++;
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Asistencia del {dia:dd/MM/yyyy} guardada ({guardados} {(guardados == 1 ? "persona" : "personas")}).";
        return RedirectToAction(nameof(Index), new { fecha = dia.ToString("yyyy-MM-dd") });
    }

    // ---------- Resumen mensual ----------
    private static DateTime ParsearMes(string mes)
    {
        if (!string.IsNullOrEmpty(mes) &&
            DateTime.TryParseExact(mes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m))
            return new DateTime(m.Year, m.Month, 1);
        var hoy = DateTime.Today;
        return new DateTime(hoy.Year, hoy.Month, 1);
    }

    private async Task<AsistenciaMesVM> ArmarMesAsync(string mes)
    {
        var inicio = ParsearMes(mes);
        var fin = inicio.AddMonths(1);

        var registros = await _db.Asistencias.Where(a => a.Fecha >= inicio && a.Fecha < fin).ToListAsync();

        // Personal activo, más quienes tengan marcas en el mes aunque ya no trabajen
        var conRegistro = registros.Select(r => r.EmpleadoId).Distinct().ToList();
        var personal = await _db.Empleados.Where(e => e.Activo || conRegistro.Contains(e.Id)).OrderBy(e => e.Nombre).ToListAsync();

        var vm = new AsistenciaMesVM { Mes = inicio };
        var porPersona = registros.GroupBy(r => r.EmpleadoId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var e in personal)
        {
            var fila = new FilaResumen { Empleado = e };
            if (porPersona.TryGetValue(e.Id, out var regs))
                foreach (var r in regs) fila.PorDia[r.Fecha.Day] = r;
            vm.Filas.Add(fila);
        }
        return vm;
    }

    public async Task<IActionResult> Resumen(string mes) => View(await ArmarMesAsync(mes));

    public async Task<IActionResult> ExportarMes(string mes)
    {
        var vm = await ArmarMesAsync(mes);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Asistencia del personal");

        var cab = new List<string> { "Nombre", "Cargo" };
        for (int d = 1; d <= vm.DiasEnMes; d++) cab.Add(d.ToString());
        cab.AddRange(new[] { "Asistió", "Faltas", "Permisos", "% asistencia", "Horas trabajadas" });
        for (int i = 0; i < cab.Count; i++) ws.Cell(1, i + 1).Value = cab[i];

        int fila = 2;
        foreach (var f in vm.Filas)
        {
            ws.Cell(fila, 1).Value = f.Empleado.Nombre;
            ws.Cell(fila, 2).Value = f.Empleado.Cargo;
            for (int d = 1; d <= vm.DiasEnMes; d++)
                if (f.PorDia.TryGetValue(d, out var a))
                    ws.Cell(fila, 2 + d).Value = Asistencia.Letra(a.Estado);
            int c0 = 2 + vm.DiasEnMes;
            ws.Cell(fila, c0 + 1).Value = f.Presentes;
            ws.Cell(fila, c0 + 2).Value = f.Faltas;
            ws.Cell(fila, c0 + 3).Value = f.Permisos;
            if (f.Porcentaje != null) ws.Cell(fila, c0 + 4).Value = f.Porcentaje.Value / 100.0;
            ws.Cell(fila, c0 + 4).Style.NumberFormat.Format = "0%";
            ws.Cell(fila, c0 + 5).Value = Math.Round(f.Horas, 1);
            fila++;
        }

        ws.Cell(fila + 1, 1).Value = "A = asistió · F = faltó · P = permiso";
        var encabezado = ws.Range(1, 1, 1, cab.Count);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0432A8");
        ws.Range(1, 3, Math.Max(fila - 1, 1), 2 + vm.DiasEnMes).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"asistencia-personal-{vm.Mes:yyyy-MM}.xlsx");
    }
}
