using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VicdogsPeru.Models;

/// <summary>Fecha que no se debe olvidar: pagos, trámites, citas, etc.</summary>
public class Recordatorio
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Escribe qué debes recordar.")]
    [StringLength(120)]
    [Display(Name = "¿Qué no debes olvidar?")]
    public string Titulo { get; set; }

    [StringLength(300)]
    public string Notas { get; set; }

    /// <summary>Mensual, Semanal, Anual o UnaVez.</summary>
    [Required, StringLength(10)]
    [Display(Name = "¿Cada cuánto?")]
    public string Frecuencia { get; set; } = "Mensual";

    [Range(1, 31, ErrorMessage = "El día debe estar entre 1 y 31.")]
    [Display(Name = "Día del mes")]
    public int? DiaMes { get; set; }

    /// <summary>0 = domingo ... 6 = sábado.</summary>
    [Display(Name = "Día de la semana")]
    public int? DiaSemana { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    [Range(0, 30, ErrorMessage = "Los días de aviso deben estar entre 0 y 30.")]
    [Display(Name = "Avisarme")]
    public int AvisarDiasAntes { get; set; } = 1;

    public bool Activo { get; set; } = true;

    /// <summary>Fecha de la ocurrencia que ya se marcó como hecha.</summary>
    public DateTime? HechoFecha { get; set; }

    // ---------- Citas y avisos por WhatsApp ----------

    public const string TipoPago = "Pago";
    public const string TipoCita = "Cita";

    /// <summary>Pago (servicios, trámites…) o Cita (con hora, perro y tutor).</summary>
    [Required, StringLength(10)]
    [Display(Name = "Tipo")]
    public string Tipo { get; set; } = TipoPago;

    /// <summary>Hora de la cita (obligatoria en las citas). Con hora, los avisos salen 1 día y 3 horas antes.</summary>
    [Display(Name = "Hora")]
    public TimeSpan? Hora { get; set; }

    [Display(Name = "Perro")]
    public int? CaninoId { get; set; }
    public Canino Canino { get; set; }

    [Display(Name = "Tutor (dueño del perro)")]
    public int? TutorId { get; set; }
    public Tutor Tutor { get; set; }

    /// <summary>Instructor que dará la clase o atenderá la cita (del módulo Personal).</summary>
    [Display(Name = "Instructor")]
    public int? EmpleadoId { get; set; }
    public Empleado Empleado { get; set; }

    [Display(Name = "Avisarme por WhatsApp")]
    public bool AvisarWhatsApp { get; set; }

    [Display(Name = "Avisar al instructor por WhatsApp")]
    public bool AvisarInstructorWhatsApp { get; set; }

    /// <summary>Fecha de la ocurrencia para la que ya se envió el aviso de «1 día antes».</summary>
    public DateTime? Aviso1DiaFecha { get; set; }

    /// <summary>Fecha de la ocurrencia para la que ya se envió el aviso de «3 horas antes».</summary>
    public DateTime? Aviso3HorasFecha { get; set; }

    [NotMapped]
    public bool EsCita => Tipo == TipoCita;

    [NotMapped]
    public string HoraTexto => Hora.HasValue ? DateTime.Today.Add(Hora.Value).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant() : null;

    /// <summary>Próxima ocurrencia con su hora que todavía no ha pasado (sin hora: a las 9:00 am).</summary>
    public DateTime? ProximaOcurrencia(DateTime ahora)
    {
        var hora = Hora ?? TimeSpan.FromHours(9);
        var f = Proxima(ahora.Date);
        if (f.HasValue && f.Value.Add(hora) < ahora)
            f = Proxima(ahora.Date.AddDays(1));
        return f?.Add(hora);
    }

    public static readonly string[] Dias = { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };

    [NotMapped]
    public string Cuando => Frecuencia switch
    {
        "Mensual" => $"Cada mes, el día {DiaMes}",
        "Semanal" => DiaSemana.HasValue ? "Cada " + Dias[DiaSemana.Value % 7] : "Cada semana",
        "Anual" => Fecha.HasValue ? $"Cada año, el {Fecha.Value:dd/MM}" : "Cada año",
        "UnaVez" => Fecha.HasValue ? $"Una vez, el {Fecha.Value:dd/MM/yyyy}" : "Una vez",
        _ => ""
    };

    private static DateTime EnMes(int anio, int mes, int dia) =>
        new DateTime(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));

    /// <summary>Próxima fecha desde hoy (incluyendo hoy).</summary>
    public DateTime? Proxima(DateTime hoy)
    {
        hoy = hoy.Date;
        if (Frecuencia == "Mensual" && DiaMes.HasValue)
        {
            var c = EnMes(hoy.Year, hoy.Month, DiaMes.Value);
            if (c < hoy) { var s = hoy.AddMonths(1); c = EnMes(s.Year, s.Month, DiaMes.Value); }
            return c;
        }
        if (Frecuencia == "Semanal" && DiaSemana.HasValue)
        {
            int dif = (DiaSemana.Value - (int)hoy.DayOfWeek + 7) % 7;
            return hoy.AddDays(dif);
        }
        if (Frecuencia == "Anual" && Fecha.HasValue)
        {
            var c = EnMes(hoy.Year, Fecha.Value.Month, Fecha.Value.Day);
            if (c < hoy) c = EnMes(hoy.Year + 1, Fecha.Value.Month, Fecha.Value.Day);
            return c;
        }
        if (Frecuencia == "UnaVez" && Fecha.HasValue && Fecha.Value.Date >= hoy) return Fecha.Value.Date;
        return null;
    }

    /// <summary>Última fecha que ya pasó (antes de hoy).</summary>
    public DateTime? Anterior(DateTime hoy)
    {
        hoy = hoy.Date;
        if (Frecuencia == "Mensual" && DiaMes.HasValue)
        {
            var c = EnMes(hoy.Year, hoy.Month, DiaMes.Value);
            if (c >= hoy) { var p = hoy.AddMonths(-1); c = EnMes(p.Year, p.Month, DiaMes.Value); }
            return c;
        }
        if (Frecuencia == "Semanal" && DiaSemana.HasValue)
        {
            int dif = ((int)hoy.DayOfWeek - DiaSemana.Value + 7) % 7;
            if (dif == 0) dif = 7;
            return hoy.AddDays(-dif);
        }
        if (Frecuencia == "Anual" && Fecha.HasValue)
        {
            var c = EnMes(hoy.Year, Fecha.Value.Month, Fecha.Value.Day);
            if (c >= hoy) c = EnMes(hoy.Year - 1, Fecha.Value.Month, Fecha.Value.Day);
            return c;
        }
        if (Frecuencia == "UnaVez" && Fecha.HasValue && Fecha.Value.Date < hoy) return Fecha.Value.Date;
        return null;
    }
}
