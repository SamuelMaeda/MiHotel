using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using SkiaSharp;
using System.Data;

namespace MiHotel.Controllers
{
    public class ClientesController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;
        private const long TamanoMaximoImagen = 10 * 1024 * 1024;
        private const int DimensionMaximaImagen = 1800;

        public ClientesController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private string ObtenerRol() => HttpContext.Session.GetString("NombreRol")?.Trim().ToLower() ?? "";

        private IActionResult? ValidarAcceso()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario")))
            {
                return RedirectToAction("Login", "Acceso");
            }

            string rol = ObtenerRol();
            if (rol != "admin" && rol != "recepcionista")
            {
                return RedirectToAction("Index", "Panel");
            }

            return null;
        }

        private static string NormalizarTelefono(string telefono) => telefono.Replace(" ", "").Trim();

        private static string FormatearTelefono(string telefono)
        {
            string limpio = NormalizarTelefono(telefono);
            return limpio.Length == 8 ? limpio[..4] + " " + limpio[4..] : telefono;
        }

        private static string? NormalizarOpcional(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        private static string? NormalizarDpi(string? dpi)
        {
            if (string.IsNullOrWhiteSpace(dpi)) return null;
            return new string(dpi.Where(char.IsDigit).ToArray());
        }

        private static string? NormalizarPlaca(string? placa) =>
            string.IsNullOrWhiteSpace(placa) ? null : placa.Trim().ToUpperInvariant();

        private static string ObtenerColumnaOrden(string columna) => columna.ToLower() switch
        {
            "nit" => "c.nit",
            "telefono" => "c.telefono",
            "empresa" => "ec.nombre",
            "tipo" => "cc.codigo",
            "placa" => "cd.placa_reciente",
            "limpieza" => "cd.solicita_limpieza",
            _ => "c.nombre"
        };

        private static int ObtenerIdTipoCliente(MySqlConnection conexion, MySqlTransaction? transaccion = null)
        {
            const string sql = "SELECT id_tipoclipro FROM tipo_clipro WHERE LOWER(tipo)='cliente' LIMIT 1;";
            using var comando = new MySqlCommand(sql, conexion, transaccion);
            object? resultado = comando.ExecuteScalar();
            if (resultado == null) throw new InvalidOperationException("No existe el tipo 'cliente' en tipo_clipro.");
            return Convert.ToInt32(resultado);
        }

        private void CargarCatalogos(int? empresaSeleccionada = null, string clasificacionSeleccionada = "B")
        {
            var empresas = new List<SelectListItem>();
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var comando = new MySqlCommand(
                    "SELECT id_empresa_cliente, nombre FROM empresa_cliente WHERE estado='activo' OR id_empresa_cliente=@id ORDER BY nombre;",
                    conexion);
                comando.Parameters.AddWithValue("@id", (object?)empresaSeleccionada ?? DBNull.Value);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    int id = Convert.ToInt32(lector["id_empresa_cliente"]);
                    empresas.Add(new SelectListItem(lector["nombre"]?.ToString(), id.ToString(), id == empresaSeleccionada));
                }
            }
            catch
            {
                // El error principal se mostrará al guardar; la vista puede seguir mostrándose.
            }

            ViewBag.EmpresasCliente = empresas;
            ViewBag.ClasificacionesCliente = new List<SelectListItem>
            {
                new("A - Tranquilo", "A", clasificacionSeleccionada == "A"),
                new("B - Neutral", "B", clasificacionSeleccionada == "B"),
                new("C - Problemático", "C", clasificacionSeleccionada == "C")
            };
        }

        private bool ValidarImagen(IFormFile? archivo, string campo)
        {
            if (archivo == null || archivo.Length == 0) return true;

            if (archivo.Length > TamanoMaximoImagen)
            {
                ModelState.AddModelError(campo, "La fotografía no puede superar 10 MB.");
                return false;
            }

            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            {
                ModelState.AddModelError(campo, "Use una imagen JPG, PNG o WEBP.");
                return false;
            }

            return true;
        }

        private static byte[] ComprimirImagen(IFormFile archivo)
        {
            using Stream entrada = archivo.OpenReadStream();
            using SKBitmap original = SKBitmap.Decode(entrada)
                ?? throw new InvalidOperationException("El archivo seleccionado no es una imagen válida.");

            double escala = Math.Min(1d, DimensionMaximaImagen / (double)Math.Max(original.Width, original.Height));
            int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
            int alto = Math.Max(1, (int)Math.Round(original.Height * escala));
            using var redimensionada = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Opaque));
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
                ?? throw new InvalidOperationException("No fue posible comprimir la fotografía.");
            return datos.ToArray();
        }

        private static void GuardarDocumento(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int idCliente,
            string tipo,
            IFormFile? archivo)
        {
            if (archivo == null || archivo.Length == 0) return;
            byte[] contenido = ComprimirImagen(archivo);

            const string sql = @"
                INSERT INTO cliente_documento
                    (id_clipro, tipo_documento, contenido, tipo_mime, nombre_original, tamano_original, tamano_comprimido)
                VALUES
                    (@id, @tipo, @contenido, 'image/jpeg', @nombre, @original, @comprimido)
                ON DUPLICATE KEY UPDATE
                    contenido=VALUES(contenido), tipo_mime=VALUES(tipo_mime),
                    nombre_original=VALUES(nombre_original), tamano_original=VALUES(tamano_original),
                    tamano_comprimido=VALUES(tamano_comprimido), fecha_subida=CURRENT_TIMESTAMP;";

            using var comando = new MySqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@id", idCliente);
            comando.Parameters.AddWithValue("@tipo", tipo);
            comando.Parameters.Add("@contenido", MySqlDbType.LongBlob).Value = contenido;
            comando.Parameters.AddWithValue("@nombre", Path.GetFileName(archivo.FileName));
            comando.Parameters.AddWithValue("@original", archivo.Length);
            comando.Parameters.AddWithValue("@comprimido", contenido.Length);
            comando.ExecuteNonQuery();
        }

        private static bool EmpresaClienteEsValida(
            MySqlConnection conexion,
            MySqlTransaction transaccion,
            int? idEmpresa,
            int? idClienteExistente = null)
        {
            if (!idEmpresa.HasValue)
            {
                return true;
            }

            const string sql = @"
                SELECT COUNT(*)
                FROM empresa_cliente e
                WHERE e.id_empresa_cliente = @id_empresa
                  AND (
                      e.estado = 'activo'
                      OR EXISTS (
                          SELECT 1 FROM cliente_detalle cd
                          WHERE cd.id_clipro = @id_cliente
                            AND cd.id_empresa_cliente = e.id_empresa_cliente
                      )
                  );";
            using var comando = new MySqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@id_empresa", idEmpresa.Value);
            comando.Parameters.AddWithValue("@id_cliente", (object?)idClienteExistente ?? DBNull.Value);
            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
        }

        private bool ExisteDpi(MySqlConnection conexion, string? dpi, int? excluirId = null, MySqlTransaction? transaccion = null)
        {
            if (string.IsNullOrWhiteSpace(dpi)) return false;
            string sql = "SELECT COUNT(*) FROM cliente_detalle WHERE numero_dpi=@dpi" +
                         (excluirId.HasValue ? " AND id_clipro<>@id" : "") + ";";
            using var comando = new MySqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@dpi", dpi);
            if (excluirId.HasValue) comando.Parameters.AddWithValue("@id", excluirId.Value);
            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
        }

        private static bool ExisteCorreo(MySqlConnection conexion, string? correo, int tipoCliente, int? excluirId = null, MySqlTransaction? transaccion = null)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;
            string sql = "SELECT COUNT(*) FROM clipro WHERE id_tipoclipro=@tipo AND LOWER(correo)=LOWER(@correo)" +
                         (excluirId.HasValue ? " AND id_clipro<>@id" : "") + ";";
            using var comando = new MySqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@tipo", tipoCliente);
            comando.Parameters.AddWithValue("@correo", correo.Trim());
            if (excluirId.HasValue) comando.Parameters.AddWithValue("@id", excluirId.Value);
            return Convert.ToInt32(comando.ExecuteScalar()) > 0;
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index(string busqueda = "", string ordenarPor = "nombre", string direccion = "asc", string vista = "activos", int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;

            DataTable tabla = new();
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                int tipo = ObtenerIdTipoCliente(conexion);
                string columna = ObtenerColumnaOrden(ordenarPor);
                string sentido = direccion.Trim().ToLower() == "desc" ? "DESC" : "ASC";
                string vistaNormalizada = vista.Trim().ToLower() == "inactivos" ? "inactivos" : "activos";
                string estado = vistaNormalizada == "inactivos" ? "inactivo" : "activo";
                pagina = Math.Max(1, pagina);

                string filtro = string.IsNullOrWhiteSpace(busqueda) ? "" : @"
                    AND (c.nombre LIKE @busqueda OR c.nit LIKE @busqueda OR c.telefono LIKE @busqueda
                         OR c.correo LIKE @busqueda OR cd.numero_dpi LIKE @busqueda
                         OR cd.placa_reciente LIKE @busqueda OR ec.nombre LIKE @busqueda)";

                string desde = @"
                    FROM clipro c
                    INNER JOIN cliente_detalle cd ON cd.id_clipro=c.id_clipro
                    INNER JOIN clasificacion_cliente cc ON cc.codigo=cd.codigo_clasificacion
                    LEFT JOIN empresa_cliente ec ON ec.id_empresa_cliente=cd.id_empresa_cliente
                    WHERE c.id_tipoclipro=@tipo AND c.estado=@estado" + filtro;

                using var conteo = new MySqlCommand("SELECT COUNT(*) " + desde + ";", conexion);
                conteo.Parameters.AddWithValue("@tipo", tipo);
                conteo.Parameters.AddWithValue("@estado", estado);
                if (filtro.Length > 0) conteo.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                int total = Convert.ToInt32(conteo.ExecuteScalar());
                int paginas = Math.Max(1, (int)Math.Ceiling(total / (double)RegistrosPorPagina));
                pagina = Math.Min(pagina, paginas);

                string sql = @"
                    SELECT c.id_clipro, c.nombre, c.nit, c.telefono, c.correo,
                           ec.nombre AS empresa_procedencia, cd.placa_reciente, cd.solicita_limpieza,
                           cc.codigo AS codigo_clasificacion, cc.nombre AS nombre_clasificacion, c.estado " +
                           desde + $" ORDER BY {columna} {sentido} LIMIT @limite OFFSET @offset;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@tipo", tipo);
                comando.Parameters.AddWithValue("@estado", estado);
                if (filtro.Length > 0) comando.Parameters.AddWithValue("@busqueda", "%" + busqueda.Trim() + "%");
                comando.Parameters.AddWithValue("@limite", RegistrosPorPagina);
                comando.Parameters.AddWithValue("@offset", (pagina - 1) * RegistrosPorPagina);
                new MySqlDataAdapter(comando).Fill(tabla);

                ViewBag.Busqueda = busqueda;
                ViewBag.OrdenarPor = ordenarPor;
                ViewBag.Direccion = sentido.ToLower();
                ViewBag.Vista = vistaNormalizada;
                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = paginas;
                ViewBag.TotalRegistros = total;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al cargar los clientes: " + ex.Message;
            }
            return View(tabla);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(string? returnUrl = null)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            ViewBag.ReturnUrl = returnUrl;
            CargarCatalogos();
            return View(new ClienteAdmin());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Crear(ClienteAdmin modelo, string? returnUrl = null)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            ViewBag.ReturnUrl = returnUrl;

            modelo.NumeroDpi = NormalizarDpi(modelo.NumeroDpi);
            modelo.PlacaReciente = NormalizarPlaca(modelo.PlacaReciente);
            ValidarImagen(modelo.DpiFrente, nameof(modelo.DpiFrente));
            if (!ModelState.IsValid)
            {
                CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                int tipo = ObtenerIdTipoCliente(conexion, transaccion);

                if (!EmpresaClienteEsValida(conexion, transaccion, modelo.IdEmpresaCliente))
                {
                    ModelState.AddModelError(nameof(modelo.IdEmpresaCliente), "Debe seleccionar una empresa activa.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }

                if (ExisteCorreo(conexion, modelo.Correo, tipo, null, transaccion))
                {
                    ModelState.AddModelError(nameof(modelo.Correo), "El correo ya está registrado en otro cliente.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }

                if (ExisteDpi(conexion, modelo.NumeroDpi, null, transaccion))
                {
                    ModelState.AddModelError(nameof(modelo.NumeroDpi), "Este número de DPI ya pertenece a otro cliente.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }

                const string insertarCliente = @"
                    INSERT INTO clipro (id_tipoclipro,nombre,nit,telefono,correo,direccion,estado)
                    VALUES (@tipo,@nombre,@nit,@telefono,@correo,@direccion,'activo');";
                using var comandoCliente = new MySqlCommand(insertarCliente, conexion, transaccion);
                comandoCliente.Parameters.AddWithValue("@tipo", tipo);
                comandoCliente.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comandoCliente.Parameters.AddWithValue("@nit", (object?)NormalizarOpcional(modelo.Nit) ?? DBNull.Value);
                comandoCliente.Parameters.AddWithValue("@telefono", NormalizarTelefono(modelo.Telefono));
                comandoCliente.Parameters.AddWithValue("@correo", (object?)NormalizarOpcional(modelo.Correo) ?? DBNull.Value);
                comandoCliente.Parameters.AddWithValue("@direccion", (object?)NormalizarOpcional(modelo.Direccion) ?? DBNull.Value);
                comandoCliente.ExecuteNonQuery();
                int id = Convert.ToInt32(comandoCliente.LastInsertedId);

                const string insertarDetalle = @"
                    INSERT INTO cliente_detalle
                        (id_clipro,id_empresa_cliente,numero_dpi,placa_reciente,codigo_clasificacion,solicita_limpieza)
                    VALUES
                        (@id,@empresa,@dpi,@placa,@clasificacion,@solicita_limpieza);";
                using var comandoDetalle = new MySqlCommand(insertarDetalle, conexion, transaccion);
                comandoDetalle.Parameters.AddWithValue("@id", id);
                comandoDetalle.Parameters.AddWithValue("@empresa", (object?)modelo.IdEmpresaCliente ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@dpi", (object?)modelo.NumeroDpi ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@placa", (object?)modelo.PlacaReciente ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@clasificacion", modelo.CodigoClasificacion);
                comandoDetalle.Parameters.AddWithValue("@solicita_limpieza", modelo.SolicitaLimpieza);
                comandoDetalle.ExecuteNonQuery();

                GuardarDocumento(conexion, transaccion, id, "dpi_frente", modelo.DpiFrente);
                transaccion.Commit();
                TempData["Exito"] = "Cliente creado correctamente.";

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    string destino = returnUrl + (returnUrl.Contains('?') ? "&" : "?") + "idClipro=" + id;
                    if (Url.IsLocalUrl(destino)) return Redirect(destino);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al guardar el cliente: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                return View(modelo);
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(int id)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                int tipo = ObtenerIdTipoCliente(conexion);
                const string sql = @"
                    SELECT c.id_clipro,c.nombre,c.nit,c.telefono,c.correo,c.direccion,
                           cd.id_empresa_cliente,cd.numero_dpi,cd.placa_reciente,cd.codigo_clasificacion,cd.solicita_limpieza,
                           EXISTS(SELECT 1 FROM cliente_documento d WHERE d.id_clipro=c.id_clipro AND d.tipo_documento='dpi_frente') AS frente
                    FROM clipro c INNER JOIN cliente_detalle cd ON cd.id_clipro=c.id_clipro
                    WHERE c.id_clipro=@id AND c.id_tipoclipro=@tipo LIMIT 1;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@tipo", tipo);
                using var lector = comando.ExecuteReader();
                if (!lector.Read())
                {
                    TempData["Mensaje"] = "No se encontró el cliente solicitado.";
                    return RedirectToAction("Index");
                }

                var modelo = new EditarCliente
                {
                    IdClipro = id,
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Nit = lector["nit"] == DBNull.Value ? null : lector["nit"].ToString(),
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Correo = lector["correo"] == DBNull.Value ? null : lector["correo"].ToString(),
                    Direccion = lector["direccion"] == DBNull.Value ? null : lector["direccion"].ToString(),
                    IdEmpresaCliente = lector["id_empresa_cliente"] == DBNull.Value ? null : Convert.ToInt32(lector["id_empresa_cliente"]),
                    NumeroDpi = lector["numero_dpi"] == DBNull.Value ? null : lector["numero_dpi"].ToString(),
                    PlacaReciente = lector["placa_reciente"] == DBNull.Value ? null : lector["placa_reciente"].ToString(),
                    CodigoClasificacion = lector["codigo_clasificacion"]?.ToString() ?? "B",
                    SolicitaLimpieza = Convert.ToBoolean(lector["solicita_limpieza"]),
                    TieneDpiFrente = Convert.ToBoolean(lector["frente"])
                };
                CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el cliente: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Editar(EditarCliente modelo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            modelo.NumeroDpi = NormalizarDpi(modelo.NumeroDpi);
            modelo.PlacaReciente = NormalizarPlaca(modelo.PlacaReciente);
            ValidarImagen(modelo.DpiFrente, nameof(modelo.DpiFrente));
            if (!ModelState.IsValid)
            {
                CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                return View(modelo);
            }

            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                using var transaccion = conexion.BeginTransaction();
                int tipo = ObtenerIdTipoCliente(conexion, transaccion);

                if (!EmpresaClienteEsValida(conexion, transaccion, modelo.IdEmpresaCliente, modelo.IdClipro))
                {
                    ModelState.AddModelError(nameof(modelo.IdEmpresaCliente), "Debe seleccionar una empresa activa.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }

                if (ExisteCorreo(conexion, modelo.Correo, tipo, modelo.IdClipro, transaccion))
                {
                    ModelState.AddModelError(nameof(modelo.Correo), "El correo ya está registrado en otro cliente.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }
                if (ExisteDpi(conexion, modelo.NumeroDpi, modelo.IdClipro, transaccion))
                {
                    ModelState.AddModelError(nameof(modelo.NumeroDpi), "Este número de DPI ya pertenece a otro cliente.");
                    transaccion.Rollback();
                    CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                    return View(modelo);
                }

                const string actualizarCliente = @"
                    UPDATE clipro SET nombre=@nombre,nit=@nit,telefono=@telefono,correo=@correo,direccion=@direccion
                    WHERE id_clipro=@id AND id_tipoclipro=@tipo;";
                using var comandoCliente = new MySqlCommand(actualizarCliente, conexion, transaccion);
                comandoCliente.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                comandoCliente.Parameters.AddWithValue("@nit", (object?)NormalizarOpcional(modelo.Nit) ?? DBNull.Value);
                comandoCliente.Parameters.AddWithValue("@telefono", NormalizarTelefono(modelo.Telefono));
                comandoCliente.Parameters.AddWithValue("@correo", (object?)NormalizarOpcional(modelo.Correo) ?? DBNull.Value);
                comandoCliente.Parameters.AddWithValue("@direccion", (object?)NormalizarOpcional(modelo.Direccion) ?? DBNull.Value);
                comandoCliente.Parameters.AddWithValue("@id", modelo.IdClipro);
                comandoCliente.Parameters.AddWithValue("@tipo", tipo);
                if (comandoCliente.ExecuteNonQuery() == 0) throw new InvalidOperationException("No se encontró el cliente solicitado.");

                const string actualizarDetalle = @"
                    UPDATE cliente_detalle SET id_empresa_cliente=@empresa,numero_dpi=@dpi,
                        placa_reciente=@placa,codigo_clasificacion=@clasificacion,
                        solicita_limpieza=@solicita_limpieza WHERE id_clipro=@id;";
                using var comandoDetalle = new MySqlCommand(actualizarDetalle, conexion, transaccion);
                comandoDetalle.Parameters.AddWithValue("@empresa", (object?)modelo.IdEmpresaCliente ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@dpi", (object?)modelo.NumeroDpi ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@placa", (object?)modelo.PlacaReciente ?? DBNull.Value);
                comandoDetalle.Parameters.AddWithValue("@clasificacion", modelo.CodigoClasificacion);
                comandoDetalle.Parameters.AddWithValue("@solicita_limpieza", modelo.SolicitaLimpieza);
                comandoDetalle.Parameters.AddWithValue("@id", modelo.IdClipro);
                comandoDetalle.ExecuteNonQuery();

                GuardarDocumento(conexion, transaccion, modelo.IdClipro, "dpi_frente", modelo.DpiFrente);
                transaccion.Commit();
                TempData["Exito"] = "Cliente actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = "Ocurrió un error al actualizar el cliente: " + ex.Message;
                modelo.Telefono = FormatearTelefono(modelo.Telefono);
                CargarCatalogos(modelo.IdEmpresaCliente, modelo.CodigoClasificacion);
                return View(modelo);
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
                int tipo = ObtenerIdTipoCliente(conexion);
                const string sql = @"
                    SELECT c.id_clipro,c.nombre,c.nit,c.telefono,c.correo,c.direccion,c.estado,
                           ec.nombre AS empresa,cd.numero_dpi,cd.placa_reciente,cd.solicita_limpieza,
                           cc.codigo AS clasificacion,cc.nombre AS nombre_clasificacion,
                           EXISTS(SELECT 1 FROM cliente_documento d WHERE d.id_clipro=c.id_clipro AND d.tipo_documento='dpi_frente') AS frente
                    FROM clipro c INNER JOIN cliente_detalle cd ON cd.id_clipro=c.id_clipro
                    INNER JOIN clasificacion_cliente cc ON cc.codigo=cd.codigo_clasificacion
                    LEFT JOIN empresa_cliente ec ON ec.id_empresa_cliente=cd.id_empresa_cliente
                    WHERE c.id_clipro=@id AND c.id_tipoclipro=@tipo LIMIT 1;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@tipo", tipo);
                using var lector = comando.ExecuteReader();
                if (!lector.Read()) return RedirectToAction("Index");
                var modelo = new ClienteDetalleViewModel
                {
                    IdClipro = id,
                    Nombre = lector["nombre"]?.ToString() ?? "",
                    Nit = lector["nit"] == DBNull.Value ? null : lector["nit"].ToString(),
                    Telefono = FormatearTelefono(lector["telefono"]?.ToString() ?? ""),
                    Correo = lector["correo"] == DBNull.Value ? null : lector["correo"].ToString(),
                    Direccion = lector["direccion"] == DBNull.Value ? null : lector["direccion"].ToString(),
                    EmpresaProcedencia = lector["empresa"] == DBNull.Value ? null : lector["empresa"].ToString(),
                    NumeroDpi = lector["numero_dpi"] == DBNull.Value ? null : lector["numero_dpi"].ToString(),
                    PlacaReciente = lector["placa_reciente"] == DBNull.Value ? null : lector["placa_reciente"].ToString(),
                    CodigoClasificacion = lector["clasificacion"]?.ToString() ?? "B",
                    NombreClasificacion = lector["nombre_clasificacion"]?.ToString() ?? "Neutral",
                    SolicitaLimpieza = Convert.ToBoolean(lector["solicita_limpieza"]),
                    TieneDpiFrente = Convert.ToBoolean(lector["frente"]),
                    Estado = lector["estado"]?.ToString() ?? "activo"
                };

                lector.Close();

                using (var reservas = new MySqlCommand(@"
                    SELECT r.id_reserva, r.id_reserva_grupo, r.fecha_entrada, r.fecha_salida,
                           p.codigo AS habitacion, r.estado,
                           COALESCE(rf.estado_facturacion, 'sin_definir') AS estado_facturacion
                    FROM reserva r
                    INNER JOIN proser p ON p.id_proser = r.id_habitacion
                    LEFT JOIN reserva_facturacion rf ON rf.id_reserva = r.id_reserva
                    WHERE r.id_clipro = @id
                    ORDER BY r.fecha_entrada DESC, r.id_reserva DESC;", conexion))
                {
                    reservas.Parameters.AddWithValue("@id", id);
                    using var lectorReservas = reservas.ExecuteReader();
                    while (lectorReservas.Read())
                    {
                        modelo.Reservas.Add(new ReservaResumenFiscalViewModel
                        {
                            IdReserva = Convert.ToInt32(lectorReservas["id_reserva"]),
                            IdReservaGrupo = lectorReservas["id_reserva_grupo"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(lectorReservas["id_reserva_grupo"]),
                            FechaEntrada = Convert.ToDateTime(lectorReservas["fecha_entrada"]),
                            FechaSalida = Convert.ToDateTime(lectorReservas["fecha_salida"]),
                            Habitacion = lectorReservas["habitacion"]?.ToString() ?? "",
                            Estado = lectorReservas["estado"]?.ToString() ?? "",
                            EstadoFacturacion = lectorReservas["estado_facturacion"]?.ToString() ?? "sin_definir"
                        });
                    }
                }
                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cargar el detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult DocumentoDpi(int id, string tipo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (tipo != "dpi_frente") return NotFound();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            int idTipo = ObtenerIdTipoCliente(conexion);
            const string sql = @"
                SELECT d.contenido,d.tipo_mime FROM cliente_documento d
                INNER JOIN clipro c ON c.id_clipro=d.id_clipro
                WHERE d.id_clipro=@id AND d.tipo_documento=@tipo AND c.id_tipoclipro=@idTipo LIMIT 1;";
            using var comando = new MySqlCommand(sql, conexion);
            comando.Parameters.AddWithValue("@id", id);
            comando.Parameters.AddWithValue("@tipo", tipo);
            comando.Parameters.AddWithValue("@idTipo", idTipo);
            using var lector = comando.ExecuteReader(CommandBehavior.SequentialAccess);
            if (!lector.Read()) return NotFound();
            Response.Headers.CacheControl = "no-store, no-cache";
            return File((byte[])lector["contenido"], lector["tipo_mime"]?.ToString() ?? "image/jpeg");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarDocumentoDpi(int id, string tipo)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            if (tipo != "dpi_frente") return BadRequest();
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand("DELETE FROM cliente_documento WHERE id_clipro=@id AND tipo_documento=@tipo;", conexion);
            comando.Parameters.AddWithValue("@id", id);
            comando.Parameters.AddWithValue("@tipo", tipo);
            comando.ExecuteNonQuery();
            TempData["Exito"] = "Fotografía eliminada correctamente.";
            return RedirectToAction("Editar", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id, string busqueda = "", string ordenarPor = "nombre", string direccion = "asc", string vista = "activos", int pagina = 1)
        {
            IActionResult? acceso = ValidarAcceso();
            if (acceso != null) return acceso;
            try
            {
                using var conexion = _conexionBD.ObtenerConexion();
                conexion.Open();
                int tipo = ObtenerIdTipoCliente(conexion);
                const string sql = @"
                    UPDATE clipro SET estado=IF(estado='activo','inactivo','activo')
                    WHERE id_clipro=@id AND id_tipoclipro=@tipo;";
                using var comando = new MySqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("@id", id);
                comando.Parameters.AddWithValue("@tipo", tipo);
                comando.ExecuteNonQuery();
                TempData["Exito"] = "Estado del cliente actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Ocurrió un error al cambiar el estado: " + ex.Message;
            }
            return RedirectToAction("Index", new { busqueda, ordenarPor, direccion, vista, pagina });
        }
    }
}
