using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ProductosServiciosController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public ProductosServiciosController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ===============================
        // VALIDAR SESION ACTIVA
        // ===============================
        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrWhiteSpace(idUsuario);
        }

        // ===============================
        // VALIDAR SESION
        // ===============================
        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        // ===============================
        // FORMATEAR TEXTO PARA MOSTRAR
        // ===============================
        private string FormatearTextoPresentacion(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return texto;
            }

            string textoLimpio = texto.Replace("_", " ").Trim().ToLower();
            return char.ToUpper(textoLimpio[0]) + textoLimpio.Substring(1);
        }

        // ===============================
        // OBTENER COLUMNA DE ORDEN SEGURA
        // ===============================
        private string ObtenerColumnaOrden(string columna)
        {
            return columna.Trim().ToLower() switch
            {
                "nombre" => "p.nombre_proser",
                "tipo" => "tp.nombre",
                "precio" => "p.precio",
                "stock" => "p.stock",
                "estado" => "te.estado",
                _ => "p.codigo"
            };
        }

        // ===============================
        // CONDICION GENERAL DE BUSQUEDA
        // ===============================
        private string ObtenerCondicionBusquedaGeneral()
        {
            return @"
                (
                    p.codigo LIKE @busqueda
                    OR p.nombre_proser LIKE @busqueda
                    OR tp.nombre LIKE @busqueda
                    OR te.estado LIKE @busqueda
                    OR p.descripcion LIKE @busqueda
                )";
        }

        // ===============================
        // OBTENER ID DE UNIDAD SERVICIO
        // ===============================
        private int ObtenerIdUnidadServicio(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_umedida
                FROM unidad_medida
                WHERE LOWER(nombre) = 'servicio'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe la unidad de medida 'servicio'.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // OBTENER NOMBRE DE TIPO PROSER
        // ===============================
        private string ObtenerNombreTipoProser(MySqlConnection conexion, int idTipoProser)
        {
            string consulta = @"
                SELECT nombre
                FROM tipo_proser
                WHERE id_tipoproser = @id
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id", idTipoProser);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No se encontró el tipo de producto o servicio.");
            }

            return resultado.ToString()?.Trim().ToLower() ?? "";
        }

        // ===============================
        // GENERAR CODIGO AUTOMATICO
        // ===============================
        private string GenerarCodigoAutomatico(MySqlConnection conexion, int idTipoProser)
        {
            string nombreTipo = ObtenerNombreTipoProser(conexion, idTipoProser);
            string prefijo = nombreTipo == "servicio" ? "SER" : "PRO";

            string consulta = @"
                SELECT codigo
                FROM proser
                WHERE id_tipoproser = @id_tipoproser
                  AND codigo LIKE @prefijo
                ORDER BY id_proser DESC
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id_tipoproser", idTipoProser);
            comando.Parameters.AddWithValue("@prefijo", prefijo + "%");

            object? resultado = comando.ExecuteScalar();

            int correlativo = 1;

            if (resultado != null)
            {
                string codigoActual = resultado.ToString() ?? "";

                if (codigoActual.StartsWith(prefijo) && codigoActual.Length > prefijo.Length)
                {
                    string numeroTexto = codigoActual.Substring(prefijo.Length);

                    if (int.TryParse(numeroTexto, out int numeroActual))
                    {
                        correlativo = numeroActual + 1;
                    }
                }
            }

            return prefijo + correlativo.ToString("D4");
        }

        // ===============================
        // VALIDAR MODELO SEGUN TIPO
        // ===============================
        private void ValidarSegunTipo(ProductoServicioFormViewModel modelo)
        {
            if (modelo.IdTipoProser <= 0)
            {
                return;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string nombreTipo = ObtenerNombreTipoProser(conexion, modelo.IdTipoProser);
                modelo.EsServicio = nombreTipo == "servicio";

                if (modelo.EsServicio)
                {
                    modelo.IdCategoria = null;
                    modelo.IdSubcategoria = null;
                    modelo.IdMarca = null;
                    modelo.Stock = 0;
                    modelo.IdUnidadMedida = null;
                }
                else
                {
                    if (!modelo.IdUnidadMedida.HasValue || modelo.IdUnidadMedida.Value <= 0)
                    {
                        ModelState.AddModelError("IdUnidadMedida", "Debe seleccionar la unidad de medida.");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ocurrió un error al validar el tipo: " + ex.Message);
            }
        }

        // ===============================
        // LISTADO
        // ===============================
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "codigo",
            string direccion = "asc",
            string vista = "activos",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tabla = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string vistaNormalizada = vista.Trim().ToLower() == "inactivos" ? "inactivos" : "activos";
                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";
                string filtroBusqueda = "";
                string condicionFiltro = ObtenerCondicionBusquedaGeneral();

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string condicionEstado = vistaNormalizada == "inactivos"
                    ? "LOWER(te.estado) = 'inactivo'"
                    : "LOWER(te.estado) = 'activo'";

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    filtroBusqueda = $" AND {condicionFiltro} ";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM proser p
                    INNER JOIN tipo_proser tp ON p.id_tipoproser = tp.id_tipoproser
                    INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                    WHERE LOWER(tp.nombre) IN ('producto', 'servicio')
                      AND {condicionEstado}
                      {filtroBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comandoConteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                int totalRegistros = Convert.ToInt32(comandoConteo.ExecuteScalar());
                int totalPaginas = (int)Math.Ceiling((double)totalRegistros / RegistrosPorPagina);

                if (totalPaginas == 0)
                {
                    totalPaginas = 1;
                }

                if (pagina > totalPaginas)
                {
                    pagina = totalPaginas;
                }

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = $@"
                    SELECT
                        p.id_proser,
                        p.codigo,
                        p.nombre_proser,
                        tp.nombre AS tipo,
                        p.precio,
                        p.stock,
                        te.estado
                    FROM proser p
                    INNER JOIN tipo_proser tp ON p.id_tipoproser = tp.id_tipoproser
                    INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                    WHERE LOWER(tp.nombre) IN ('producto', 'servicio')
                      AND {condicionEstado}
                      {filtroBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tabla);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar los productos y servicios: " + ex.Message;
            }

            return View(tabla);
        }

        // ===============================
        // DETALLE
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        p.id_proser,
                        p.codigo,
                        p.nombre_proser,
                        tp.nombre AS tipo,
                        c.nombre_categoria,
                        s.nombre_subcategoria,
                        m.nombre_marca,
                        u.nombre AS unidad_medida,
                        p.precio,
                        p.stock,
                        te.estado,
                        p.descripcion
                    FROM proser p
                    INNER JOIN tipo_proser tp ON p.id_tipoproser = tp.id_tipoproser
                    INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                    LEFT JOIN categoria c ON p.id_categoria = c.id_categoria
                    LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                    LEFT JOIN marca m ON p.id_marca = m.id_marca
                    LEFT JOIN unidad_medida u ON p.id_umedida = u.id_umedida
                    WHERE p.id_proser = @id
                      AND LOWER(tp.nombre) IN ('producto', 'servicio')
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el producto o servicio solicitado.";
                    return RedirectToAction("Index");
                }

                string tipo = lector["tipo"]?.ToString() ?? "";
                bool esServicio = tipo.Trim().ToLower() == "servicio";

                ProductoServicioDetalleViewModel modelo = new ProductoServicioDetalleViewModel
                {
                    IdProser = Convert.ToInt32(lector["id_proser"]),
                    Codigo = lector["codigo"]?.ToString() ?? "",
                    NombreProser = lector["nombre_proser"]?.ToString() ?? "",
                    Tipo = FormatearTextoPresentacion(tipo),
                    Categoria = lector["nombre_categoria"] == DBNull.Value ? null : FormatearTextoPresentacion(lector["nombre_categoria"]?.ToString() ?? ""),
                    Subcategoria = lector["nombre_subcategoria"] == DBNull.Value ? null : FormatearTextoPresentacion(lector["nombre_subcategoria"]?.ToString() ?? ""),
                    Marca = lector["nombre_marca"] == DBNull.Value ? null : FormatearTextoPresentacion(lector["nombre_marca"]?.ToString() ?? ""),
                    UnidadMedida = lector["unidad_medida"] == DBNull.Value ? null : FormatearTextoPresentacion(lector["unidad_medida"]?.ToString() ?? ""),
                    Precio = Convert.ToDecimal(lector["precio"]),
                    Stock = Convert.ToInt32(lector["stock"]),
                    Estado = FormatearTextoPresentacion(lector["estado"]?.ToString() ?? ""),
                    Descripcion = lector["descripcion"] == DBNull.Value ? null : lector["descripcion"]?.ToString(),
                    EsServicio = esServicio
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ===============================
        // CREAR
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            var modelo = new ProductoServicioFormViewModel();

            CargarCombos(modelo);
            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(ProductoServicioFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            ValidarSegunTipo(modelo);

            if (!ModelState.IsValid)
            {
                CargarCombos(modelo);
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string nombreTipo = ObtenerNombreTipoProser(conexion, modelo.IdTipoProser);
                bool esServicio = nombreTipo == "servicio";

                string codigoGenerado = GenerarCodigoAutomatico(conexion, modelo.IdTipoProser);

                int? idUnidadFinal = esServicio
                    ? ObtenerIdUnidadServicio(conexion)
                    : modelo.IdUnidadMedida;

                string insertar = @"
                    INSERT INTO proser
                    (
                        id_categoria,
                        id_subcategoria,
                        id_marca,
                        id_umedida,
                        id_tipoestado,
                        id_tipoproser,
                        codigo,
                        nombre_proser,
                        precio,
                        stock,
                        descripcion
                    )
                    VALUES
                    (
                        @id_categoria,
                        @id_subcategoria,
                        @id_marca,
                        @id_umedida,
                        @id_tipoestado,
                        @id_tipoproser,
                        @codigo,
                        @nombre_proser,
                        @precio,
                        @stock,
                        @descripcion
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_categoria", esServicio ? DBNull.Value : (object?)modelo.IdCategoria ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@id_subcategoria", esServicio ? DBNull.Value : (object?)modelo.IdSubcategoria ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@id_marca", esServicio ? DBNull.Value : (object?)modelo.IdMarca ?? DBNull.Value);
                comandoInsertar.Parameters.AddWithValue("@id_umedida", idUnidadFinal ?? throw new Exception("No se pudo determinar la unidad de medida."));
                comandoInsertar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoInsertar.Parameters.AddWithValue("@id_tipoproser", modelo.IdTipoProser);
                comandoInsertar.Parameters.AddWithValue("@codigo", codigoGenerado);
                comandoInsertar.Parameters.AddWithValue("@nombre_proser", modelo.NombreProser.Trim());
                comandoInsertar.Parameters.AddWithValue("@precio", modelo.Precio);
                comandoInsertar.Parameters.AddWithValue("@stock", esServicio ? 0 : modelo.Stock);
                comandoInsertar.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(modelo.Descripcion) ? DBNull.Value : modelo.Descripcion.Trim());

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Producto o servicio creado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al crear el producto o servicio: " + ex.Message;
                CargarCombos(modelo);
                return View(modelo);
            }
        }

        // ===============================
        // EDITAR
        // ===============================
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        p.id_proser,
                        p.id_categoria,
                        p.id_subcategoria,
                        p.id_marca,
                        p.id_umedida,
                        p.id_tipoestado,
                        p.id_tipoproser,
                        p.codigo,
                        p.nombre_proser,
                        p.precio,
                        p.stock,
                        p.descripcion,
                        tp.nombre AS tipo_nombre
                    FROM proser p
                    INNER JOIN tipo_proser tp ON p.id_tipoproser = tp.id_tipoproser
                    WHERE p.id_proser = @id
                      AND LOWER(tp.nombre) IN ('producto', 'servicio')
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el producto o servicio solicitado.";
                    return RedirectToAction("Index");
                }

                ProductoServicioFormViewModel modelo = new ProductoServicioFormViewModel
                {
                    IdProser = Convert.ToInt32(lector["id_proser"]),
                    IdCategoria = lector["id_categoria"] == DBNull.Value ? null : Convert.ToInt32(lector["id_categoria"]),
                    IdSubcategoria = lector["id_subcategoria"] == DBNull.Value ? null : Convert.ToInt32(lector["id_subcategoria"]),
                    IdMarca = lector["id_marca"] == DBNull.Value ? null : Convert.ToInt32(lector["id_marca"]),
                    IdUnidadMedida = lector["id_umedida"] == DBNull.Value ? null : Convert.ToInt32(lector["id_umedida"]),
                    IdTipoEstado = Convert.ToInt32(lector["id_tipoestado"]),
                    IdTipoProser = Convert.ToInt32(lector["id_tipoproser"]),
                    Codigo = lector["codigo"]?.ToString() ?? "",
                    NombreProser = lector["nombre_proser"]?.ToString() ?? "",
                    Precio = Convert.ToDecimal(lector["precio"]),
                    Stock = Convert.ToInt32(lector["stock"]),
                    Descripcion = lector["descripcion"] == DBNull.Value ? null : lector["descripcion"].ToString(),
                    EsServicio = (lector["tipo_nombre"]?.ToString()?.Trim().ToLower() ?? "") == "servicio"
                };

                CargarCombos(modelo);
                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el producto o servicio: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(ProductoServicioFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            ValidarSegunTipo(modelo);

            if (!ModelState.IsValid)
            {
                CargarCombos(modelo);
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string nombreTipo = ObtenerNombreTipoProser(conexion, modelo.IdTipoProser);
                bool esServicio = nombreTipo == "servicio";

                string actualizar;

                if (esServicio)
                {
                    actualizar = @"
                        UPDATE proser
                        SET nombre_proser = @nombre_proser,
                            precio = @precio,
                            descripcion = @descripcion,
                            id_tipoestado = @id_tipoestado
                        WHERE id_proser = @id_proser;";
                }
                else
                {
                    actualizar = @"
                        UPDATE proser
                        SET id_categoria = @id_categoria,
                            id_subcategoria = @id_subcategoria,
                            id_marca = @id_marca,
                            id_umedida = @id_umedida,
                            id_tipoestado = @id_tipoestado,
                            nombre_proser = @nombre_proser,
                            precio = @precio,
                            descripcion = @descripcion
                        WHERE id_proser = @id_proser;";
                }

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);

                if (!esServicio)
                {
                    comandoActualizar.Parameters.AddWithValue("@id_categoria", (object?)modelo.IdCategoria ?? DBNull.Value);
                    comandoActualizar.Parameters.AddWithValue("@id_subcategoria", (object?)modelo.IdSubcategoria ?? DBNull.Value);
                    comandoActualizar.Parameters.AddWithValue("@id_marca", (object?)modelo.IdMarca ?? DBNull.Value);
                    comandoActualizar.Parameters.AddWithValue("@id_umedida", modelo.IdUnidadMedida ?? throw new Exception("Debe seleccionar la unidad de medida."));
                }

                comandoActualizar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoActualizar.Parameters.AddWithValue("@nombre_proser", modelo.NombreProser.Trim());
                comandoActualizar.Parameters.AddWithValue("@precio", modelo.Precio);
                comandoActualizar.Parameters.AddWithValue("@descripcion", string.IsNullOrWhiteSpace(modelo.Descripcion) ? DBNull.Value : modelo.Descripcion.Trim());
                comandoActualizar.Parameters.AddWithValue("@id_proser", modelo.IdProser);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Producto o servicio actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el producto o servicio: " + ex.Message;
                CargarCombos(modelo);
                return View(modelo);
            }
        }

        // ===============================
        // CAMBIAR ESTADO
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CambiarEstado(
            int id,
            string vista = "activos",
            string busqueda = "",
            string ordenarPor = "codigo",
            string direccion = "asc",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string nombreEstadoDestino = vista.Trim().ToLower() == "inactivos" ? "activo" : "inactivo";

                string obtenerEstado = @"
                    SELECT id_tipoestado
                    FROM tipo_estado
                    WHERE LOWER(estado) = @estado
                    LIMIT 1;";

                using var comandoEstado = new MySqlCommand(obtenerEstado, conexion);
                comandoEstado.Parameters.AddWithValue("@estado", nombreEstadoDestino);

                object? resultado = comandoEstado.ExecuteScalar();

                if (resultado == null)
                {
                    TempData["Mensaje"] = $"No existe el estado '{nombreEstadoDestino}' en tipo_estado.";
                    return RedirectToAction("Index", new { vista, busqueda, ordenarPor, direccion, pagina });
                }

                int idTipoEstado = Convert.ToInt32(resultado);

                string actualizar = @"
                    UPDATE proser
                    SET id_tipoestado = @id_tipoestado
                    WHERE id_proser = @id_proser;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@id_tipoestado", idTipoEstado);
                comandoActualizar.Parameters.AddWithValue("@id_proser", id);

                int filas = comandoActualizar.ExecuteNonQuery();

                if (filas == 0)
                {
                    TempData["Mensaje"] = "No se pudo cambiar el estado del producto o servicio.";
                }
                else
                {
                    TempData["Exito"] = nombreEstadoDestino == "activo"
                        ? "Producto o servicio activado correctamente."
                        : "Producto o servicio inactivado correctamente.";
                }

                return RedirectToAction("Index", new { vista, busqueda, ordenarPor, direccion, pagina });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
                return RedirectToAction("Index", new { vista, busqueda, ordenarPor, direccion, pagina });
            }
        }

        // ===============================
        // CARGAR COMBOS
        // ===============================
        private void CargarCombos(ProductoServicioFormViewModel? modelo = null)
        {
            List<dynamic> tipos = new List<dynamic>();
            List<dynamic> categorias = new List<dynamic>();
            List<dynamic> subcategorias = new List<dynamic>();
            List<dynamic> marcas = new List<dynamic>();
            List<dynamic> unidades = new List<dynamic>();
            List<dynamic> estados = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consultaTipos = @"
                    SELECT id_tipoproser, nombre
                    FROM tipo_proser
                    WHERE LOWER(nombre) IN ('producto', 'servicio')
                    ORDER BY nombre;";

                using (var comando = new MySqlCommand(consultaTipos, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        tipos.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_tipoproser"]),
                            Nombre = FormatearTextoPresentacion(lector["nombre"]?.ToString() ?? "")
                        });
                    }
                }

                string consultaCategorias = @"
                    SELECT id_categoria, nombre_categoria
                    FROM categoria
                    WHERE LOWER(nombre_categoria) <> 'habitaciones'
                    ORDER BY nombre_categoria;";

                using (var comando = new MySqlCommand(consultaCategorias, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        categorias.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_categoria"]),
                            Nombre = FormatearTextoPresentacion(lector["nombre_categoria"]?.ToString() ?? "")
                        });
                    }
                }

                string consultaSubcategorias = @"
                SELECT s.id_subcategoria, s.nombre_subcategoria
                FROM subcategoria s
                INNER JOIN categoria c ON s.id_categoria = c.id_categoria
                WHERE LOWER(c.nombre_categoria) <> 'habitaciones'
                  AND LOWER(s.estado) = 'activo'
                ORDER BY s.nombre_subcategoria;";

                using (var comando = new MySqlCommand(consultaSubcategorias, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        subcategorias.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_subcategoria"]),
                            Nombre = FormatearTextoPresentacion(lector["nombre_subcategoria"]?.ToString() ?? "")
                        });
                    }
                }

                string consultaMarcas = @"
                    SELECT id_marca, nombre_marca
                    FROM marca
                    ORDER BY nombre_marca;";

                using (var comando = new MySqlCommand(consultaMarcas, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        marcas.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_marca"]),
                            Nombre = FormatearTextoPresentacion(lector["nombre_marca"]?.ToString() ?? "")
                        });
                    }
                }

                string consultaUnidades = @"
                    SELECT id_umedida, nombre
                    FROM unidad_medida
                    WHERE LOWER(nombre) <> 'noche'
                      AND LOWER(nombre) <> 'servicio'
                    ORDER BY nombre;";

                using (var comando = new MySqlCommand(consultaUnidades, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        unidades.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_umedida"]),
                            Nombre = FormatearTextoPresentacion(lector["nombre"]?.ToString() ?? "")
                        });
                    }
                }

                string consultaEstados = @"
                    SELECT id_tipoestado, estado
                    FROM tipo_estado
                    WHERE LOWER(estado) IN ('activo', 'inactivo')
                    ORDER BY estado;";

                using (var comando = new MySqlCommand(consultaEstados, conexion))
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        estados.Add(new
                        {
                            Id = Convert.ToInt32(lector["id_tipoestado"]),
                            Nombre = FormatearTextoPresentacion(lector["estado"]?.ToString() ?? "")
                        });
                    }
                }
            }
            catch
            {
            }

            ViewBag.TiposProser = tipos;
            ViewBag.Categorias = categorias;
            ViewBag.Subcategorias = subcategorias;
            ViewBag.Marcas = marcas;
            ViewBag.Unidades = unidades;
            ViewBag.Estados = estados;

            if (modelo != null)
            {
                ViewBag.EsServicio = modelo.EsServicio;
            }
        }
    }
}