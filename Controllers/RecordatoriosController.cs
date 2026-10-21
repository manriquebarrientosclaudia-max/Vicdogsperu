using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;
using VicdogsPeru.Services;

namespace VicdogsPeru.Controllers;

public class CaninoOpcion
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int? TutorId { get; set; }
}

public class RecordatoriosController : Controller
{
    private readonly AppDbContext _db;
    public RecordatoriosController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var lista = await _db.Recordatorios.Include(r => r.Tutor).Include(r => r.Canino).Include(r => r.Empleado).ToListAsync();
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
    public async Task<IActionResult> Create(string tipo)
    {
        var r = tipo == Recordatorio.TipoCita
            ? new Recordatorio { Tipo = Recordatorio.TipoCita, Frecuencia = "UnaVez", AvisarDiasAntes = 1, AvisarWhatsApp = true, AvisarInstructorWhatsApp = true, Fecha = DateTime.Today.AddDays(1) }
            : new Recordatorio { DiaMes = DateTime.Today.Day };
        await CargarListasAsync();
        return View("Form", r);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Recordatorio r)
    {
        await ValidarAsync(r);
        if (!ModelState.IsValid) { await CargarListasAsync(); return View("Form", r); }
        r.Id = 0;
        r.HechoFecha = null;
        r.Aviso1DiaFecha = null; r.Aviso3HorasFecha = null;
        _db.Recordatorios.Add(r);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Recordatorio guardado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        if (r == null) return NotFound();
        await CargarListasAsync();
        return View("Form", r);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Recordatorio datos)
    {
        var r = await _db.Recordatorios.FindAsync(id);
        if (r == null) return NotFound();
        await ValidarAsync(datos);
        if (!ModelState.IsValid) { datos.Id = id; await CargarListasAsync(); return View("Form", datos); }

        // Si cambió cuándo ocurre, los avisos de WhatsApp vuelven a salir
        var cambioCuando = r.Frecuencia != datos.Frecuencia || r.DiaMes != datos.DiaMes || r.DiaSemana != datos.DiaSemana
                           || r.Fecha != datos.Fecha || r.Hora != datos.Hora;

        r.Titulo = datos.Titulo; r.Notas = datos.Notas; r.Frecuencia = datos.Frecuencia;
        r.DiaMes = datos.DiaMes; r.DiaSemana = datos.DiaSemana; r.Fecha = datos.Fecha;
        r.AvisarDiasAntes = datos.AvisarDiasAntes; r.Activo = datos.Activo;
        r.Tipo = datos.Tipo; r.Hora = datos.Hora; r.CaninoId = datos.CaninoId; r.TutorId = datos.TutorId; r.EmpleadoId = datos.EmpleadoId;
        r.AvisarWhatsApp = datos.AvisarWhatsApp; r.AvisarInstructorWhatsApp = datos.AvisarInstructorWhatsApp;
        r.HechoFecha = null;   // si cambió la fecha, vuelve a avisar
        if (cambioCuando) { r.Aviso1DiaFecha = null; r.Aviso3HorasFecha = null; }
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

    // ---------- WhatsApp ----------

    /// <summary>
    /// Lo llama un servicio externo (por ejemplo cron-job.org) cada 5 minutos: despierta la web gratuita de Render
    /// y envía los avisos que toquen. Se protege con la clave WhatsApp__ClaveCron, no con inicio de sesión.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Procesar(string clave, [FromServices] AvisosWhatsApp avisos, [FromServices] IConfiguration config)
    {
        var esperada = config["WhatsApp:ClaveCron"];
        if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(clave) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(clave), Encoding.UTF8.GetBytes(esperada)))
            return NotFound();

