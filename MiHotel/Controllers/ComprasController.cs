using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MySql.Data.MySqlClient;
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

        private IActionResult? ValidarAcceso()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario")))
                return RedirectToAction("Login", "Acceso");

            string rol = HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";
            return rol == "admin" ? null : RedirectToAction("Index", "Panel");
        }

        private void CargarCatalogos()
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            const string sqlProveedores = @"
                SELECT c.id_clipro,c.nombre
                FROM clipro c
                INNER JOIN tipo_clipro tc ON c.id_tipoclipro=tc.id_tipoclipro
                WHERE c.estado='activo' AND LOWER(tc.tipo)='proveedor'
                ORDER BY c.nombre;";
            var proveedores = new DataTable();
            new MySqlDataAdapter(sqlProveedores, conexion).Fill(proveedores);

            const string sqlProductos = @"
                SELECT p.id_proser,p.nombre_proser,p.stock
                FROM proser p
                INNER JOIN tipo_proser tp ON p.id_tipoproser=tp.id_tipoproser
                INNER JOIN tipo_estado te ON p.id_tipoestado=te.id_tipoestado
                WHERE LOWER(tp.nombre)='producto'
                  AND LOWER(te.estado)='activo'
                ORDER BY p.nombre_proser;";
            var productos = new DataTable();
            new MySqlDataAdapter(sqlProductos, conexion).Fill(productos);

            ViewBag.Proveedores = proveedores;
            ViewBag.Productos = productos;
        }

        private static int ObtenerIdCatalogo(
            MySqlConnection conexion,
            string tabla,
            string columnaId,
            string columnaNombre,
            string nombre)
        {
            string sql = $@"
                SELECT {columnaId}
                FROM {tabla}
                WHERE LOWER({columnaNombre})=@nombre
                LIMIT 1;";
            using var comando = new MySqlCommand(sql, conexion);
            comando.Parameters.AddWithValue("@nombre", nombre.ToLowerInvariant());
            object? resultado = comando.ExecuteScalar();
            if (resultado == null)
                throw new InvalidOperationException($"No existe la configuración necesaria para '{nombre}'.");
            return Convert.ToInt32(resultado);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                CargarCatalogos();
                return View();
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar los productos: " + ex.Message;
                return RedirectToAction("Index", "Panel");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Historial(
            string busqueda = "",
            string ordenarPor = "fecha_hora",
            string direccion = "desc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            const int registrosPorPagina = 10;
            pagina = Math.Max(1, pagina);

            var columnasPermitidas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fecha_hora"] = "m.fecha_hora",
                ["proveedor"] = "c.nombre",
                ["usuario"] = "u.nombre_usuario",
                ["productos"] = "cantidad_productos",
                ["unidades"] = "total_unidades"
            };

            if (!columnasPermitidas.TryGetValue(ordenarPor, out string? columnaOrden))
            {
                ordenarPor = "fecha_hora";
                columnaOrden = columnasPermitidas[ordenarPor];
            }

            direccion = direccion.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
            busqueda = busqueda?.Trim() ?? "";
            string filtroBusqueda = string.IsNullOrWhiteSpace(busqueda)
                ? ""
                : @"AND (
                        c.nombre LIKE @busqueda
                        OR u.nombre_usuario LIKE @busqueda
                        OR EXISTS (
                            SELECT 1
                            FROM detalle db
                            INNER JOIN proser pb ON pb.id_proser=db.id_proser
                            WHERE db.id_movimiento=m.id_movimiento
                              AND pb.nombre_proser LIKE @busqueda
                        )
                    )";

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string sqlTotal = $@"
                    SELECT COUNT(*)
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm ON tm.id_tipomov=m.id_tipomov
                    INNER JOIN clipro c ON c.id_clipro=m.id_clipro
                    INNER JOIN usuario u ON u.id_usuario=m.id_usuario
                    WHERE LOWER(tm.nombre_tipomov)='compra'
                    {filtroBusqueda};";

                using var comandoTotal = new MySqlCommand(sqlTotal, conexion);
                if (!string.IsNullOrWhiteSpace(busqueda))
                    comandoTotal.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");

                int totalRegistros = Convert.ToInt32(comandoTotal.ExecuteScalar());
                int totalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)registrosPorPagina));
                pagina = Math.Min(pagina, totalPaginas);
                int desplazamiento = (pagina - 1) * registrosPorPagina;

                string sql = $@"
                    SELECT
                        m.id_movimiento,
                        m.fecha_hora,
                        c.nombre AS proveedor,
                        u.nombre_usuario AS usuario,
                        COUNT(d.id_detalle) AS cantidad_productos,
                        COALESCE(SUM(d.cantidad),0) AS total_unidades
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm ON tm.id_tipomov=m.id_tipomov
                    INNER JOIN clipro c ON c.id_clipro=m.id_clipro
                    INNER JOIN usuario u ON u.id_usuario=m.id_usuario
                    INNER JOIN detalle d ON d.id_movimiento=m.id_movimiento
                    WHERE LOWER(tm.nombre_tipomov)='compra'
                    {filtroBusqueda}
                    GROUP BY m.id_movimiento,m.fecha_hora,c.nombre,u.nombre_usuario
                    ORDER BY {columnaOrden} {direccion},m.id_movimiento DESC
                    LIMIT @limite OFFSET @desplazamiento;";

                using var comando = new MySqlCommand(sql, conexion);
                if (!string.IsNullOrWhiteSpace(busqueda))
                    comando.Parameters.AddWithValue("@busqueda", $"%{busqueda}%");
                comando.Parameters.AddWithValue("@limite", registrosPorPagina);
                comando.Parameters.AddWithValue("@desplazamiento", desplazamiento);

                var historial = new DataTable();
                new MySqlDataAdapter(comando).Fill(historial);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccion;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
                return View(historial);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar el historial de compras: " + ex.Message;
                return RedirectToAction("Crear");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                const string sqlCompra = @"
                    SELECT
                        m.id_movimiento,
                        m.fecha_hora,
                        c.nombre AS proveedor,
                        u.nombre_usuario AS usuario
                    FROM movimiento m
                    INNER JOIN tipo_movimiento tm ON tm.id_tipomov=m.id_tipomov
                    INNER JOIN clipro c ON c.id_clipro=m.id_clipro
                    INNER JOIN usuario u ON u.id_usuario=m.id_usuario
                    WHERE m.id_movimiento=@id
                      AND LOWER(tm.nombre_tipomov)='compra'
                    LIMIT 1;";
                var compra = new DataTable();
                using (var comandoCompra = new MySqlCommand(sqlCompra, conexion))
                {
                    comandoCompra.Parameters.AddWithValue("@id", id);
                    new MySqlDataAdapter(comandoCompra).Fill(compra);
                }

                if (compra.Rows.Count == 0)
                {
                    TempData["Mensaje"] = "El ingreso de inventario solicitado no existe.";
                    return RedirectToAction("Historial");
                }

                const string sqlProductos = @"
                    SELECT
                        COALESCE(p.nombre_proser,d.descripcion,'Producto no disponible') AS producto,
                        d.cantidad
                    FROM detalle d
                    LEFT JOIN proser p ON p.id_proser=d.id_proser
                    WHERE d.id_movimiento=@id
                    ORDER BY producto;";
                var productosCompra = new DataTable();
                using (var comandoProductos = new MySqlCommand(sqlProductos, conexion))
                {
                    comandoProductos.Parameters.AddWithValue("@id", id);
                    new MySqlDataAdapter(comandoProductos).Fill(productosCompra);
                }

                ViewBag.ProductosCompra = productosCompra;
                return View(compra.Rows[0]);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible cargar el detalle de la compra: " + ex.Message;
                return RedirectToAction("Historial");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(int idProveedor, List<int>? idProducto, List<int>? cantidad)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            if (idProveedor <= 0)
            {
                TempData["Mensaje"] = "Seleccione un proveedor.";
                return RedirectToAction("Crear");
            }

            if (idProducto == null || cantidad == null ||
                idProducto.Count == 0 || idProducto.Count != cantidad.Count)
            {
                TempData["Mensaje"] = "Agregue al menos un producto con su cantidad.";
                return RedirectToAction("Crear");
            }

            if (idProducto.Distinct().Count() != idProducto.Count)
            {
                TempData["Mensaje"] = "Un producto no puede aparecer más de una vez en el mismo ingreso.";
                return RedirectToAction("Crear");
            }

            if (cantidad.Any(valor => valor <= 0 || valor > 999999))
            {
                TempData["Mensaje"] = "Las cantidades deben estar entre 1 y 999,999 unidades.";
                return RedirectToAction("Crear");
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoCompra = ObtenerIdCatalogo(
                    conexion, "tipo_movimiento", "id_tipomov", "nombre_tipomov", "compra");

                using var transaccion = conexion.BeginTransaction();

                using (var validarProveedor = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM clipro c
                    INNER JOIN tipo_clipro tc ON c.id_tipoclipro=tc.id_tipoclipro
                    WHERE c.id_clipro=@id
                      AND c.estado='activo'
                      AND LOWER(tc.tipo)='proveedor';", conexion, transaccion))
                {
                    validarProveedor.Parameters.AddWithValue("@id", idProveedor);
                    if (Convert.ToInt32(validarProveedor.ExecuteScalar()) == 0)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "El proveedor seleccionado no está disponible.";
                        return RedirectToAction("Crear");
                    }
                }

                for (int i = 0; i < idProducto.Count; i++)
                {
                    using var validarProducto = new MySqlCommand(@"
                        SELECT p.id_proser
                        FROM proser p
                        INNER JOIN tipo_proser tp ON p.id_tipoproser=tp.id_tipoproser
                        INNER JOIN tipo_estado te ON p.id_tipoestado=te.id_tipoestado
                        WHERE p.id_proser=@id
                          AND LOWER(tp.nombre)='producto'
                          AND LOWER(te.estado)='activo'
                        LIMIT 1
                        FOR UPDATE;", conexion, transaccion);
                    validarProducto.Parameters.AddWithValue("@id", idProducto[i]);
                    if (validarProducto.ExecuteScalar() == null)
                    {
                        transaccion.Rollback();
                        TempData["Mensaje"] = "Uno de los productos seleccionados ya no está disponible.";
                        return RedirectToAction("Crear");
                    }
                }

                int idUsuario = Convert.ToInt32(HttpContext.Session.GetString("IdUsuario"));
                int idMovimiento;
                using (var insertarMovimiento = new MySqlCommand(@"
                    INSERT INTO movimiento
                        (id_usuario,id_clipro,id_tipomov,id_formapago,id_reserva,fecha_hora,estado,observaciones)
                    VALUES
                        (@usuario,@proveedor,@tipo,NULL,NULL,CURRENT_TIMESTAMP,'activo',
                         'Ingreso de existencias desde Compras');
                    SELECT LAST_INSERT_ID();", conexion, transaccion))
                {
                    insertarMovimiento.Parameters.AddWithValue("@usuario", idUsuario);
                    insertarMovimiento.Parameters.AddWithValue("@proveedor", idProveedor);
                    insertarMovimiento.Parameters.AddWithValue("@tipo", idTipoCompra);
                    idMovimiento = Convert.ToInt32(insertarMovimiento.ExecuteScalar());
                }

                for (int i = 0; i < idProducto.Count; i++)
                {
                    using (var insertarDetalle = new MySqlCommand(@"
                        INSERT INTO detalle
                            (id_movimiento,id_proser,cantidad,precio_unitario,subtotal,descripcion)
                        VALUES
                            (@movimiento,@producto,@cantidad,0,0,@descripcion);", conexion, transaccion))
                    {
                        insertarDetalle.Parameters.AddWithValue("@movimiento", idMovimiento);
                        insertarDetalle.Parameters.AddWithValue("@producto", idProducto[i]);
                        insertarDetalle.Parameters.AddWithValue("@cantidad", cantidad[i]);
                        insertarDetalle.Parameters.AddWithValue(
                            "@descripcion",
                            $"Ingreso de {cantidad[i]} unidades al inventario");
                        insertarDetalle.ExecuteNonQuery();
                    }

                    using var actualizarStock = new MySqlCommand(@"
                        UPDATE proser
                        SET stock=stock+@cantidad
                        WHERE id_proser=@producto;", conexion, transaccion);
                    actualizarStock.Parameters.AddWithValue("@cantidad", cantidad[i]);
                    actualizarStock.Parameters.AddWithValue("@producto", idProducto[i]);
                    actualizarStock.ExecuteNonQuery();
                }

                transaccion.Commit();
                TempData["Exito"] = "Existencias actualizadas correctamente.";
                return RedirectToAction("Historial");
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No fue posible actualizar las existencias: " + ex.Message;
            }

            return RedirectToAction("Crear");
        }
    }
}
