namespace VicdogsPeru.Models;

public static class Ayudas
{
    /// <summary>Resume el estado de vacunación según la fecha de vencimiento más cercana de cada tipo.</summary>
    public static (string Texto, string Clase) EstadoVacunas(this Canino c)
    {
        var venc = c.Vacunas.Where(v => v.Vencimiento != null)
            .GroupBy(v => v.Tipo).Select(g => g.Max(v => v.Vencimiento.Value)).ToList();
        if (!venc.Any()) return ("Sin fechas de vacuna", "estado-sin");

        var dias = (venc.Min().Date - DateTime.Today).Days;
        if (dias < 0) return ("Vacuna vencida", "estado-vencida");
        if (dias == 0) return ("Vacuna vence hoy", "estado-pronto");
        if (dias <= 30) return ($"Vacuna vence en {dias} días", "estado-pronto");
        return ("Vacunas al día", "estado-ok");
    }

    private static readonly string[] Meses = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
    public static string MesCorto(this DateTime f) => Meses[f.Month - 1];

    private static readonly string[] MesesLargos = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
    /// <summary>«15 de marzo» (sin año).</summary>
    public static string DiaYMes(this DateTime f) => f.Day + " de " + MesesLargos[f.Month - 1];

    /// <summary>Próxima vez que cae un cumpleaños (hoy cuenta).</summary>
    public static DateTime ProximoCumple(this DateTime nacimiento, DateTime hoy)
    {
        hoy = hoy.Date;
        DateTime En(int anio) => new DateTime(anio, nacimiento.Month, Math.Min(nacimiento.Day, DateTime.DaysInMonth(anio, nacimiento.Month)));
        var c = En(hoy.Year);
        return c < hoy ? En(hoy.Year + 1) : c;
    }

    // ----- Redes sociales -----
    /// <summary>Saca el usuario de un texto como «@sam», «sam» o un enlace de Instagram/TikTok.</summary>
    public static string UsuarioRed(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        valor = valor.Trim();
        if (!valor.Contains("://") && valor.Contains('/')) valor = "https://" + valor;
        if (Uri.TryCreate(valor, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
        {
            var seg = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrEmpty(seg) ? null : seg.TrimStart('@');
        }
        var usuario = valor.TrimStart('@').Trim('/');
        return usuario.Length == 0 ? null : usuario;
    }

    /// <summary>Enlace para abrir el perfil. Solo http/https.</summary>
    public static string EnlaceRed(string valor, string red)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        if (Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")) return u.ToString();
        var usuario = UsuarioRed(v);
        if (usuario == null) return null;
        return red switch
        {
            "instagram" => "https://www.instagram.com/" + Uri.EscapeDataString(usuario) + "/",
            "facebook" => "https://www.facebook.com/" + Uri.EscapeDataString(usuario),
            _ => "https://www.tiktok.com/@" + Uri.EscapeDataString(usuario)
        };
    }

    /// <summary>Texto para mostrar una red: «@usuario» en Instagram/TikTok y el nombre de la página en Facebook.</summary>
    public static string TextoRed(string valor, string red)
    {
        if (red != "facebook") return Etiqueta(valor);
        var u = UsuarioRed(valor);
        if (u == null) return null;
        return u.StartsWith("profile.php", StringComparison.OrdinalIgnoreCase) ? "Perfil de Facebook" : u;
    }

    /// <summary>«@usuario» listo para etiquetar.</summary>
    public static string Etiqueta(string valor)
    {
        var u = UsuarioRed(valor);
        return u == null ? null : "@" + u;
    }

    // ----- Contacto de tutores -----
    private static string Digitos(string tel) => new string((tel ?? "").Where(c => char.IsDigit(c)).ToArray());

    /// <summary>Número listo para la API de WhatsApp (solo dígitos, con 51 si es celular peruano) o null.</summary>
    public static string TelefonoWhatsApp(string telefono)
    {
        var d = Digitos(telefono);
        if (d.Length == 9 && d.StartsWith("9")) d = "51" + d;
        return d.Length >= 10 ? d : null;
    }

    public static string EnlaceWhatsApp(string telefono)
    {
        var d = Digitos(telefono);
        if (d.Length == 9 && d.StartsWith("9")) d = "51" + d;   // celular de Perú
        return d.Length >= 9 ? "https://wa.me/" + d : null;
    }

    public static string EnlaceLlamar(string telefono)
    {
        var d = Digitos(telefono);
        return d.Length >= 6 ? "tel:" + (telefono.Trim().StartsWith("+") ? "+" : "") + d : null;
    }

    public static string Fecha(this DateTime? f) => f.HasValue ? f.Value.ToString("dd/MM/yyyy") : "—";
    public static string Fecha(this DateTime f) => f.ToString("dd/MM/yyyy");
}