        var enviados = await avisos.ProcesarAsync(DateTime.Now, HttpContext.RequestAborted);
        return Ok(new { ok = true, enviados });
    }

    /// <summary>Manda un mensaje de prueba a tu WhatsApp para comprobar que todo está bien configurado.</summary>
    [HttpPost]
    public async Task<IActionResult> ProbarWhatsApp([FromServices] IWhatsAppSender wa, [FromServices] IConfiguration config)
    {
        var tel = Ayudas.TelefonoWhatsApp(config["WhatsApp:TelefonoAdmin"]);
        if (!wa.Configurado)
            TempData["Error"] = "WhatsApp todavía no está configurado (faltan WhatsApp__Token y WhatsApp__PhoneNumberId).";
        else if (tel == null)
            TempData["Error"] = "Falta tu número: configura WhatsApp__TelefonoAdmin (ej. 51987654321).";
        else if (await wa.EnviarAsync(tel, "✅ Prueba de Vicdogs Perú: los avisos por WhatsApp funcionan.", HttpContext.RequestAborted))
            TempData["Ok"] = "Mensaje de prueba enviado a tu WhatsApp.";
        else
            TempData["Error"] = "WhatsApp rechazó el mensaje. Revisa el token, el número y la plantilla (mira el log del servidor).";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarListasAsync()
    {
        var tutores = await _db.Tutores.OrderBy(t => t.Nombre).ToListAsync();
        ViewBag.Tutores = tutores.Select(t => new SelectListItem(t.Nombre, t.Id.ToString())).ToList();

        var instructores = await _db.Empleados.Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Instructores = instructores.Select(e => new SelectListItem(
            (string.IsNullOrWhiteSpace(e.Cargo) ? e.Nombre : e.Nombre + " (" + e.Cargo + ")") +
            (string.IsNullOrWhiteSpace(e.Telefono) ? " · sin teléfono" : " · " + e.Telefono),
            e.Id.ToString())).ToList();

        var caninos = await _db.Caninos.Where(c => c.Activo).Include(c => c.Tutores).ThenInclude(x => x.Tutor)
            .OrderBy(c => c.Nombre).ToListAsync();
        // Para elegir el tutor automáticamente al escoger el perro: el primero que tenga teléfono
        ViewBag.Caninos = caninos.Select(c => new CaninoOpcion
        {
            Id = c.Id, Nombre = c.Nombre,
            TutorId = (c.Tutores.Select(x => x.Tutor).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Telefono))
                       ?? c.Tutores.Select(x => x.Tutor).FirstOrDefault())?.Id
        }).ToList();
    }

    private async Task ValidarAsync(Recordatorio r)
    {
        if (r.Tipo != Recordatorio.TipoCita) r.Tipo = Recordatorio.TipoPago;
        Validar(r);

        if (r.EsCita)
        {
            if (r.Hora == null) ModelState.AddModelError("Hora", "Indica la hora de la cita.");
        }
        else
        {
            // Un pago no lleva perro ni tutor ni aviso al tutor
            r.CaninoId = null; r.TutorId = null; r.EmpleadoId = null; r.AvisarInstructorWhatsApp = false;
        }
        if (r.Hora.HasValue && (r.Hora < TimeSpan.Zero || r.Hora >= TimeSpan.FromDays(1)))
            ModelState.AddModelError("Hora", "La hora no es válida.");
        if (r.Hora.HasValue) r.Hora = new TimeSpan(r.Hora.Value.Hours, r.Hora.Value.Minutes, 0);

        if (r.CaninoId != null && !await _db.Caninos.AnyAsync(c => c.Id == r.CaninoId)) r.CaninoId = null;
        if (r.TutorId != null && !await _db.Tutores.AnyAsync(t => t.Id == r.TutorId)) r.TutorId = null;
        if (r.EmpleadoId != null && !await _db.Empleados.AnyAsync(e => e.Id == r.EmpleadoId)) r.EmpleadoId = null;

        if (r.EsCita && r.EmpleadoId == null)
            ModelState.AddModelError("EmpleadoId", "Elige el instructor que dará la clase.");

        if (r.AvisarInstructorWhatsApp)
        {
            var instructor = r.EmpleadoId == null ? null : await _db.Empleados.FindAsync(r.EmpleadoId);
            if (instructor != null && Ayudas.TelefonoWhatsApp(instructor.Telefono) == null)
                ModelState.AddModelError("EmpleadoId", $"{instructor.Nombre} no tiene un teléfono válido. Agrégalo en Personal.");
        }
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
