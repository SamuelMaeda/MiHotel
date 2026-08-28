using MySql.Data.MySqlClient;

namespace MiHotel.Services
{
    public class FacturacionService
    {
        public void RegistrarDecision(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReserva,
            bool requiereFactura,
            int idUsuario,
            string origen,
            string? detalle = null)
        {
            bool? valorAnterior = null;
            string? estadoAnterior = null;

            using (var comando = new MySqlCommand(@"
                SELECT requiere_factura, estado_facturacion
                FROM reserva_facturacion
                WHERE id_reserva = @id_reserva
                LIMIT 1
                FOR UPDATE;", conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                using var lector = comando.ExecuteReader();

                if (lector.Read())
                {
                    valorAnterior = lector["requiere_factura"] == DBNull.Value
                        ? null
                        : Convert.ToBoolean(lector["requiere_factura"]);
                    estadoAnterior = lector["estado_facturacion"]?.ToString();
                }
            }

            string estadoNuevo = requiereFactura ? "pendiente" : "no_solicitada";
            string estadoAdministrativo = requiereFactura ? "pendiente_revision" : "cerrado";

            using (var comando = new MySqlCommand(@"
                INSERT INTO reserva_facturacion
                    (id_reserva, requiere_factura, estado_facturacion,
                     estado_administrativo, fecha_decision, id_usuario_decision,
                     id_usuario_actualizacion)
                VALUES
                    (@id_reserva, @requiere_factura, @estado_facturacion,
                     @estado_administrativo, CURRENT_TIMESTAMP, @id_usuario,
                     @id_usuario)
                ON DUPLICATE KEY UPDATE
                    requiere_factura = VALUES(requiere_factura),
                    estado_facturacion = VALUES(estado_facturacion),
                    estado_administrativo = VALUES(estado_administrativo),
                    fecha_decision = CURRENT_TIMESTAMP,
                    id_usuario_decision = VALUES(id_usuario_decision),
                    id_usuario_actualizacion = VALUES(id_usuario_actualizacion);",
                conexion, transaccion))
            {
                comando.Parameters.AddWithValue("@id_reserva", idReserva);
                comando.Parameters.AddWithValue("@requiere_factura", requiereFactura);
                comando.Parameters.AddWithValue("@estado_facturacion", estadoNuevo);
                comando.Parameters.AddWithValue("@estado_administrativo", estadoAdministrativo);
                comando.Parameters.AddWithValue("@id_usuario", idUsuario);
                comando.ExecuteNonQuery();
            }

            RegistrarHistorial(
                conexion,
                transaccion,
                idReserva,
                origen,
                valorAnterior,
                requiereFactura,
                estadoAnterior,
                estadoNuevo,
                detalle,
                idUsuario);
        }

        public void MarcarRegistrada(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            IEnumerable<int> idsReservas,
            int idUsuario,
            long idDocumentoFiscal)
        {
            foreach (int idReserva in idsReservas.Distinct())
            {
                bool? requiereAnterior = null;
                string? estadoAnterior = null;

                using (var comando = new MySqlCommand(@"
                    SELECT requiere_factura, estado_facturacion
                    FROM reserva_facturacion
                    WHERE id_reserva = @id_reserva
                    LIMIT 1
                    FOR UPDATE;", conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    using var lector = comando.ExecuteReader();

                    if (lector.Read())
                    {
                        requiereAnterior = lector["requiere_factura"] == DBNull.Value
                            ? null
                            : Convert.ToBoolean(lector["requiere_factura"]);
                        estadoAnterior = lector["estado_facturacion"]?.ToString();
                    }
                }

                using (var comando = new MySqlCommand(@"
                    INSERT INTO reserva_facturacion
                        (id_reserva, requiere_factura, estado_facturacion,
                         estado_administrativo, fecha_decision, id_usuario_decision,
                         id_usuario_actualizacion)
                    VALUES
                        (@id_reserva, 1, 'registrada', 'cerrado', CURRENT_TIMESTAMP,
                         @id_usuario, @id_usuario)
                    ON DUPLICATE KEY UPDATE
                        requiere_factura = 1,
                        estado_facturacion = 'registrada',
                        estado_administrativo = 'cerrado',
                        id_usuario_actualizacion = VALUES(id_usuario_actualizacion);",
                    conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@id_reserva", idReserva);
                    comando.Parameters.AddWithValue("@id_usuario", idUsuario);
                    comando.ExecuteNonQuery();
                }

                RegistrarHistorial(
                    conexion,
                    transaccion,
                    idReserva,
                    "documento_registrado",
                    requiereAnterior,
                    true,
                    estadoAnterior,
                    "registrada",
                    $"Documento fiscal #{idDocumentoFiscal} asociado.",
                    idUsuario);
            }
        }

        public void RegistrarHistorial(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idReserva,
            string accion,
            bool? requiereAnterior,
            bool? requiereNuevo,
            string? estadoAnterior,
            string estadoNuevo,
            string? detalle,
            int idUsuario)
        {
            using var comando = new MySqlCommand(@"
                INSERT INTO reserva_facturacion_historial
                    (id_reserva, accion, requiere_factura_anterior,
                     requiere_factura_nuevo, estado_anterior, estado_nuevo,
                     detalle, id_usuario, fecha_hora)
                VALUES
                    (@id_reserva, @accion, @requiere_anterior,
                     @requiere_nuevo, @estado_anterior, @estado_nuevo,
                     @detalle, @id_usuario, CURRENT_TIMESTAMP);",
                conexion, transaccion);
            comando.Parameters.AddWithValue("@id_reserva", idReserva);
            comando.Parameters.AddWithValue("@accion", accion);
            comando.Parameters.AddWithValue("@requiere_anterior", (object?)requiereAnterior ?? DBNull.Value);
            comando.Parameters.AddWithValue("@requiere_nuevo", (object?)requiereNuevo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@estado_anterior", (object?)estadoAnterior ?? DBNull.Value);
            comando.Parameters.AddWithValue("@estado_nuevo", estadoNuevo);
            comando.Parameters.AddWithValue("@detalle", (object?)detalle ?? DBNull.Value);
            comando.Parameters.AddWithValue("@id_usuario", idUsuario);
            comando.ExecuteNonQuery();
        }
    }
}
