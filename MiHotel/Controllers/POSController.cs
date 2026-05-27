using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MiHotel.Data;
using System.Text.Json;
using System.Data;

namespace MiHotel.Controllers
{
    public class POSController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public POSController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarSesion()
        {
            if (HttpContext.Session.GetString("IdUsuario") == null)
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        public IActionResult Index()
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            var dtProd = new DataTable();
            new MySqlDataAdapter(@"
            SELECT 
            p.id_proser,
            p.nombre_proser,
            p.precio,
            p.stock,
            tp.nombre AS tipo
            FROM proser p
            INNER JOIN tipo_proser tp
                ON p.id_tipoproser = tp.id_tipoproser
            WHERE LOWER(tp.nombre) IN ('producto', 'servicio')", conexion).Fill(dtProd);

            var dtCli = new DataTable();
            new MySqlDataAdapter("SELECT id_clipro, nombre FROM clipro WHERE estado='activo'", conexion).Fill(dtCli);

                        var dtRes = new DataTable();

                        new MySqlDataAdapter(@"
            SELECT 
                r.id_reserva,
                r.id_clipro,
                r.id_habitacion,
                c.nombre AS cliente,
                h.nombre_proser AS habitacion,
                r.fecha_entrada,
                r.fecha_salida
            FROM reserva r
            INNER JOIN clipro c
                ON r.id_clipro = c.id_clipro
            INNER JOIN proser h
                ON r.id_habitacion = h.id_proser
            INNER JOIN tipo_proser tp
                ON h.id_tipoproser = tp.id_tipoproser
            WHERE r.estado = 'en_curso'
            AND LOWER(tp.nombre) = 'habitacion'
            ", conexion).Fill(dtRes);

            ViewBag.Productos = dtProd;
            ViewBag.Clientes = dtCli;
            ViewBag.Reservas = dtRes;

            return View();
        }

        [HttpPost]
        public IActionResult GuardarVenta(string detalleJson, int? id_clipro, int? id_reserva, decimal montoPagado)
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            if (string.IsNullOrEmpty(detalleJson))
                return RedirectToAction("Index");

            var carrito = JsonSerializer.Deserialize<List<ItemCarrito>>(detalleJson);

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            using var tx = conexion.BeginTransaction();

            try
            {
                int idClienteFinal;

                if (id_reserva.HasValue)
                {
                    var cmdReserva = new MySqlCommand(@"
        SELECT id_clipro
        FROM reserva
        WHERE id_reserva = @id
        AND estado = 'en_curso'
        LIMIT 1", conexion, tx);

                    cmdReserva.Parameters.AddWithValue("@id", id_reserva.Value);

                    var resultado = cmdReserva.ExecuteScalar();

                    if (resultado == null)
                    {
                        TempData["Error"] = "La reservación seleccionada no es válida.";
                        return RedirectToAction("Index");
                    }

                    int clienteReserva = Convert.ToInt32(resultado);

                    // VALIDAR CLIENTE ↔ RESERVA
                    if (id_clipro.HasValue && clienteReserva != id_clipro.Value)
                    {
                        TempData["Error"] = "La reservación no pertenece al cliente seleccionado.";
                        return RedirectToAction("Index");
                    }

                    idClienteFinal = clienteReserva;
                }
                else
                {
                    idClienteFinal = id_clipro ?? ObtenerClienteGenerico(conexion, tx);
                }

                int idUsuario = Convert.ToInt32(HttpContext.Session.GetString("IdUsuario"));
                int idTipoVenta = ObtenerTipo(conexion, tx, "venta");
                int idTipoCxC = ObtenerTipo(conexion, tx, "cuenta_por_cobrar");
                int idFormaPagoEfectivo = 1;
                int idFormaPagoCredito = 5;

                decimal total = carrito.Sum(x => x.precio * x.cantidad);

                // 🔹 MOVIMIENTO VENTA
                int idMovimientoVenta = InsertarMovimiento(conexion, tx, idTipoVenta, idClienteFinal, id_reserva, idUsuario, idFormaPagoEfectivo);
                foreach (var item in carrito)
                {
                    InsertarDetalle(conexion, tx, idMovimientoVenta, item);

                    var cmdTipo = new MySqlCommand(@"
    SELECT LOWER(tp.nombre)
    FROM proser p
    INNER JOIN tipo_proser tp
        ON p.id_tipoproser = tp.id_tipoproser
    WHERE p.id_proser = @id
    LIMIT 1", conexion, tx);

                    cmdTipo.Parameters.AddWithValue("@id", item.id);

                    string tipo = Convert.ToString(cmdTipo.ExecuteScalar()) ?? "";

                    if (tipo == "producto")
                    {
                        new MySqlCommand(
                            "UPDATE proser SET stock = stock - @c WHERE id_proser=@id",
                            conexion, tx)
                        {
                            Parameters =
        {
            new("@c", item.cantidad),
            new("@id", item.id)
        }
                        }.ExecuteNonQuery();
                    }


                }

                // 🔹 PAGO PARCIAL (si aplica)
                if (montoPagado > 0)
                {
                    int idTipoPago = ObtenerTipo(conexion, tx, "reserva");

                    int idMovPago = InsertarMovimiento(conexion, tx, idTipoPago, idClienteFinal, id_reserva, idUsuario, idFormaPagoEfectivo);

                    new MySqlCommand(@"
                        INSERT INTO detalle (id_movimiento, cantidad, precio_unitario, subtotal)
                        VALUES (@mov,1,@p,@p)", conexion, tx)
                    {
                        Parameters =
                        {
                            new("@mov", idMovPago),
                            new("@p", montoPagado)
                        }
                    }.ExecuteNonQuery();
                }

                // 🔹 CUENTA POR COBRAR
                decimal restante = total - montoPagado;

                if (restante > 0)
                {
                    int idMovCxC = InsertarMovimiento(conexion, tx, idTipoCxC, idClienteFinal, id_reserva, idUsuario, idFormaPagoCredito);

                    new MySqlCommand(@"
                        INSERT INTO detalle (id_movimiento, cantidad, precio_unitario, subtotal)
                        VALUES (@mov,1,@r,@r)", conexion, tx)
                    {
                        Parameters =
                        {
                            new("@mov", idMovCxC),
                            new("@r", restante)
                        }
                    }.ExecuteNonQuery();
                }

                tx.Commit();

                TempData["Exito"] = "La venta fue registrada correctamente.";

                return RedirectToAction("Index");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private int InsertarMovimiento(MySqlConnection c, MySqlTransaction tx,
        int tipo, int cliente, int? reserva, int idUsuario, int idFormaPago)
        {
            var cmd = new MySqlCommand(@"
            INSERT INTO movimiento
            (id_tipomov,id_clipro,id_reserva,id_usuario,id_formapago,fecha_hora,estado)
            VALUES
            (@t,@c,@r,@u,@f,NOW(),'activo');

            SELECT LAST_INSERT_ID();", c, tx);

            cmd.Parameters.AddWithValue("@t", tipo);
            cmd.Parameters.AddWithValue("@c", cliente);
            cmd.Parameters.AddWithValue("@r", (object?)reserva ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", idUsuario);
            cmd.Parameters.AddWithValue("@f", idFormaPago);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private void InsertarDetalle(MySqlConnection c, MySqlTransaction tx, int mov, ItemCarrito item)
        {
            var cmd = new MySqlCommand(@"
        INSERT INTO detalle
        (id_movimiento,id_proser,cantidad,precio_unitario,subtotal)
        VALUES
        (@m,@p,@c,@pr,@s)", c, tx);

            cmd.Parameters.AddWithValue("@m", mov);
            cmd.Parameters.AddWithValue("@p", item.id);
            cmd.Parameters.AddWithValue("@c", item.cantidad);
            cmd.Parameters.AddWithValue("@pr", item.precio);
            cmd.Parameters.AddWithValue("@s", item.precio * item.cantidad);

            cmd.ExecuteNonQuery();
        }

        private int ObtenerTipo(MySqlConnection c, MySqlTransaction tx, string nombre)
        {
            var cmd = new MySqlCommand("SELECT id_tipomov FROM tipo_movimiento WHERE LOWER(nombre_tipomov)=@n LIMIT 1", c, tx);
            cmd.Parameters.AddWithValue("@n", nombre);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private int ObtenerClienteGenerico(MySqlConnection c, MySqlTransaction tx)
        {
            var cmd = new MySqlCommand("SELECT id_clipro FROM clipro WHERE nombre='CLIENTE GENERAL' LIMIT 1", c, tx);
            var r = cmd.ExecuteScalar();

            if (r != null) return Convert.ToInt32(r);

            return Convert.ToInt32(new MySqlCommand(
                "INSERT INTO clipro(nombre,estado) VALUES('CLIENTE GENERAL','activo'); SELECT LAST_INSERT_ID();",
                c, tx).ExecuteScalar());
        }

        public class ItemCarrito
        {
            public int id { get; set; }
            public decimal precio { get; set; }
            public int cantidad { get; set; }
        }
    }
}


