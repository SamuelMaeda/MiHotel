// ===============================
// CONFIGURACION GENERAL DEL SISTEMA
// ===============================

using MiHotel.Data;
using MiHotel.Models.Configuracion;

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
                    <p>No se encontró el archivo de configuración requerido.</p>
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

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ===============================
// CONFIGURACION DEL PIPELINE HTTP
// ===============================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Acceso}/{action=Login}/{id?}");

app.Run();