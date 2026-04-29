// ===============================
// SERVICIO DE DISPONIBILIDAD
// ===============================

using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;

namespace MiHotel.Services
{
    public class DisponibilidadService
    {
        private readonly ConexionBD _conexionBD;

        public DisponibilidadService(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ===============================
        // OBTENER ID DE TIPO PROSER HABITACION
        // ===============================
        private int ObtenerIdTipoProserHabitacion(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_tipoproser
                FROM tipo_proser
                WHERE LOWER(nombre) = 'habitacion'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe el tipo 'habitacion' en tipo_proser.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // OBTENER HABITACIONES DISPONIBLES
        // ===============================
        public List<DisponibilidadResultadoViewModel> ObtenerHabitacionesDisponibles(
            DateTime fechaEntrada,
            DateTime fechaSalida,
            int? idSubcategoria = null)
        {
            List<DisponibilidadResultadoViewModel> lista = new List<DisponibilidadResultadoViewModel>();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

            string filtroSubcategoria = idSubcategoria.HasValue
                ? "AND p.id_subcategoria = @id_subcategoria"
                : "";

            string consulta = $@"
                SELECT
                    p.id_proser,
                    p.codigo,
                    COALESCE(s.nombre_subcategoria, '-') AS tipo_habitacion,
                    COALESCE(s.precio, 0) AS precio,
                    te.estado
                FROM proser p
                LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                WHERE p.id_tipoproser = @id_tipoproser
                  AND LOWER(te.estado) NOT IN ('remodelacion', 'renta')
                  {filtroSubcategoria}
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM reserva r
                      WHERE r.id_habitacion = p.id_proser
                        AND r.estado IN ('pendiente', 'confirmada', 'en_curso')
                        AND (
                            @fecha_entrada < r.fecha_salida
                            AND @fecha_salida > r.fecha_entrada
                        )
                  )
                ORDER BY p.codigo ASC;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);
            comando.Parameters.AddWithValue("@fecha_entrada", fechaEntrada.Date);
            comando.Parameters.AddWithValue("@fecha_salida", fechaSalida.Date);

            if (idSubcategoria.HasValue)
            {
                comando.Parameters.AddWithValue("@id_subcategoria", idSubcategoria.Value);
            }

            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                lista.Add(new DisponibilidadResultadoViewModel
                {
                    IdHabitacion = Convert.ToInt32(lector["id_proser"]),
                    NumeroHabitacion = lector["codigo"]?.ToString() ?? "",
                    TipoHabitacion = lector["tipo_habitacion"]?.ToString() ?? "-",
                    Precio = Convert.ToDecimal(lector["precio"]),
                    Estado = lector["estado"]?.ToString() ?? ""
                });
            }

            return lista;
        }

        // ===============================
        // VALIDAR SI UNA HABITACION ESTA DISPONIBLE
        // ===============================
        public bool EstaHabitacionDisponible(
            int idHabitacion,
            DateTime fechaEntrada,
            DateTime fechaSalida,
            int? idReservaExcluir = null)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string excluirReserva = idReservaExcluir.HasValue
                ? "AND r.id_reserva <> @id_reserva_excluir"
                : "";

            string consulta = $@"
                SELECT COUNT(*)
                FROM reserva r
                INNER JOIN proser p ON r.id_habitacion = p.id_proser
                INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                WHERE r.id_habitacion = @id_habitacion
                  AND r.estado IN ('pendiente', 'confirmada', 'en_curso')
                  {excluirReserva}
                  AND (
                      @fecha_entrada < r.fecha_salida
                      AND @fecha_salida > r.fecha_entrada
                  );";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_habitacion", idHabitacion);
            comando.Parameters.AddWithValue("@fecha_entrada", fechaEntrada.Date);
            comando.Parameters.AddWithValue("@fecha_salida", fechaSalida.Date);

            if (idReservaExcluir.HasValue)
            {
                comando.Parameters.AddWithValue("@id_reserva_excluir", idReservaExcluir.Value);
            }

            int cruces = Convert.ToInt32(comando.ExecuteScalar());

            if (cruces > 0)
            {
                return false;
            }

            string consultaEstadoOperativo = @"
                SELECT LOWER(te.estado)
                FROM proser p
                INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                WHERE p.id_proser = @id_habitacion
                LIMIT 1;";

            using var comandoEstado = new MySqlCommand(consultaEstadoOperativo, conexion);
            comandoEstado.Parameters.AddWithValue("@id_habitacion", idHabitacion);

            object? resultadoEstado = comandoEstado.ExecuteScalar();
            string estadoActual = resultadoEstado?.ToString()?.Trim().ToLower() ?? "";

            if (estadoActual == "remodelacion" || estadoActual == "renta")
            {
                return false;
            }

            return true;
        }
    }
}