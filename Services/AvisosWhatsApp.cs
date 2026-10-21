using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Services;

/// <summary>
/// Revisa los recordatorios y manda por WhatsApp el aviso de «1 día antes» y el de «3 horas antes».
/// Es seguro llamarlo muchas veces: cada aviso se marca como enviado (por fecha de ocurrencia) y no se repite.
/// </summary>
public class AvisosWhatsApp
{
    private static readonly SemaphoreSlim Candado = new(1, 1);   // evita duplicados si el temporizador y el ping externo coinciden

    private readonly AppDbContext _db;
    private readonly IWhatsAppSender _wa;
    private readonly IConfiguration _config;
    private readonly ILogger<AvisosWhatsApp> _log;

    public AvisosWhatsApp(AppDbContext db, IWhatsAppSender wa, IConfiguration config, ILogger<AvisosWhatsApp> log)
    {
        _db = db; _wa = wa; _config = config; _log = log;
    }

    /// <summary>Devuelve cuántos mensajes se enviaron.</summary>
    public async Task<int> ProcesarAsync(DateTime ahora, CancellationToken ct = default)
    {
        await Candado.WaitAsync(ct);
        try
        {
            var enviados = 0;
            var lista = await _db.Recordatorios
                .Include(r => r.Tutor).Include(r => r.Canino).Include(r => r.Empleado)
                .Where(r => r.Activo && (r.AvisarWhatsApp || r.AvisarInstructorWhatsApp))
                .ToListAsync(ct);

            foreach (var r in lista)
            {
                var ocurrencia = r.ProximaOcurrencia(ahora);
                if (ocurrencia == null) continue;
                var dia = ocurrencia.Value.Date;
                if (r.HechoFecha?.Date == dia) continue;   // ya lo marcaron como hecho

                var falta = ocurrencia.Value - ahora;
                var con3Horas = r.Hora.HasValue;

                // Aviso de 3 horas antes (solo si tiene hora)
                if (con3Horas && falta <= TimeSpan.FromHours(3) && r.Aviso3HorasFecha?.Date != dia)
                {
                    var n = await EnviarAsync(r, ocurrencia.Value, ahora, tresHoras: true, ct);
                    if (n.HasValue) { r.Aviso3HorasFecha = dia; r.Aviso1DiaFecha ??= dia; enviados += n.Value; }
                }
                // Aviso de 1 día antes. Si ya estamos dentro de las 3 horas, ese aviso ya no tiene sentido
                else if (falta <= TimeSpan.FromHours(24) && (!con3Horas || falta > TimeSpan.FromHours(3)) && r.Aviso1DiaFecha?.Date != dia)
                {
                    var n = await EnviarAsync(r, ocurrencia.Value, ahora, tresHoras: false, ct);
                    if (n.HasValue) { r.Aviso1DiaFecha = dia; enviados += n.Value; }
                }
            }

            await _db.SaveChangesAsync(ct);
            return enviados;
        }
        finally { Candado.Release(); }
    }

    /// <summary>Envía el aviso a quien corresponda. Devuelve null si no se pudo enviar a nadie (se reintenta luego).</summary>
    private async Task<int?> EnviarAsync(Recordatorio r, DateTime ocurrencia, DateTime ahora, bool tresHoras, CancellationToken ct)
    {
        if (!_wa.Configurado)
        {
            _log.LogWarning("WhatsApp sin configurar: no se envió el aviso de «{Titulo}».", r.Titulo);
            return null;
        }

        var destinos = new List<(string Telefono, string Mensaje)>();
        if (r.AvisarWhatsApp)
        {
            var admin = Ayudas.TelefonoWhatsApp(_config["WhatsApp:TelefonoAdmin"]);
            if (admin != null) destinos.Add((admin, MensajeInterno(r, ocurrencia, ahora, tresHoras)));
            else _log.LogWarning("Falta WhatsApp__TelefonoAdmin: no hay a quién avisar de «{Titulo}».", r.Titulo);
        }
        if (r.AvisarInstructorWhatsApp)
        {
            var instructor = Ayudas.TelefonoWhatsApp(r.Empleado?.Telefono);
            if (instructor != null) destinos.Add((instructor, MensajeInstructor(r, ocurrencia, ahora, tresHoras)));
            else _log.LogWarning("«{Titulo}»: el instructor no está asignado o no tiene un teléfono válido.", r.Titulo);
        }
        // Un mismo número no recibe dos veces el mismo recordatorio
        destinos = destinos.GroupBy(d => d.Telefono).Select(g => g.First()).ToList();
        if (destinos.Count == 0) return null;

        var ok = 0;
        foreach (var (tel, msg) in destinos)
            if (await _wa.EnviarAsync(tel, msg, ct)) ok++;
        return ok > 0 ? ok : null;
    }

