// ===============================
// CONFIGURACION GENERAL DEL SISTEMA
// ===============================

using MiHotel.Data;
using MiHotel.Models.Configuracion;
using MiHotel.Services;
using Microsoft.AspNetCore.DataProtection;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// RUTA DEL ARCHIVO DE CONFIGURACION
// ===============================

string rutaConfig = Path.Combine(builder.Environment.ContentRootPath, "Config", "config.json");

// ===============================
// VALIDACION DE EXISTENCIA DEL ARCHIVO
// ===============================

if (!File.Exists(rutaConfig))
{
    var appError = builder.Build();

    appError.Run(async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync(@"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Sistema no disponible</title>
                <style>
                    body {
                        margin: 0;
                        padding: 0;
                        font-family: Arial, sans-serif;
                        background-color: #E6D3D0;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                    }
                    .contenedor {
                        background-color: #FFFFFF;
                        padding: 40px;
                        border-radius: 12px;
                        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                        max-width: 500px;
                        text-align: center;
                    }
                    h1 {
                        color: #824B44;
                        margin-bottom: 20px;
                    }
                    p {
                        color: #363636;
                        font-size: 16px;
                        line-height: 1.5;
                    }
                </style>
            </head>
            <body>
                <div class='contenedor'>
                    <h1>De momento no es posible acceder al sistema</h1>
                    <p>No se encontr� el archivo de configuraci�n requerido.</p>
                    <p>Contacte al administrador del sistema.</p>
                </div>
            </body>
            </html>
        ");
    });

    appError.Run();
    return;
}

// ===============================
// CARGA DE CONFIGURACION PERSONALIZADA
// ===============================

builder.Configuration.AddJsonFile(
    path: rutaConfig,
    optional: false,
    reloadOnChange: true
);

// ===============================
// REGISTRO DE LA CONFIGURACION EN MEMORIA
// ===============================

builder.Services.Configure<ConfigSistema>(
    builder.Configuration
);

// ===============================
// SERVICIOS DEL SISTEMA
// ===============================

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<ConexionBD>();
builder.Services.AddScoped<DisponibilidadService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// No existen cookies persistentes ni enlaces públicos que deban sobrevivir al
// reinicio. Cada ejecución genera sus propias llaves y cierra sesiones previas.
builder.Services.AddSingleton<IDataProtectionProvider>(
    new EphemeralDataProtectionProvider());

var app = builder.Build();

// ===============================
// CONFIGURACION DEL PIPELINE HTTP
// ===============================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// El sistema se ejecuta únicamente en la computadora local. Aunque un perfil
// se configure accidentalmente para escuchar en la red, las solicitudes de
// otros equipos se rechazan antes de llegar a los controladores.
app.Use(async (context, next) =>
{
    IPAddress? direccionRemota = context.Connection.RemoteIpAddress;

    if (direccionRemota != null && !IPAddress.IsLoopback(direccionRemota))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("El sistema solo está disponible localmente.");
        return;
    }

    await next();
});

// Solo el login puede consultarse sin una sesión interna activa. Las cuentas
// de clientes y sus antiguos flujos de autoservicio quedan deshabilitados.
app.Use(async (context, next) =>
{
    PathString ruta = context.Request.Path;
    bool esLogin = ruta.StartsWithSegments("/Acceso/Login");
    bool esError = ruta.StartsWithSegments("/Home/Error");

    if (!esLogin && !esError)
    {
        string? idUsuario = context.Session.GetString("IdUsuario");
        string rol = context.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";

        if (string.IsNullOrWhiteSpace(idUsuario) || rol == "cliente")
        {
            context.Session.Clear();
            context.Response.Redirect("/Acceso/Login");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Acceso}/{action=Login}/{id?}");

app.Run();
