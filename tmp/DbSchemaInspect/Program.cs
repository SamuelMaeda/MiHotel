using System.Text.Json;
using MySql.Data.MySqlClient;

string raiz = Directory.GetCurrentDirectory();
string appsettings = Path.Combine(raiz, "MiHotel", "appsettings.json");
string migracion = Path.Combine(
    raiz,
    "MiHotel",
    "Database",
    "Migrations",
    "20260826_facturacion_pendiente.sql");
using JsonDocument json = JsonDocument.Parse(File.ReadAllText(appsettings));
string cadena = json.RootElement.GetProperty("ConnectionStrings").GetProperty("ConexionHotel").GetString()
    ?? throw new InvalidOperationException("No se encontró la conexión.");

using var conexion = new MySqlConnection(cadena);
conexion.Open();

string sql = File.ReadAllText(migracion);
var script = new MySqlScript(conexion, sql);
int sentencias = script.Execute();
Console.WriteLine($"Sentencias ejecutadas: {sentencias}");

string[] tablas =
[
    "reserva_facturacion",
    "reserva_facturacion_historial",
    "documento_fiscal",
    "documento_fiscal_reserva"
];
foreach (string tabla in tablas)
{
    using var comando = new MySqlCommand($"SELECT COUNT(*) FROM `{tabla}`;", conexion);
    Console.WriteLine($"{tabla}: {Convert.ToInt64(comando.ExecuteScalar())} filas");
}
