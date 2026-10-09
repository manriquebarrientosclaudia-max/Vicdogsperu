using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Models;

namespace VicdogsPeru.Data;

public static class DbInitializer
{
    public static async Task InicializarAsync(IServiceProvider sp, IWebHostEnvironment env, IConfiguration config)
    {
        var db = sp.GetRequiredService<AppDbContext>();

        // Crea las tablas si todavía no existen (la base la crea Render)
        await db.Database.EnsureCreatedAsync();

        // Datos migrados de la versión anterior: la vacuna «Rabia» pasa a llamarse «Séxtuple»
        await db.Database.ExecuteSqlRawAsync("UPDATE \"Vacunas\" SET \"Tipo\" = 'Séxtuple' WHERE \"Tipo\" = 'Rabia'");

        // Usuario administrador inicial
        if (!await db.Usuarios.AnyAsync())
        {
            var usuario = new Usuario
            {
                NombreUsuario = config["Admin:Usuario"] ?? "admin",
                NombreCompleto = config["Admin:Nombre"] ?? "Administrador"
            };
            usuario.ClaveHash = new PasswordHasher<Usuario>().HashPassword(usuario, config["Admin:Clave"] ?? "Vicdogs2026!");
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
        }

        var ruta = Path.Combine(env.ContentRootPath, "Data", "seed.json");
        if (File.Exists(ruta))
        {
            await SembrarAsync(db, ruta);
        }

        // Cada perro que tenía un peso referencial pero ningún registro recibe su primer registro de peso
        var sinRegistro = await db.Caninos.Where(c => c.PesoKg != null && !c.Pesos.Any()).ToListAsync();
        foreach (var c in sinRegistro)
            db.Pesos.Add(new PesoRegistro { CaninoId = c.Id, Fecha = c.FechaRegistro.Date, PesoKg = c.PesoKg.Value, Nota = "Peso inicial" });
        if (sinRegistro.Any()) await db.SaveChangesAsync();
    }

    private static async Task SembrarAsync(AppDbContext db, string ruta)
    {
        var semilla = JsonSerializer.Deserialize<Semilla>(File.ReadAllText(ruta),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Servicios
        if (!await db.Servicios.AnyAsync())
        {
            db.Servicios.AddRange(semilla.Servicios.Select(n => new Servicio { Nombre = n }));
            await db.SaveChangesAsync();
        }

        // Carga inicial de perros (solo si la tabla está vacía)
        if (await db.Caninos.AnyAsync()) return;

        var servicios = await db.Servicios.ToDictionaryAsync(s => s.Nombre);
        var tutores = new Dictionary<string, Tutor>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in semilla.Caninos)
        {
            var c = new Canino
            {
                Nombre = s.Nombre,
                Sexo = s.Sexo,
                EdadTexto = s.Edad,
                Raza = s.Raza,
                Servicio = servicios[s.Servicio],
                Distrito = s.Distrito,
                TipoBano = s.Bano,
                AlimentacionTipo = s.AlimTipo,
                AlimentacionMarca = s.AlimMarca,
                PesoKg = s.Peso,
                FechaNacimiento = Fecha(s.FechaNacimiento),
                FotoUrl = string.IsNullOrEmpty(s.Foto) ? null : "/uploads/caninos/" + s.Foto,
                Observaciones = s.Observaciones,
                Activo = true
            };

            foreach (var nombre in s.Tutores)
            {
                if (!tutores.TryGetValue(nombre, out var t))
                {
                    t = new Tutor { Nombre = nombre };
                    tutores[nombre] = t;
                }
                c.Tutores.Add(new CaninoTutor { Canino = c, Tutor = t });
            }

            foreach (var v in s.Vacunas)
                c.Vacunas.Add(new Vacuna { Tipo = v.Tipo, FechaAplicada = Fecha(v.Aplicada), FechaProxima = Fecha(v.Proxima), Observacion = v.Obs });
            foreach (var f in s.Desparasitaciones)
                c.Desparasitaciones.Add(new Desparasitacion { Fecha = Fecha(f).Value });
            foreach (var a in s.Antipulgas)
                c.Antipulgas.Add(new Antipulga { Fecha = Fecha(a.Fecha), Marca = a.Marca, Meses = a.Meses });

            db.Caninos.Add(c);
        }
        await db.SaveChangesAsync();
    }

    private static DateTime? Fecha(string texto) =>
        DateTime.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var f) ? f : null;

    // Clases para leer seed.json
    private class Semilla { public List<string> Servicios { get; set; } public List<SCanino> Caninos { get; set; } }
    private class SCanino
    {
        public string Nombre { get; set; } public string Sexo { get; set; } public string Edad { get; set; }
        public string Raza { get; set; } public string Servicio { get; set; } public string Distrito { get; set; }
        public List<string> Tutores { get; set; } = new();
        public string Foto { get; set; } public string Bano { get; set; }
        public string AlimTipo { get; set; } public string AlimMarca { get; set; }
        public decimal? Peso { get; set; } public string FechaNacimiento { get; set; } public string Observaciones { get; set; }
        public List<SVacuna> Vacunas { get; set; } = new();
        public List<string> Desparasitaciones { get; set; } = new();
        public List<SAntipulga> Antipulgas { get; set; } = new();
    }
    private class SVacuna { public string Tipo { get; set; } public string Aplicada { get; set; } public string Proxima { get; set; } public string Obs { get; set; } }
    private class SAntipulga { public string Fecha { get; set; } public string Marca { get; set; } public int? Meses { get; set; } }
}
