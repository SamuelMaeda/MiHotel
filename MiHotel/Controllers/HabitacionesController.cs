// CONTROLADOR ORIGINAL DEL SISTEMA MIHOTEL
// MODIFICACIÓN: SOLO AJUSTE DE FUENTE DE PRECIO (proser → subcategoria)
// NO SE ELIMINÓ NI REESTRUCTURÓ LÓGICA EXISTENTE

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using SkiaSharp;
using System.Data;

namespace MiHotel.Controllers
{
    public class HabitacionesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;
        private const int MaximoFotografiasPorHabitacion = 6;
        private const long TamanoMaximoFotografia = 10 * 1024 * 1024;
        private const int DimensionMaximaFotografia = 1800;

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

        private bool ValidarFotografias(IEnumerable<IFormFile>? archivos)
        {
            if (archivos == null) return true;

            bool validas = true;
            foreach (IFormFile archivo in archivos.Where(a => a.Length > 0))
            {
                if (archivo.Length > TamanoMaximoFotografia)
                {
                    ModelState.AddModelError(nameof(HabitacionFormViewModel.Fotografias),
                        $"La fotografía {archivo.FileName} supera el máximo de 10 MB.");
                    validas = false;
                }

                string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
                {
                    ModelState.AddModelError(nameof(HabitacionFormViewModel.Fotografias),
                        $"La fotografía {archivo.FileName} debe tener formato JPG, PNG o WEBP.");
                    validas = false;
                }
            }

            return validas;
        }

        private static byte[] ComprimirFotografia(IFormFile archivo)
        {
            using Stream entrada = archivo.OpenReadStream();
            using SKBitmap original = SKBitmap.Decode(entrada)
                ?? throw new InvalidOperationException("Uno de los archivos seleccionados no es una imagen válida.");

            double escala = Math.Min(1d,
                DimensionMaximaFotografia / (double)Math.Max(original.Width, original.Height));
            int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
            int alto = Math.Max(1, (int)Math.Round(original.Height * escala));

            using var redimensionada = new SKBitmap(
                new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Opaque));
            using (var lienzo = new SKCanvas(redimensionada))
            using (var pintura = new SKPaint { IsAntialias = true })
            {
                lienzo.Clear(SKColors.White);
                lienzo.DrawBitmap(
                    original,
                    new SKRect(0, 0, ancho, alto),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                    pintura);
                lienzo.Flush();
            }

