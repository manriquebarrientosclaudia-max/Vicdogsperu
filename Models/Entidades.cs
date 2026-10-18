using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VicdogsPeru.Models;

public class Usuario
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string NombreUsuario { get; set; }
    [Required, StringLength(300)] public string ClaveHash { get; set; }
    [StringLength(100)] public string NombreCompleto { get; set; }
}

public class Servicio
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Nombre { get; set; }
    public ICollection<Canino> Caninos { get; set; } = new List<Canino>();
}

public class Tutor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre del tutor.")]
    [StringLength(120)]
    [Display(Name = "Nombre del tutor")]
    public string Nombre { get; set; }

    [StringLength(30)] [Display(Name = "Teléfono")] public string Telefono { get; set; }
    [DataType(DataType.Date)]
    [Display(Name = "Cumpleaños")]
    public DateTime? Cumpleanos { get; set; }
    [StringLength(200)] [Display(Name = "Dirección")] public string Direccion { get; set; }
    [StringLength(500)] public string Notas { get; set; }

    [StringLength(200)] [Display(Name = "Instagram")] public string Instagram { get; set; }
    [StringLength(200)] [Display(Name = "TikTok")] public string TikTok { get; set; }
    [StringLength(200)] [Display(Name = "Facebook")] public string Facebook { get; set; }

    public ICollection<CaninoTutor> Caninos { get; set; } = new List<CaninoTutor>();

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

public class Canino
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre del perro.")]
    [StringLength(80)]
    public string Nombre { get; set; }

    [Required(ErrorMessage = "Elige el sexo.")]
    [StringLength(10)]
    public string Sexo { get; set; }

    [StringLength(40)] [Display(Name = "Edad")] public string EdadTexto { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime? FechaNacimiento { get; set; }

    [StringLength(100)] [Display(Name = "Raza o cruce")] public string Raza { get; set; }

    [Required(ErrorMessage = "Elige el servicio.")]
    [Display(Name = "Servicio")]
    public int? ServicioId { get; set; }
    public Servicio Servicio { get; set; }

    [StringLength(80)] public string Distrito { get; set; }

    [Display(Name = "Peso referencial (kg)")]
    [Range(0, 200, ErrorMessage = "El peso debe estar entre 0 y 200 kg.")]
    public decimal? PesoKg { get; set; }

    [StringLength(30)] [Display(Name = "Tipo de baño")] public string TipoBano { get; set; }
    [StringLength(80)] [Display(Name = "Alimentación")] public string AlimentacionTipo { get; set; }
    [StringLength(80)] [Display(Name = "Marca de alimento")] public string AlimentacionMarca { get; set; }

    [StringLength(200)] [Display(Name = "Instagram")] public string Instagram { get; set; }
    [StringLength(200)] [Display(Name = "TikTok")] public string TikTok { get; set; }

    [StringLength(30)] [Display(Name = "Tipo de agua")] public string TipoAgua { get; set; }   // De la llave, Hervida, Mineral, Cualquiera

    [DataType(DataType.Date)] [Display(Name = "Hospedado desde")] public DateTime? HospedajeDesde { get; set; }
    [DataType(DataType.Date)] [Display(Name = "Hospedado hasta")] public DateTime? HospedajeHasta { get; set; }

    [StringLength(200)] public string FotoUrl { get; set; }
    [StringLength(1000)] public string Observaciones { get; set; }

    public bool Activo { get; set; } = true;
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de ingreso / registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public ICollection<CaninoTutor> Tutores { get; set; } = new List<CaninoTutor>();
    public ICollection<Vacuna> Vacunas { get; set; } = new List<Vacuna>();
    public ICollection<Antipulga> Antipulgas { get; set; } = new List<Antipulga>();
    public ICollection<Desparasitacion> Desparasitaciones { get; set; } = new List<Desparasitacion>();
    public ICollection<PesoRegistro> Pesos { get; set; } = new List<PesoRegistro>();

    /// <summary>En guardería y hogar recreativo la fecha de alta es la fecha de ingreso al servicio.</summary>
    [NotMapped]
    public bool EsIngresoAServicio =>
        string.Equals(Servicio?.Nombre, "Guardería Educativa", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Servicio?.Nombre, "Hogar Recreativo", StringComparison.OrdinalIgnoreCase);

    [NotMapped]
    public string EtiquetaFechaRegistro => EsIngresoAServicio ? "Fecha de ingreso al servicio" : "Registrado el";

    /// <summary>«05/10/2026 al 12/10/2026 (7 noches)» o null si no hay fechas.</summary>
    [NotMapped]
    public string HospedajeTexto
    {
        get
        {
            if (HospedajeDesde == null && HospedajeHasta == null) return null;
            var d = HospedajeDesde?.ToString("dd/MM/yyyy") ?? "—";
            var h = HospedajeHasta?.ToString("dd/MM/yyyy") ?? "—";
            var txt = $"Del {d} al {h}";
            if (HospedajeDesde != null && HospedajeHasta != null)
            {
                var n = (HospedajeHasta.Value.Date - HospedajeDesde.Value.Date).Days;
                if (n >= 0) txt += $" ({n} {(n == 1 ? "noche" : "noches")})";
            }
            return txt;
        }
    }

    [NotMapped]
    public string FotoOPlaceholder => string.IsNullOrEmpty(FotoUrl) ? "/img/sin-foto.svg" : FotoUrl;
}

