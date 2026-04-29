using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MiHotel.Data;
using System.Data;

namespace MiHotel.Controllers
{
    public class ComprasController : Controller
    {
        private readonly ConexionBD _conexionBD;

        public ComprasController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ============================
        // VALIDAR SESIÓN
        // ============================
        private IActionResult? ValidarSesion()
        {
            if (HttpContext.Session.GetString("IdUsuario") == null)
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        // ============================
        // VISTA CREAR COMPRA
        // ============================
        public IActionResult Crear()
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            // PROVEEDORES
            string sqlProv = @"
                SELECT id_clipro, nombre 
                FROM clipro
                WHERE estado='activo'
                AND id_tipoclipro = (
                    SELECT id_tipoclipro 
                    FROM tipo_clipro 
                    WHERE LOWER(tipo)='proveedor'
                    LIMIT 1
                )";

            var daProv = new MySqlDataAdapter(sqlProv, conexion);
            var dtProv = new DataTable();
            daProv.Fill(dtProv);

            // PRODUCTOS (SOLO PRODUCTOS, NO SERVICIOS)
            string sqlProd = @"
                SELECT id_proser, nombre_proser, precio, stock
                FROM proser
                WHERE id_tipoproser = (
                    SELECT id_tipoproser 
                    FROM tipo_proser 
                    WHERE LOWER(nombre)='producto'
                    LIMIT 1
                )";

            var daProd = new MySqlDataAdapter(sqlProd, conexion);
            var dtProd = new DataTable();
            daProd.Fill(dtProd);

            ViewBag.Proveedores = dtProv;
            ViewBag.Productos = dtProd;

            return View();
        }

        // ============================
        // GUARDAR COMPRA
        // ============================
        [HttpPost]
        public IActionResult Crear(int idProveedor, List<int> idProducto, List<int> cantidad, List<decimal> precio)
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            using var transaccion = conexion.BeginTransaction();

            try
            {
                int idUsuario = Convert.ToInt32(HttpContext.Session.GetString("IdUsuario"));

                // ============================
                // INSERTAR MOVIMIENTO (COMPRA)
                // ============================
                string sqlMov = @"
                    INSERT INTO movimiento (id_tipomov, id_usuario, id_clipro, fecha)
                    VALUES (2, @usuario, @proveedor, NOW());
                    SELECT LAST_INSERT_ID();";

                int idMovimiento;

                using (var cmdMov = new MySqlCommand(sqlMov, conexion, transaccion))
                {
                    cmdMov.Parameters.AddWithValue("@usuario", idUsuario);
                    cmdMov.Parameters.AddWithValue("@proveedor", idProveedor);

                    idMovimiento = Convert.ToInt32(cmdMov.ExecuteScalar());
                }

                // ============================
                // INSERTAR DETALLE + ACTUALIZAR STOCK
                // ============================
                for (int i = 0; i < idProducto.Count; i++)
                {
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

                    // STOCK
                    string sqlStock = @"
                        UPDATE proser
                        SET stock = stock + @cant
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