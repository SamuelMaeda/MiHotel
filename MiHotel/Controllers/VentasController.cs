using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MiHotel.Data;
using System.Data;

namespace MiHotel.Controllers
{
    public class VentasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public VentasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private IActionResult? ValidarSesion()
        {
            if (HttpContext.Session.GetString("IdUsuario") == null)
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        // ============================
        // VISTA VENTA
        // ============================
        public IActionResult Crear()
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sqlProd = @"
                SELECT id_proser, nombre_proser, precio, stock
                FROM proser
                WHERE id_tipoproser = (
                    SELECT id_tipoproser 
                    FROM tipo_proser 
                    WHERE LOWER(nombre)='producto'
                    LIMIT 1
                )";

            var da = new MySqlDataAdapter(sqlProd, conexion);
            var dt = new DataTable();
            da.Fill(dt);

            ViewBag.Productos = dt;

            return View();
        }

        // ============================
        // GUARDAR VENTA
        // ============================
        [HttpPost]
        public IActionResult Crear(List<int> idProducto, List<int> cantidad, List<decimal> precio)
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            using var transaccion = conexion.BeginTransaction();

            try
            {
                int idUsuario = Convert.ToInt32(HttpContext.Session.GetString("IdUsuario"));

                // MOVIMIENTO (VENTA)
                string sqlMov = @"
                    INSERT INTO movimiento (id_tipomov, id_usuario, fecha)
                    VALUES (1, @usuario, NOW());
                    SELECT LAST_INSERT_ID();";

                int idMovimiento;

                using (var cmdMov = new MySqlCommand(sqlMov, conexion, transaccion))
                {
                    cmdMov.Parameters.AddWithValue("@usuario", idUsuario);
                    idMovimiento = Convert.ToInt32(cmdMov.ExecuteScalar());
                }

                // DETALLE + STOCK
                for (int i = 0; i < idProducto.Count; i++)
                {
                    // VALIDAR STOCK
                    string sqlCheck = "SELECT stock FROM proser WHERE id_proser=@id";
                    int stockActual;

                    using (var cmdCheck = new MySqlCommand(sqlCheck, conexion, transaccion))
                    {
                        cmdCheck.Parameters.AddWithValue("@id", idProducto[i]);
                        stockActual = Convert.ToInt32(cmdCheck.ExecuteScalar());
                    }

                    if (cantidad[i] > stockActual)
                        throw new Exception("Stock insuficiente");

                    // DETALLE
                    string sqlDet = @"
                        INSERT INTO detalle (id_movimiento, id_proser, cantidad, precio_unitario)
                        VALUES (@mov, @prod, @cant, @precio)";

                    using (var cmdDet = new MySqlCommand(sqlDet, conexion, transaccion))
                    {
                        cmdDet.Parameters.AddWithValue("@mov", idMovimiento);
                        cmdDet.Parameters.AddWithValue("@prod", idProducto[i]);
                        cmdDet.Parameters.AddWithValue("@cant", cantidad[i]);
                        cmdDet.Parameters.AddWithValue("@precio", precio[i]);

                        cmdDet.ExecuteNonQuery();
                    }

                    // RESTAR STOCK
                    string sqlStock = @"
                        UPDATE proser
                        SET stock = stock - @cant
                        WHERE id_proser = @prod";

                    using (var cmdStock = new MySqlCommand(sqlStock, conexion, transaccion))
                    {
                        cmdStock.Parameters.AddWithValue("@cant", cantidad[i]);
                        cmdStock.Parameters.AddWithValue("@prod", idProducto[i]);

                        cmdStock.ExecuteNonQuery();
                    }
                }

                transaccion.Commit();

                return RedirectToAction("Crear");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
    }
}