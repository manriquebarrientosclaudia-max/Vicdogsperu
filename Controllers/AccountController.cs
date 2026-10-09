using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VicdogsPeru.Data;
using VicdogsPeru.Models;

namespace VicdogsPeru.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string usuario, string clave, string returnUrl = null)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.NombreUsuario == usuario);
        var ok = u != null &&
                 new PasswordHasher<Usuario>().VerifyHashedPassword(u, u.ClaveHash, clave ?? "") != PasswordVerificationResult.Failed;
        if (!ok)
        {
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.NombreCompleto ?? u.NombreUsuario)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, Authorize]
    public IActionResult CambiarClave() => View();

    [HttpPost, Authorize]
    public async Task<IActionResult> CambiarClave(string claveActual, string claveNueva, string confirmar)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var u = await _db.Usuarios.FindAsync(id);
        var hasher = new PasswordHasher<Usuario>();

        if (hasher.VerifyHashedPassword(u, u.ClaveHash, claveActual ?? "") == PasswordVerificationResult.Failed)
            ViewBag.Error = "La contraseña actual no es correcta.";
        else if (string.IsNullOrWhiteSpace(claveNueva) || claveNueva.Length < 8)
            ViewBag.Error = "La nueva contraseña debe tener al menos 8 caracteres.";
        else if (claveNueva != confirmar)
            ViewBag.Error = "La confirmación no coincide con la nueva contraseña.";
        else
        {
            u.ClaveHash = hasher.HashPassword(u, claveNueva);
            await _db.SaveChangesAsync();
            ViewBag.Ok = "Contraseña actualizada.";
        }
        return View();
    }
}