public class CaninoTutor
{
    public int CaninoId { get; set; }
    public Canino Canino { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; }
}

public class Vacuna
{
    public int Id { get; set; }
    public int CaninoId { get; set; }
    public Canino Canino { get; set; }

    [Required, StringLength(40)] public string Tipo { get; set; }   // Séxtuple, Leptospira, KC, Otra
    public DateTime? FechaAplicada { get; set; }
    public DateTime? FechaProxima { get; set; }
    [StringLength(300)] public string Observacion { get; set; }

    /// <summary>Meses que dura cada vacuna: Leptospira y KC cada 6 meses; Séxtuple y las demás cada 12.</summary>
    public static int MesesVigencia(string tipo) => tipo == "Leptospira" || tipo == "KC" ? 6 : 12;

    /// <summary>Fecha en que vence: la próxima indicada o, si no hay, la aplicada más la vigencia de su tipo.</summary>
    [NotMapped]
    public DateTime? Vencimiento => FechaProxima ?? FechaAplicada?.AddMonths(MesesVigencia(Tipo));
}

public class Antipulga
{
    public int Id { get; set; }
    public int CaninoId { get; set; }
    public Canino Canino { get; set; }
    public DateTime? Fecha { get; set; }
    [StringLength(80)] public string Marca { get; set; }

    /// <summary>Duración del antipulgas: 1 mes o 3 meses (según el producto).</summary>
    public int? Meses { get; set; }

    [NotMapped]
    public DateTime? Proxima => Fecha != null && Meses != null ? Fecha.Value.AddMonths(Meses.Value) : null;
}

public class Desparasitacion
{
    public int Id { get; set; }
    public int CaninoId { get; set; }
    public Canino Canino { get; set; }
    public DateTime Fecha { get; set; }
    [StringLength(80)] public string Producto { get; set; }   // se muestra como «Marca»

    /// <summary>La desparasitación se repite cada 3 meses.</summary>
    [NotMapped]
    public DateTime Proxima => Fecha.AddMonths(3);
}

public class PesoRegistro
{
    public int Id { get; set; }
    public int CaninoId { get; set; }
    public Canino Canino { get; set; }

    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; }

    [Range(0.1, 200, ErrorMessage = "El peso debe estar entre 0.1 y 200 kg.")]
    public decimal PesoKg { get; set; }

    [StringLength(200)] public string Nota { get; set; }
}
