using MySql.Data.MySqlClient;

namespace MiHotel.Data
{
    public class ConexionBD
    {
        private readonly string _cadenaConexion;

        public ConexionBD(IConfiguration configuration)
        {
            _cadenaConexion = configuration.GetConnectionString("ConexionHotel")
                ?? throw new InvalidOperationException(
                    "No se encontró la conexión de MiHotel. Configure ConnectionStrings:ConexionHotel.");

            var datos = new MySqlConnectionStringBuilder(_cadenaConexion);
            datos.Pooling = true;
            // MySQL y la aplicación residen en el mismo equipo; desactivar TLS
            // evita depender del almacén de certificados de la cuenta de Windows.
            datos.SslMode = MySqlSslMode.Disabled;
            datos.AllowPublicKeyRetrieval = true;
            datos.ConnectionTimeout = Math.Max(5u, datos.ConnectionTimeout);
            datos.DefaultCommandTimeout = Math.Max(30u, datos.DefaultCommandTimeout);
            _cadenaConexion = datos.ConnectionString;
        }

        public MySqlConnection ObtenerConexion()
        {
            return new MySqlConnection(_cadenaConexion);
        }

        public bool Comprobar(out string mensaje)
        {
            try
            {
                using var conexion = ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand("SELECT 1;", conexion);
                comando.ExecuteScalar();
                mensaje = "Base de datos disponible.";
                return true;
            }
            catch
            {
                mensaje = "No fue posible conectar con la base de datos.";
                return false;
            }
        }
    }
}
