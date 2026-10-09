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
