namespace MiHotel.Infrastructure;

public sealed class RutasSistema
{
    public RutasSistema(string raizDatos, string configuracion, string registros, string respaldos, string claves)
    {
        RaizDatos = raizDatos;
        Configuracion = configuracion;
        Registros = registros;
        Respaldos = respaldos;
        ClavesProteccion = claves;
    }

    public string RaizDatos { get; }
    public string Configuracion { get; }
    public string Registros { get; }
    public string Respaldos { get; }
    public string ClavesProteccion { get; }

    public static RutasSistema Crear(IHostEnvironment ambiente)
    {
        string? raizConfigurada = Environment.GetEnvironmentVariable("MIHOTEL_DATA_DIR");
        string raiz = !string.IsNullOrWhiteSpace(raizConfigurada)
            ? Path.GetFullPath(raizConfigurada)
            : ambiente.IsDevelopment()
                ? Path.Combine(ambiente.ContentRootPath, "App_Data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MiHotel");

        string configuracion = ambiente.IsDevelopment()
            ? Path.Combine(ambiente.ContentRootPath, "Config")
            : Path.Combine(raiz, "Config");

        return new RutasSistema(
            raiz,
            configuracion,
            Path.Combine(raiz, "Logs"),
            Path.Combine(raiz, "Backups"),
            Path.Combine(raiz, "Keys"));
    }

    public void CrearDirectorios()
    {
        Directory.CreateDirectory(RaizDatos);
        Directory.CreateDirectory(Configuracion);
        Directory.CreateDirectory(Registros);
        Directory.CreateDirectory(Respaldos);
        Directory.CreateDirectory(ClavesProteccion);
    }
}
