using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VicdogsPeru.Models;

/// <summary>Persona que trabaja en Vicdogs.</summary>
public class Empleado
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(120)]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; }

    [StringLength(80)] [Display(Name = "Cargo")] public string Cargo { get; set; }
    [StringLength(30)] [Display(Name = "Teléfono")] public string Telefono { get; set; }

    [Display(Name = "Trabaja actualmente")]
    public bool Activo { get; set; } = true;

    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();

    [NotMapped]
    public string Iniciales
    {
        get
        {
            var partes = (Nombre ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return "?";
            return partes.Length == 1 ? partes[0][..1].ToUpper() : (partes[0][..1] + partes[1][..1]).ToUpper();
        }
    }
}

/// <summary>Asistencia de un empleado en un día (una por empleado y por fecha).</summary>
public class Asistencia
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; }

    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; }

    /// <summary>Presente, Falta o Permiso.</summary>
    [Required, StringLength(10)]
    public string Estado { get; set; } = Presente;

    public TimeSpan? HoraEntrada { get; set; }
    public TimeSpan? HoraSalida { get; set; }

    [StringLength(200)]
    public string Nota { get; set; }

    public const string Presente = "Presente";
    public const string Falta = "Falta";
    public const string Permiso = "Permiso";
    public static readonly string[] Estados = { Presente, Falta, Permiso };

    public static string Letra(string estado) => estado switch { Presente => "A", Falta => "F", Permiso => "P", _ => "" };
    public static string Clase(string estado) => estado switch { Presente => "as-presente", Falta => "as-falta", Permiso => "as-permiso", _ => "as-sin" };

    /// <summary>«08:00 – 17:30», «desde 08:00» o vacío.</summary>
    [NotMapped]
    public string Horario =>
        HoraEntrada != null && HoraSalida != null ? $"{HoraEntrada:hh\\:mm} – {HoraSalida:hh\\:mm}" :
        HoraEntrada != null ? $"desde {HoraEntrada:hh\\:mm}" :
        HoraSalida != null ? $"hasta {HoraSalida:hh\\:mm}" : "";

    /// <summary>Horas trabajadas si hay entrada y salida.</summary>
    [NotMapped]
    public double? Horas => HoraEntrada != null && HoraSalida != null && HoraSalida > HoraEntrada
        ? (HoraSalida.Value - HoraEntrada.Value).TotalHours : null;
}