            using SKImage imagen = SKImage.FromBitmap(redimensionada);
            using SKData datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 80)
                ?? throw new InvalidOperationException("No fue posible comprimir una fotografía.");
            return datos.ToArray();
        }

        private static void GuardarFotografias(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idHabitacion,
            IEnumerable<IFormFile>? archivos)
        {
            if (archivos == null) return;

            const string sql = @"
                INSERT INTO habitacion_fotografia
                    (id_proser,contenido,tipo_mime,nombre_original,tamano_original,tamano_comprimido)
                VALUES
                    (@id,@contenido,'image/jpeg',@nombre,@original,@comprimido);";

            foreach (IFormFile archivo in archivos.Where(a => a.Length > 0))
            {
                byte[] contenido = ComprimirFotografia(archivo);
                using var comando = new MySqlCommand(sql, conexion, transaccion);
                comando.Parameters.AddWithValue("@id", idHabitacion);
                comando.Parameters.Add("@contenido", MySqlDbType.LongBlob).Value = contenido;
                comando.Parameters.AddWithValue("@nombre", Path.GetFileName(archivo.FileName));
                comando.Parameters.AddWithValue("@original", archivo.Length);
                comando.Parameters.AddWithValue("@comprimido", contenido.Length);
                comando.ExecuteNonQuery();
            }
        }

        private static int ContarFotografias(
            MySqlConnection conexion,
            int idHabitacion,
            MySqlTransaction? transaccion = null)
        {
            using var comando = new MySqlCommand(
                "SELECT COUNT(*) FROM habitacion_fotografia WHERE id_proser=@id;",
                conexion,
                transaccion);
            comando.Parameters.AddWithValue("@id", idHabitacion);
            return Convert.ToInt32(comando.ExecuteScalar());
        }

        private List<HabitacionFotografiaViewModel> ObtenerFotografias(int idHabitacion)
        {
            var fotografias = new List<HabitacionFotografiaViewModel>();
            if (idHabitacion <= 0) return fotografias;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT id_fotografia,fecha_subida
                FROM habitacion_fotografia
                WHERE id_proser=@id
                ORDER BY fecha_subida,id_fotografia;", conexion);
            comando.Parameters.AddWithValue("@id", idHabitacion);
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                fotografias.Add(new HabitacionFotografiaViewModel
                {
                    IdFotografia = Convert.ToInt32(lector["id_fotografia"]),
                    FechaSubida = Convert.ToDateTime(lector["fecha_subida"])
                });
            }

            return fotografias;
        }

        private void CargarFotografiasExistentes(HabitacionFormViewModel modelo)
        {
            modelo.FotografiasExistentes = ObtenerFotografias(modelo.IdProser);
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
        [RequestSizeLimit(65 * 1024 * 1024)]
        public IActionResult Crear(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();

            ValidarFotografias(modelo.Fotografias);
            ValidarSubcategoriaActiva(modelo);
            int nuevasFotografias = modelo.Fotografias.Count(a => a.Length > 0);
            if (nuevasFotografias > MaximoFotografiasPorHabitacion)
            {
                ModelState.AddModelError(nameof(modelo.Fotografias),
                    $"Puede registrar un máximo de {MaximoFotografiasPorHabitacion} fotografías por habitación.");
            }

            if (!ModelState.IsValid) return View(modelo);

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();

                int idTipo = ObtenerIdTipoProserHabitacion(conexion);
                int idCategoria = ObtenerIdCategoriaHabitaciones(conexion);
                int idUnidad = ObtenerIdUnidadNoche(conexion);
                using var transaccion = conexion.BeginTransaction();

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

                using var comandoInsertar = new MySqlCommand(insertar, conexion, transaccion);
                comandoInsertar.Parameters.AddWithValue("@id_categoria", idCategoria);
                comandoInsertar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
                comandoInsertar.Parameters.AddWithValue("@id_umedida", idUnidad);
                comandoInsertar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoInsertar.Parameters.AddWithValue("@id_tipoproser", idTipo);
                comandoInsertar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
                comandoInsertar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);

                comandoInsertar.ExecuteNonQuery();
                int idHabitacion = Convert.ToInt32(comandoInsertar.LastInsertedId);
                GuardarFotografias(conexion, transaccion, idHabitacion, modelo.Fotografias);
                transaccion.Commit();

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

            lector.Close();
            CargarFotografiasExistentes(modelo);

            return View(modelo);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Detalle(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT p.id_proser,p.codigo,s.nombre_subcategoria,s.precio,
                       te.estado,p.descripcion
                FROM proser p
                LEFT JOIN subcategoria s ON p.id_subcategoria=s.id_subcategoria
                INNER JOIN tipo_estado te ON p.id_tipoestado=te.id_tipoestado
                INNER JOIN tipo_proser tp ON p.id_tipoproser=tp.id_tipoproser
                WHERE p.id_proser=@id AND LOWER(tp.nombre)='habitacion'
                LIMIT 1;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            using var lector = comando.ExecuteReader();
            if (!lector.Read()) return RedirectToAction("Index");

            var modelo = new HabitacionViewModel
            {
                IdProser = Convert.ToInt32(lector["id_proser"]),
                NumeroHabitacion = lector["codigo"]?.ToString() ?? "",
                TipoHabitacion = lector["nombre_subcategoria"]?.ToString() ?? "",
                Precio = lector["precio"] == DBNull.Value ? 0 : Convert.ToDecimal(lector["precio"]),
                Estado = lector["estado"]?.ToString() ?? "",
                Descripcion = lector["descripcion"]?.ToString()
            };

            lector.Close();
            modelo.Fotografias = ObtenerFotografias(modelo.IdProser);
            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(65 * 1024 * 1024)]
        public IActionResult Editar(HabitacionFormViewModel modelo)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            CargarCombos();

            ValidarFotografias(modelo.Fotografias);
            ValidarSubcategoriaActiva(modelo);
            if (!ModelState.IsValid)
            {
                CargarFotografiasExistentes(modelo);
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();

                int existentes = ContarFotografias(conexion, modelo.IdProser, transaccion);
                int nuevas = modelo.Fotografias.Count(a => a.Length > 0);
                if (existentes + nuevas > MaximoFotografiasPorHabitacion)
                {
                    transaccion.Rollback();
                    ModelState.AddModelError(nameof(modelo.Fotografias),
                        $"La habitación puede conservar un máximo de {MaximoFotografiasPorHabitacion} fotografías. Elimine alguna antes de agregar más.");
                    CargarFotografiasExistentes(modelo);
                    return View(modelo);
                }

                string actualizar = @"
                    UPDATE proser
                    SET id_subcategoria = @id_subcategoria,
                        id_tipoestado = @id_tipoestado,
                        codigo = @codigo,
                        nombre_proser = @nombre_proser,
                        descripcion = @descripcion
                    WHERE id_proser = @id_proser;";

                using var comandoActualizar = new MySqlCommand(actualizar, conexion, transaccion);
                comandoActualizar.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);
                comandoActualizar.Parameters.AddWithValue("@id_tipoestado", modelo.IdTipoEstado);
                comandoActualizar.Parameters.AddWithValue("@codigo", modelo.NumeroHabitacion.Trim());
                comandoActualizar.Parameters.AddWithValue("@nombre_proser", "Habitación " + modelo.NumeroHabitacion.Trim());
                comandoActualizar.Parameters.AddWithValue("@descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
                comandoActualizar.Parameters.AddWithValue("@id_proser", modelo.IdProser);

                comandoActualizar.ExecuteNonQuery();
                GuardarFotografias(conexion, transaccion, modelo.IdProser, modelo.Fotografias);
                transaccion.Commit();

                TempData["Exito"] = "Habitación actualizada correctamente.";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar la habitación: " + ex.Message;
                CargarFotografiasExistentes(modelo);
                return View(modelo);
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Fotografia(int id)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT contenido,tipo_mime
                FROM habitacion_fotografia
                WHERE id_fotografia=@id
                LIMIT 1;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            using var lector = comando.ExecuteReader();
            if (!lector.Read()) return NotFound();

            return File((byte[])lector["contenido"], lector["tipo_mime"]?.ToString() ?? "image/jpeg");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarFotografia(int id, int idHabitacion)
        {
            IActionResult? acceso = ValidarSesion();
            if (acceso != null) return acceso;

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                DELETE FROM habitacion_fotografia
                WHERE id_fotografia=@id AND id_proser=@habitacion;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            comando.Parameters.AddWithValue("@habitacion", idHabitacion);
            comando.ExecuteNonQuery();
            TempData["Exito"] = "Fotografía eliminada correctamente.";
            return RedirectToAction("Editar", new { id = idHabitacion });
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
              AND LOWER(s.estado) = 'activo'
              AND LOWER(c.estado) = 'activo'
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

        private void ValidarSubcategoriaActiva(HabitacionFormViewModel modelo)
        {
            if (modelo.IdSubcategoria <= 0)
            {
                return;
            }

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT COUNT(*)
                FROM subcategoria s
                INNER JOIN categoria c ON s.id_categoria = c.id_categoria
                WHERE s.id_subcategoria = @id_subcategoria
                  AND LOWER(s.estado) = 'activo'
                  AND LOWER(c.estado) = 'activo'
                  AND LOWER(c.nombre_categoria) = 'habitaciones';", conexion);
            comando.Parameters.AddWithValue("@id_subcategoria", modelo.IdSubcategoria);

            if (Convert.ToInt32(comando.ExecuteScalar()) == 0)
            {
                ModelState.AddModelError(nameof(modelo.IdSubcategoria),
                    "Debe seleccionar un tipo de habitación activo.");
            }
        }
    }
}
