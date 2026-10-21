using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VicdogsPeru.Services;

public interface IWhatsAppSender
{
    /// <summary>True si hay token y número configurados (si no, los avisos solo se anotan en el log).</summary>
    bool Configurado { get; }
    /// <summary>Envía un mensaje. Devuelve true si WhatsApp lo aceptó.</summary>
    Task<bool> EnviarAsync(string telefono, string mensaje, CancellationToken ct = default);
}

/// <summary>
/// Envía por la API oficial de WhatsApp Business (Meta Cloud API).
/// Variables: WhatsApp__Token, WhatsApp__PhoneNumberId y, para escribir primero a alguien (fuera de las 24 h de una conversación),
/// una plantilla aprobada con una sola variable en WhatsApp__Plantilla.
/// </summary>
public class WhatsAppCloudSender : IWhatsAppSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppCloudSender> _log;

    public WhatsAppCloudSender(HttpClient http, IConfiguration config, ILogger<WhatsAppCloudSender> log)
    {
        _http = http; _config = config; _log = log;
    }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_config["WhatsApp:Token"]) && !string.IsNullOrWhiteSpace(_config["WhatsApp:PhoneNumberId"]);

    public async Task<bool> EnviarAsync(string telefono, string mensaje, CancellationToken ct = default)
    {
        if (!Configurado)
        {
            _log.LogWarning("WhatsApp sin configurar. Mensaje para {Telefono}: {Mensaje}", telefono, mensaje);
            return false;
        }

        var plantilla = _config["WhatsApp:Plantilla"];
        object cuerpo;
        if (!string.IsNullOrWhiteSpace(plantilla))
        {
            // Las variables de plantilla no admiten saltos de línea
            var texto = mensaje.Replace("\r", " ").Replace("\n", " · ");
            cuerpo = new
            {
                messaging_product = "whatsapp",
                to = telefono,
                type = "template",
                template = new
                {
                    name = plantilla,
                    language = new { code = _config["WhatsApp:Idioma"] ?? "es" },
                    components = new object[]
                    {
                        new { type = "body", parameters = new object[] { new { type = "text", text = texto } } }
                    }
                }
            };
        }
        else
        {
            cuerpo = new { messaging_product = "whatsapp", to = telefono, type = "text", text = new { body = mensaje } };
        }

        var version = _config["WhatsApp:ApiVersion"] ?? "v21.0";
        var url = $"https://graph.facebook.com/{version}/{_config["WhatsApp:PhoneNumberId"]}/messages";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["WhatsApp:Token"]);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return true;
            _log.LogError("WhatsApp rechazó el mensaje a {Telefono}: {Codigo} {Detalle}", telefono, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "No se pudo contactar a WhatsApp para {Telefono}", telefono);
            return false;
        }
    }
}
