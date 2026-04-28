// CONTROLADOR ORIGINAL DEL SISTEMA MIHOTEL
// MODIFICACIÓN: SOLO AJUSTE DE FUENTE DE PRECIO (proser → subcategoria)
// NO SE ELIMINÓ NI REESTRUCTURÓ LÓGICA EXISTENTE

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class HabitacionesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public HabitacionesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private bool TieneSesionActiva()
        {
            string? idUsuario = HttpContext.Session.GetString("IdUsuario");
            return !string.IsNullOrEmpty(idUsuario);
        }

        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
            {
                return RedirectToAction("Login", "Acceso");
            }

            return null;
        }

        private string ObtenerColumnaOrden(string columna)
        {
            return columna.ToLower() switch
            {
                "tipo" => "s.nombre_subcategoria",
                "precio" => "s.precio", // CAMBIO
                "estado" => "te.estado",
                _ => "p.codigo"
            };
        }

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

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(string busqueda = "", string ordenarPor = "numero", string direccion = "asc", string vista = "todas", int pagina = 1)
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

                if (pagina < 1) pagina = 1;

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

                if (totalPaginas == 0) totalPaginas = 1;
                if (pagina > totalPaginas) pagina = totalPaginas;

                int offset = (pagina - 1) * RegistrosPorPagina;

                string consulta = $@"
                    SELECT
                        p.id_proser,
                        p.codigo,
                        p.nombre_proser,
                        s.nombre_subcategoria AS tipo_habitacion,
                        s.precio, -- CAMBIO
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

        [HttpGet]
        public IActionResult Crear()
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();
            return View(new HabitacionFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();

            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipo = ObtenerIdTipoProserHabitacion(conexion);
                int idCategoria = ObtenerIdCategoriaHabitaciones(conexion);
                int idUnidad = ObtenerIdUnidadNoche(conexion);

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
                        0,
                        @descripcion
                    );";

                using var comandoInsertar = new MySqlCommand(insertar, conexion);
                comandoInsertar.Parameters.AddWithValue("@id_categoria", idCategoria);
                comandoInsertar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
                comandoInsertar.Parameters.AddWithValue("@id_umedida", idUnidad);
                comandoInsertar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoInsertar.Parameters.AddWithValue("@id_tipoproser", idTipo);
                comandoInsertar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);

                comandoInsertar.ExecuteNonQuery();

                TempData["Exito"] = "Habitación creada correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Error: " + ex.Message;
                return View(modelo);
            }
        }

        // EDITAR (sin precio)
        [HttpGet]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string consulta = @"
                SELECT
                    p.id_proser,
                    p.codigo,
                    p.id_subcategoria,
                    p.id_tipoestado,
                    p.descripcion
                FROM proser p
                WHERE p.id_proser = @id
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@id", id);

            using var lector = comando.ExecuteReader();

            if (!lector.Read())
            {
                return RedirectToAction("Index");
            }

            HabitacionFormViewModel modelo = new HabitacionFormViewModel
            {
                IdProser = Convert.ToInt32(lector["id_proser"]),
                NumeroHabitacion = lector["codigo"].ToString(),
                IdSubcategoria = Convert.ToInt32(lector["id_subcategoria"]),
                IdTipoEstado = Convert.ToInt32(lector["id_tipoestado"]),
                Descripcion = lector["descripcion"]?.ToString()
            };

            return View(modelo);
        }

        [HttpPost]
        public IActionResult Editar(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();

            if (!ModelState.IsValid) return View(modelo);

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string actualizar = @"
                UPDATE proser
                SET id_subcategoria = @id_subcategoria,
                    id_tipoestado = @id_tipoestado,
                    codigo = @codigo,
                    nombre_proser = @nombre_proser,
                    descripcion = @descripcion
                WHERE id_proser = @id_proser;";

            using var comandoActualizar = new MySqlCommand(actualizar, conexion);
            comandoActualizar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
            comandoActualizar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
            comandoActualizar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
            comandoActualizar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
            comandoActualizar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
            comandoActualizar.Parameters.AddWithValue("@id_proser", modelo.IdProser);

            comandoActualizar.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        private void CargarCombos()
        {
            List<dynamic> listaSubcategorias = new List<dynamic>();
            List<dynamic> listaEstados = new List<dynamic>();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string consultaSubcategorias = @"
            SELECT s.id_subcategoria, s.nombre_subcategoria
            FROM subcategoria s
            INNER JOIN categoria c ON s.id_categoria = c.id_categoria
            WHERE LOWER(c.nombre_categoria) = 'habitaciones'
            ORDER BY s.nombre_subcategoria;";

            using (var comando = new MySqlCommand(consultaSubcategorias, conexion))
            using (var lector = comando.ExecuteReader())
            {
                while (lector.Read())
                {
                    listaSubcategorias.Add(new
                    {
                        IdSubcategoria = Convert.ToInt32(lector["id_subcategoria"]),
                        Nombre = lector["nombre_subcategoria"].ToString()
                    });
                }
            }

            string consultaEstados = @"
            SELECT id_tipoestado, estado
            FROM tipo_estado
            WHERE LOWER(estado) NOT IN ('activo', 'inactivo');";

            using (var comando = new MySqlCommand(consultaEstados, conexion))
            using (var lector = comando.ExecuteReader())
            {
                while (lector.Read())
                {
                    listaEstados.Add(new
                    {
                        IdTipoEstado = Convert.ToInt32(lector["id_tipoestado"]),
                        Estado = lector["estado"].ToString()
                    });
                }
            }

            ViewBag.SubcategoriasHabitacion = listaSubcategorias;
            ViewBag.EstadosHabitacion = listaEstados;
        }
    }
}