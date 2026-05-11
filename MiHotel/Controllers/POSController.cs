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
            SELECT id_proser, nombre_proser, precio, stock
            FROM proser
            WHERE id_tipoproser = (
                SELECT id_tipoproser 
                FROM tipo_proser 
                WHERE LOWER(nombre)='producto' 
                LIMIT 1
            )", conexion).Fill(dtProd);

            var dtCli = new DataTable();
            new MySqlDataAdapter("SELECT id_clipro, nombre FROM clipro WHERE estado='activo'", conexion).Fill(dtCli);

            var dtRes = new DataTable();
            new MySqlDataAdapter(@"
            SELECT 
                id_reserva,
                id_clipro,
                fecha_entrada,
                fecha_salida
            FROM reserva
            WHERE estado IN ('confirmada','en_curso')", conexion).Fill(dtRes);

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
                    var cmd = new MySqlCommand("SELECT id_clipro FROM reserva WHERE id_reserva=@id", conexion, tx);
                    cmd.Parameters.AddWithValue("@id", id_reserva.Value);
                    idClienteFinal = Convert.ToInt32(cmd.ExecuteScalar());
                }
                else
                {
                    idClienteFinal = id_clipro ?? ObtenerClienteGenerico(conexion, tx);
                }

                int idTipoVenta = ObtenerTipo(conexion, tx, "venta");
                int idTipoCxC = ObtenerTipo(conexion, tx, "cuenta_por_cobrar");

                decimal total = carrito.Sum(x => x.precio * x.cantidad);

                // 🔹 MOVIMIENTO VENTA
                int idMovimientoVenta = InsertarMovimiento(conexion, tx, idTipoVenta, idClienteFinal, id_reserva);

                foreach (var item in carrito)
                {
                    InsertarDetalle(conexion, tx, idMovimientoVenta, item);

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

                // 🔹 PAGO PARCIAL (si aplica)
                if (montoPagado > 0)
                {
                    int idTipoPago = ObtenerTipo(conexion, tx, "reserva");

                    int idMovPago = InsertarMovimiento(conexion, tx, idTipoPago, idClienteFinal, id_reserva);

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
                    int idMovCxC = InsertarMovimiento(conexion, tx, idTipoCxC, idClienteFinal, id_reserva);

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
                return RedirectToAction("Index");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private int InsertarMovimiento(MySqlConnection c, MySqlTransaction tx, int tipo, int cliente, int? reserva)
        {
            var cmd = new MySqlCommand(@"
                INSERT INTO movimiento (id_tipomov,id_clipro,id_reserva,fecha,estado)
                VALUES (@t,@c,@r,NOW(),'activo');
                SELECT LAST_INSERT_ID();", c, tx);

            cmd.Parameters.AddWithValue("@t", tipo);
            cmd.Parameters.AddWithValue("@c", cliente);
            cmd.Parameters.AddWithValue("@r", (object?)reserva ?? DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private void InsertarDetalle(MySqlConnection c, MySqlTransaction tx, int mov, ItemCarrito item)
        {
            var cmd = new MySqlCommand(@"
                INSERT INTO detalle (id_movimiento,id_proser,cantidad,precio_unitario,subtotal)
                VALUES (@m,@p,@c,@pr,@s)", c, tx);

            cmd.Parameters.AddWithValue("@m", mov);
            cmd.Parameters.AddWithValue("@p", item.id);
            cmd.Parameters.AddWithValue("@c", item.cantidad);
            cmd.Parameters.AddWithValue("@pr", item.precio);
            cmd.Parameters.AddWithValue("@s", item.precio * item.cantidad);

            cmd.ExecuteNonQuery();
        }

        private int ObtenerTipo(MySqlConnection c, MySqlTransaction tx, string nombre)
        {
            var cmd = new MySqlCommand("SELECT id_tipomov FROM tipo_movimiento WHERE LOWER(nombre)=@n LIMIT 1", c, tx);
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