    private static string Dia(DateTime ocurrencia, DateTime ahora)
    {
        var dif = (ocurrencia.Date - ahora.Date).Days;
        return dif == 0 ? "hoy" : dif == 1 ? "mañana" : "el " + ocurrencia.ToString("dd/MM");
    }

    private static string Hora(Recordatorio r) => r.HoraTexto;

    /// <summary>Mensaje para ti (el dueño o administrador).</summary>
    private static string MensajeInterno(Recordatorio r, DateTime ocurrencia, DateTime ahora, bool tresHoras)
    {
        var quien = r.Canino != null ? $" · {r.Canino.Nombre}" : "";
        if (r.Tutor != null) quien += r.Canino != null ? $" ({r.Tutor.Nombre})" : $" · {r.Tutor.Nombre}";
        if (r.Empleado != null) quien += $" · Instructor: {r.Empleado.Nombre}";
        var notas = string.IsNullOrWhiteSpace(r.Notas) ? "" : $" · {r.Notas}";
        var icono = r.EsCita ? "📅" : "💳";

        if (r.Hora.HasValue)
        {
            var cuando = tresHoras ? $"⏰ En 3 horas ({Hora(r)})" : $"{icono} {Dia(ocurrencia, ahora)} a las {Hora(r)}";
            return $"{cuando}: {r.Titulo}{quien}{notas}";
        }
        return $"{icono} {char.ToUpper(Dia(ocurrencia, ahora)[0])}{Dia(ocurrencia, ahora)[1..]}: {r.Titulo}{notas}";
    }

    /// <summary>Mensaje para el instructor que dará la clase: qué perro, de quién, a qué hora y dónde.</summary>
    private static string MensajeInstructor(Recordatorio r, DateTime ocurrencia, DateTime ahora, bool tresHoras)
    {
        var nombre = (r.Empleado?.Nombre ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var saludo = nombre != null ? $"Hola {nombre}" : "Hola";
        var cuando = tresHoras ? $"hoy a las {Hora(r)} (en 3 horas)" : $"{Dia(ocurrencia, ahora)} a las {Hora(r)}";

        var sb = new System.Text.StringBuilder($"{saludo}, tienes clase {cuando}: {r.Titulo}");
        if (r.Canino != null) sb.Append($" · Perro: {r.Canino.Nombre}");
        if (r.Tutor != null)
        {
            sb.Append($" · Tutor: {r.Tutor.Nombre}");
            if (!string.IsNullOrWhiteSpace(r.Tutor.Telefono)) sb.Append($" ({r.Tutor.Telefono.Trim()})");
            if (!string.IsNullOrWhiteSpace(r.Tutor.Direccion)) sb.Append($" · Dirección: {r.Tutor.Direccion.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(r.Notas)) sb.Append($" · Nota: {r.Notas.Trim()}");
        return sb.ToString();
    }
}

/// <summary>Despierta cada minuto a <see cref="AvisosWhatsApp"/> mientras la aplicación está encendida.</summary>
public class AvisosWhatsAppFondo : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AvisosWhatsAppFondo> _log;

    public AvisosWhatsAppFondo(IServiceScopeFactory scopes, ILogger<AvisosWhatsAppFondo> log)
    {
        _scopes = scopes; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var avisos = scope.ServiceProvider.GetRequiredService<AvisosWhatsApp>();
                await avisos.ProcesarAsync(DateTime.Now, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Error al revisar los avisos de WhatsApp"); }

            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
