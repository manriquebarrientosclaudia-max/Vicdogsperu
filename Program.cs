using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VicdogsPeru.Data;
using VicdogsPeru.Services;

// Las fechas del proyecto son DateTime "locales" (DateTime.Today / Now): Npgsql las guarda como
// "timestamp without time zone" en vez de exigir UTC.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ---- Puerto: Render entrega el puerto en la variable PORT ----
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto))
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");

// ---- Base de datos (PostgreSQL) ----
// En Render se arma con las variables DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD.
// En local se usa ConnectionStrings:Default (appsettings.Development.json).
var connectionString = builder.Configuration.GetConnectionString("Default");
var dbHost = builder.Configuration["DB_HOST"];
if (!string.IsNullOrEmpty(dbHost))
{
    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = dbHost,
        Port = int.TryParse(builder.Configuration["DB_PORT"], out var p) ? p : 5432,
        Database = builder.Configuration["DB_NAME"] ?? "vicdogs",
        Username = builder.Configuration["DB_USER"],
        Password = builder.Configuration["DB_PASSWORD"]
    };
    // Para una base externa que exija SSL: DB_SSLMODE=Require
    if (Enum.TryParse<SslMode>(builder.Configuration["DB_SSLMODE"], true, out var ssl))
        csb.SslMode = ssl;
    connectionString = csb.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

// ---- Avisos por WhatsApp (1 día y 3 horas antes de cada cita) ----
builder.Services.AddHttpClient<IWhatsAppSender, WhatsAppCloudSender>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<AvisosWhatsApp>();
builder.Services.AddHostedService<AvisosWhatsAppFondo>();

// ---- Render termina el HTTPS y reenvía por HTTP: hay que confiar en sus cabeceras ----
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---- Llaves de sesión/antiforgery en el disco persistente (si no, cada deploy cierra las sesiones) ----
var rutaLlaves = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(rutaLlaves))
{
    Directory.CreateDirectory(rutaLlaves);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(rutaLlaves))
        .SetApplicationName("VicdogsPeru");
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
    });

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AuthorizeFilter());                 // todo requiere iniciar sesión
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

app.UseForwardedHeaders();   // debe ir primero

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Cultura invariante: los decimales (peso) y fechas se envían siempre como 7.4 y aaaa-mm-dd
var cultura = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new List<CultureInfo> { cultura },
    SupportedUICultures = new List<CultureInfo> { cultura }
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Health check para Render (sin login)
app.MapGet("/health", () => Results.Ok("ok")).AllowAnonymous();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

// La base de datos puede tardar unos segundos en estar lista en el primer arranque: se reintenta.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var intento = 1; ; intento++)
    {
        try
        {
            await DbInitializer.InicializarAsync(scope.ServiceProvider, app.Environment, app.Configuration);
            break;
        }
        catch (Exception ex) when (intento < 12)
        {
            logger.LogWarning("Base de datos no disponible (intento {Intento}/12): {Mensaje}", intento, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

app.Run();
