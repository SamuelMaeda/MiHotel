using System.Security.Cryptography;
using MiHotel.Data;
using MySql.Data.MySqlClient;

namespace MiHotel.Services;

public sealed class DatabaseMigrationService
{
    private readonly ConexionBD _conexionBD;
    private readonly IWebHostEnvironment _ambiente;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(
        ConexionBD conexionBD,
        IWebHostEnvironment ambiente,
        ILogger<DatabaseMigrationService> logger)
    {
        _conexionBD = conexionBD;
        _ambiente = ambiente;
        _logger = logger;
    }

    public void AplicarPendientes()
    {
        string directorio = Path.Combine(_ambiente.ContentRootPath, "Database", "Migrations");
        if (!Directory.Exists(directorio))
        {
            _logger.LogWarning("No se encontró el directorio de migraciones: {Directorio}", directorio);
            return;
        }

        using var conexion = _conexionBD.ObtenerConexion();
        conexion.Open();
        CrearTablaControl(conexion);

        foreach (string archivo in Directory.EnumerateFiles(directorio, "*.sql").OrderBy(Path.GetFileName))
        {
            string nombre = Path.GetFileName(archivo);
            string sql = File.ReadAllText(archivo);
            string checksum = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql)));
            string? checksumAplicado = ObtenerChecksum(conexion, nombre);

            if (checksumAplicado != null)
            {
                if (!string.Equals(checksumAplicado, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"La migración aplicada '{nombre}' fue modificada posteriormente.");
                }
                continue;
            }

            _logger.LogInformation("Aplicando migración {Migracion}", nombre);
            var script = new MySqlScript(conexion, sql);
            script.Execute();

            using var registrar = new MySqlCommand(@"
                INSERT INTO sistema_migracion (nombre, checksum)
                VALUES (@nombre, @checksum);", conexion);
            registrar.Parameters.AddWithValue("@nombre", nombre);
            registrar.Parameters.AddWithValue("@checksum", checksum);
            registrar.ExecuteNonQuery();
        }
    }

    private static void CrearTablaControl(MySqlConnection conexion)
    {
        using var comando = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS sistema_migracion (
                id_migracion INT NOT NULL AUTO_INCREMENT,
                nombre VARCHAR(255) NOT NULL,
                checksum CHAR(64) NOT NULL,
                fecha_aplicacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (id_migracion),
                UNIQUE KEY uq_sistema_migracion_nombre (nombre)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;", conexion);
        comando.ExecuteNonQuery();
    }

    private static string? ObtenerChecksum(MySqlConnection conexion, string nombre)
    {
        using var comando = new MySqlCommand(
            "SELECT checksum FROM sistema_migracion WHERE nombre=@nombre LIMIT 1;", conexion);
        comando.Parameters.AddWithValue("@nombre", nombre);
        return comando.ExecuteScalar()?.ToString();
    }
}
