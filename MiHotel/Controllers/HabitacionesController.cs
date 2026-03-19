using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    // ===============================
    // CONTROLADOR DE HABITACIONES
    // ===============================
    public class HabitacionesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public HabitacionesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        // ===============================
        // VALIDAR SESION ACTIVA
        // ===============================
        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        // ===============================
        // VALIDAR ACCESO
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
        // OBTENER COLUMNA DE ORDEN SEGURA
        // ===============================
        private string ObtenerColumnaOrden(string columna)
        {
            return columna.ToLower() switch
            {
                "tipo" => "s.nombre_subcategoria",
                "precio" => "p.precio",
                "estado" => "te.estado",
                _ => "p.codigo"
            };
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
                throw new Exception("No existe el tipo_proser 'habitacion'.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // OBTENER ID DE CATEGORIA HABITACIONES
        // ===============================
        private int ObtenerIdCategoriaHabitaciones(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_categoria
                FROM categoria
                WHERE LOWER(nombre_categoria) = 'habitaciones'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe la categoría 'Habitaciones'.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // OBTENER ID DE UNIDAD NOCHE
        // ===============================
        private int ObtenerIdUnidadNoche(MySqlConnection conexion)
        {
            string consulta = @"
                SELECT id_umedida
                FROM unidad_medida
                WHERE LOWER(nombre) = 'noche'
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
            {
                throw new Exception("No existe la unidad de medida 'noche'.");
            }

            return Convert.ToInt32(resultado);
        }

        // ===============================
        // LISTADO DE HABITACIONES
        // ===============================
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(
            string busqueda = "",
            string ordenarPor = "numero",
            string direccion = "asc",
            string vista = "todas",
            int pagina = 1)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            DataTable tablaHabitaciones = new DataTable();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string columnaOrden = ObtenerColumnaOrden(ordenarPor);
                string direccionOrden = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";
                string vistaNormalizada = vista.Trim().ToLower();

                if (pagina < 1)
                {
                    pagina = 1;
                }

                string filtroEstado = vistaNormalizada switch
                {
                    "libres" => "AND LOWER(te.estado) = 'libre'",
                    "ocupadas" => "AND LOWER(te.estado) = 'ocupada'",
                    "remodelacion" => "AND LOWER(te.estado) = 'remodelacion'",
                    "renta" => "AND LOWER(te.estado) = 'renta'",
                    _ => ""
                };

                string filtroBusqueda = "";
                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    filtroBusqueda = @"
                        AND (
                            p.codigo LIKE @busqueda
                            OR p.nombre_proser LIKE @busqueda
                            OR p.descripcion LIKE @busqueda
                            OR s.nombre_subcategoria LIKE @busqueda
                            OR te.estado LIKE @busqueda
                        )";
                }

                string consultaConteo = $@"
                    SELECT COUNT(*)
                    FROM proser p
                    LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                    INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                    WHERE p.id_tipoproser = @id_tipoproser
                    {filtroEstado}
                    {filtroBusqueda};";

                using var comandoConteo = new MySqlCommand(consultaConteo, conexion);
                comandoConteo.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

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
                        s.nombre_subcategoria AS tipo_habitacion,
                        p.precio,
                        te.estado,
                        p.descripcion
                    FROM proser p
                    LEFT JOIN subcategoria s ON p.id_subcategoria = s.id_subcategoria
                    INNER JOIN tipo_estado te ON p.id_tipoestado = te.id_tipoestado
                    WHERE p.id_tipoproser = @id_tipoproser
                    {filtroEstado}
                    {filtroBusqueda}
                    ORDER BY {columnaOrden} {direccionOrden}
                    LIMIT @limite OFFSET @offset;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id_tipoproser", idTipoHabitacion);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                }

                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", offset);

                using var adaptador = new MySqlDataAdapter(comando);
                adaptador.Fill(tablaHabitaciones);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = direccionOrden.ToLower();
                ViewBag.Vista = string.IsNullOrWhiteSpace(vistaNormalizada) ? "todas" : vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = totalPaginas;
                ViewBag.TotalRegistros = totalRegistros;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar las habitaciones: " + ex.Message;
            }

            return View(tablaHabitaciones);
        }

        // ===============================
        // CREAR HABITACION
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

            CargarCombos();
            return View(new HabitacionFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoProserHabitacion = ObtenerIdTipoProserHabitacion(conexion);
                int idCategoriaHabitaciones = ObtenerIdCategoriaHabitaciones(conexion);
                int idUnidadNoche = ObtenerIdUnidadNoche(conexion);

                string validarNumero = @"
                    SELECT COUNT(*)
                    FROM proser
                    WHERE codigo = @codigo
                      AND id_tipoproser = @id_tipoproser;";

                using (var comandoValidar = new MySqlCommand(validarNumero, conexion))
                {
                    comandoValidar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                    comandoValidar.Parameters.AddWithValue("@id_tipoproser", idTipoProserHabitacion);

                    int existe = Convert.ToInt32(comandoValidar.ExecuteScalar());
                    if (existe > 0)
                    {
                        ViewBag.Mensaje = "Ya existe una habitación con ese número.";
                        return View(modelo);
                    }
                }

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
                        NULL,
                        @id_umedida,
                        @id_tipoestado,
                        @id_tipoproser,
                        @codigo,
                        @nombre_proser,
                        @precio,
                        0,
                        @descripcion
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_categoria", idCategoriaHabitaciones);
                comandoInsertar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
                comandoInsertar.Parameters.AddWithValue("@id_umedida", idUnidadNoche);
                comandoInsertar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoInsertar.Parameters.AddWithValue("@id_tipoproser", idTipoProserHabitacion);
                comandoInsertar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@precio", modelo.Precio);
                comandoInsertar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Habitación creada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al crear la habitación: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // EDITAR HABITACION
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

            CargarCombos();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consulta = @"
                    SELECT
                        p.id_proser,
                        p.codigo,
                        p.id_subcategoria,
                        p.id_tipoestado,
                        p.precio,
                        p.descripcion
                    FROM proser p
                    INNER JOIN tipo_proser tp ON p.id_tipoproser = tp.id_tipoproser
                    WHERE p.id_proser = @id
                      AND LOWER(tp.nombre) = 'habitacion'
                    LIMIT 1;";

                using var comando = new MySqlCommand(consulta, conexion);
                comando.Parameters.AddWithValue("@id", id);

                using var lector = comando.ExecuteReader();

                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró la habitación solicitada.";
                    return RedirectToAction("Index");
                }

                HabitacionFormViewModel modelo = new HabitacionFormViewModel
                {
                    IdProser = Convert.ToInt32(lector["id_proser"]),
                    NumeroHabitacion = lector["codigo"]?.ToString() ?? "",
                    IdSubcategoria = lector["id_subcategoria"] == DBNull.Value ? 0 : Convert.ToInt32(lector["id_subcategoria"]),
                    IdTipoEstado = Convert.ToInt32(lector["id_tipoestado"]),
                    Precio = Convert.ToDecimal(lector["precio"]),
                    Descripcion = lector["descripcion"] == DBNull.Value ? null : lector["descripcion"].ToString()
                };

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar la habitación: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null)
            {
                return acceso;
            }

            CargarCombos();

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipoProserHabitacion = ObtenerIdTipoProserHabitacion(conexion);

                string validarNumero = @"
                    SELECT COUNT(*)
                    FROM proser
                    WHERE codigo = @codigo
                      AND id_tipoproser = @id_tipoproser
                      AND id_proser <> @id_proser;";

                using (var comandoValidar = new MySqlCommand(validarNumero, conexion))
                {
                    comandoValidar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                    comandoValidar.Parameters.AddWithValue("@id_tipoproser", idTipoProserHabitacion);
                    comandoValidar.Parameters.AddWithValue("@id_proser", modelo.IdProser);

                    int existe = Convert.ToInt32(comandoValidar.ExecuteScalar());
                    if (existe > 0)
                    {
                        ViewBag.Mensaje = "Ya existe otra habitación con ese número.";
                        return View(modelo);
                    }
                }

                string actualizar = @"
                    UPDATE proser
                    SET id_subcategoria = @id_subcategoria,
                        id_tipoestado = @id_tipoestado,
                        codigo = @codigo,
                        nombre_proser = @nombre_proser,
                        precio = @precio,
                        descripcion = @descripcion
                    WHERE id_proser = @id_proser;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion);
                comandoActualizar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
                comandoActualizar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoActualizar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                comandoActualizar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
                comandoActualizar.Parameters.AddWithValue("@precio", modelo.Precio);
                comandoActualizar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@id_proser", modelo.IdProser);

                comandoActualizar.ExecuteNonQuery();

                TempData["Exito"] = "Habitación actualizada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar la habitación: " + ex.Message;
                return View(modelo);
            }
        }

        // ===============================
        // CAMBIAR ESTADO DE HABITACION
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult CambiarEstado(
            int id,
            string nombreEstado,
            string busqueda = "",
            string ordenarPor = "numero",
            string direccion = "asc",
            string vista = "todas",
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

                string obtenerIdEstado = @"
                    SELECT id_tipoestado
                    FROM tipo_estado
                    WHERE LOWER(estado) = LOWER(@nombreEstado)
                    LIMIT 1;";

                int idTipoEstado;
                using (var comandoEstado = new MySqlCommand(obtenerIdEstado, conexion))
                {
                    comandoEstado.Parameters.AddWithValue("@nombreEstado", nombreEstado.Trim());

                    object? resultado = comandoEstado.ExecuteScalar();
                    if (resultado == null)
                    {
                        TempData["Mensaje"] = "No se encontró el estado seleccionado.";
                        return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
                    }

                    idTipoEstado = Convert.ToInt32(resultado);
                }

                string actualizar = @"
                    UPDATE proser
                    SET id_tipoestado = @idTipoEstado
                    WHERE id_proser = @idProser;";

                using var comando = new MySqlCommand(actualizar, conexion);
                comando.Parameters.AddWithValue("@idTipoEstado", idTipoEstado);
                comando.Parameters.AddWithValue("@idProser", id);

                int filas = comando.ExecuteNonQuery();

                if (filas == 0)
                {
                    TempData["Mensaje"] = "No se pudo cambiar el estado de la habitación.";
                }
                else
                {
                    TempData["Exito"] = "Estado de la habitación actualizado correctamente.";
                }

                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
                return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
            }
        }

        // ===============================
        // ACCIONES FUTURAS
        // ===============================
        [HttpGet]
        public IActionResult VerDisponibilidad(int id)
        {
            TempData["Mensaje"] = "Esta funcionalidad se implementará en el siguiente proceso.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult VerReservas(int id)
        {
            TempData["Mensaje"] = "Esta funcionalidad se implementará en el siguiente proceso.";
            return RedirectToAction("Index");
        }

        // ===============================
        // CARGAR COMBOS
        // ===============================
        private void CargarCombos()
        {
            List<dynamic> listaSubcategorias = new List<dynamic>();
            List<dynamic> listaEstados = new List<dynamic>();

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                string consultaSubcategorias = @"
                    SELECT id_subcategoria, nombre_subcategoria
                    FROM subcategoria
                    WHERE nombre_subcategoria IN ('Sencilla', 'Doble', 'Suite', 'Familiar')
                    ORDER BY nombre_subcategoria;";

                using (var comandoSubcategorias = new MySqlCommand(consultaSubcategorias, conexion))
                using (var lectorSubcategorias = comandoSubcategorias.ExecuteReader())
                {
                    while (lectorSubcategorias.Read())
                    {
                        listaSubcategorias.Add(new
                        {
                            IdSubcategoria = Convert.ToInt32(lectorSubcategorias["id_subcategoria"]),
                            Nombre = lectorSubcategorias["nombre_subcategoria"]?.ToString() ?? ""
                        });
                    }
                }

                string consultaEstados = @"
                    SELECT id_tipoestado, estado
                    FROM tipo_estado
                    ORDER BY estado;";

                using (var comandoEstados = new MySqlCommand(consultaEstados, conexion))
                using (var lectorEstados = comandoEstados.ExecuteReader())
                {
                    while (lectorEstados.Read())
                    {
                        listaEstados.Add(new
                        {
                            IdTipoEstado = Convert.ToInt32(lectorEstados["id_tipoestado"]),
                            Estado = lectorEstados["estado"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch
            {
            }

            ViewBag.SubcategoriasHabitacion = listaSubcategorias;
            ViewBag.EstadosHabitacion = listaEstados;
        }
    }
}