// ===============================
// CONFIGURACION GENERAL DEL SISTEMA
// ===============================

using MiHotel.Data;
using MiHotel.Infrastructure;
using MiHotel.Models.Configuracion;
using MiHotel.Services;
using Microsoft.AspNetCore.DataProtection;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var rutasSistema = RutasSistema.Crear(builder.Environment);
rutasSistema.CrearDirectorios();

// Una instalación local no debe depender de permisos para escribir en el
// registro de eventos de Windows. La consola permite diagnosticar el sistema
// incluso cuando se ejecuta con una cuenta estándar del hotel.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new ArchivoLoggerProvider(rutasSistema.Registros));

// ===============================
// RUTA DEL ARCHIVO DE CONFIGURACION
// ===============================

string rutaConfig = Path.Combine(rutasSistema.Configuracion, "config.json");
string rutaBaseDatos = Path.Combine(rutasSistema.Configuracion, "database.json");

// ===============================
// VALIDACION DE EXISTENCIA DEL ARCHIVO
// ===============================

bool faltaConfiguracionEmpresa = !File.Exists(rutaConfig);
bool faltaConfiguracionBaseDatos = !builder.Environment.IsDevelopment() && !File.Exists(rutaBaseDatos);

if (faltaConfiguracionEmpresa || faltaConfiguracionBaseDatos)
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
                    <p>No se encontró la configuración requerida para iniciar MiHotel.</p>
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

builder.Configuration.AddJsonFile(
    path: rutaBaseDatos,
    optional: builder.Environment.IsDevelopment(),
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

// Las claves permanecen en la carpeta de datos controlada por MiHotel. Así se
// evitan dependencias del perfil de Windows y las sesiones sobreviven reinicios.
// El instalador limitará los permisos de esta carpeta a la cuenta que ejecute la app.
builder.Services.AddDataProtection()
    .SetApplicationName("MiHotel")
    .PersistKeysToFileSystem(new DirectoryInfo(rutasSistema.ClavesProteccion));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(rutasSistema);
builder.Services.AddScoped<ConexionBD>();
builder.Services.AddScoped<DisponibilidadService>();
builder.Services.AddScoped<FacturacionService>();
builder.Services.AddScoped<PermisosUsuarioService>();
builder.Services.AddScoped<DatabaseMigrationService>();
builder.Services.AddSingleton<SuspensionMonitorService>();
builder.Services.AddHostedService(servicios => servicios.GetRequiredService<SuspensionMonitorService>());

builder.Services.AddSession(options =>
{
    // La jornada del hotel no debe interrumpirse por falta de actividad. La
    // sesión se conserva mientras MiHotel y el equipo continúen funcionando.
    options.IdleTimeout = TimeSpan.FromDays(3650);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = ".MiHotel.Session";
    options.Cookie.MaxAge = TimeSpan.FromDays(3650);
});

var app = builder.Build();

// Una base existente se actualiza antes de aceptar operaciones. Si una migración
// falla, el proceso no inicia y el instalador/servicio puede detectarlo en el log.
using (IServiceScope scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>().AplicarPendientes();
}

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

// Al reanudarse el equipo después de una suspensión se invalida la sesión.
// Esto no depende de la actividad del usuario: el monitor observa una pausa
// del proceso completo y cambia la generación de sesiones aceptada.
app.Use(async (context, next) =>
{
    var monitorSuspension = context.RequestServices.GetRequiredService<SuspensionMonitorService>();
    string? idUsuario = context.Session.GetString("IdUsuario");

    if (!string.IsNullOrWhiteSpace(idUsuario))
    {
        int generacionActual = monitorSuspension.GeneracionActual;
        int? generacionSesion = context.Session.GetInt32("GeneracionSesion");

        if (generacionSesion.HasValue && generacionSesion.Value != generacionActual)
        {
            context.Session.Clear();
            context.Response.Redirect("/Acceso/Login");
            return;
        }

        if (!generacionSesion.HasValue)
        {
            context.Session.SetInt32("GeneracionSesion", generacionActual);
        }
    }

    await next();
});

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
    bool esRutaInicial = ruta == "/";
    bool esError = ruta.StartsWithSegments("/Home/Error");
    bool esEstado = ruta.StartsWithSegments("/estado");

    if (!esLogin && !esRutaInicial && !esError && !esEstado)
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

app.MapGet("/estado", (ConexionBD conexionBD) =>
{
    bool disponible = conexionBD.Comprobar(out string mensaje);
    return Results.Json(new
    {
        aplicacion = "MiHotel",
        estado = disponible ? "disponible" : "sin_conexion",
        baseDatos = mensaje,
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "sin_version"
    }, statusCode: disponible ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Acceso}/{action=Login}/{id?}");

app.Run();
