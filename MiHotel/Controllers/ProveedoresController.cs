// Controlador de proveedores - CRUD completo alineado a clientes

using Microsoft.AspNetCore.Mvc;
using MiHotel.Data;
using MiHotel.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace MiHotel.Controllers
{
    public class ProveedoresController : Controller
    {
        private readonly ConexionBD _conexionBD;
        private const int RegistrosPorPagina = 20;

        public ProveedoresController(ConexionBD conexionBD)
        {
            _conexionBD = conexionBD;
        }

        private bool TieneSesionActiva()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("IdUsuario"));
        }

        private IActionResult? ValidarSesion()
        {
            if (!TieneSesionActiva())
                return RedirectToAction("Login", "Acceso");

            return null;
        }

        private string NormalizarTelefono(string telefono)
        {
            return telefono.Replace(" ", "").Trim();
        }

        private string FormatearTelefono(string telefono)
        {
            string t = NormalizarTelefono(telefono);
            return t.Length == 8 ? t.Substring(0, 4) + " " + t.Substring(4, 4) : telefono;
        }

        private int ObtenerIdTipoProveedor(MySqlConnection conexion)
        {
            string sql = "SELECT id_tipoclipro FROM tipo_clipro WHERE LOWER(tipo)='proveedor' LIMIT 1";
            using var cmd = new MySqlCommand(sql, conexion);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ========================= INDEX =========================
        public IActionResult Index(string busqueda = "", string vista = "activos", int pagina = 1)
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            DataTable tabla = new DataTable();

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            int tipo = ObtenerIdTipoProveedor(conexion);



            string estado = vista == "inactivos" ? "inactivo" : "activo";

            string sql = @"
            SELECT * FROM clipro
            WHERE id_tipoclipro=@tipo 
            AND estado=@estado
            AND nombre LIKE @busqueda";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@tipo", tipo);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@busqueda", "%" + busqueda + "%");

            new MySqlDataAdapter(cmd).Fill(tabla);
           
            ViewBag.Vista = vista;
            ViewBag.Busqueda = busqueda;
            ViewBag.PaginaActual = pagina;


            return View(tabla);
        }

        // ========================= CREAR =========================
        [HttpGet]
        public IActionResult Crear()
        {
            var acceso = ValidarSesion();
            if (acceso != null) return acceso;

            return View(new ClienteAdmin());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(ClienteAdmin modelo)
        {
            if (!ModelState.IsValid) return View(modelo);

            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            int tipo = ObtenerIdTipoProveedor(conexion);

            // ================= VALIDACIÓN DE DUPLICADOS =================
            string validar = @"SELECT COUNT(*) 
                       FROM clipro 
                       WHERE LOWER(nombre) = LOWER(@nombre)
                       AND id_tipoclipro = @tipo";

            using (var cmdValidar = new MySqlCommand(validar, conexion))
            {
                cmdValidar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                cmdValidar.Parameters.AddWithValue("@tipo", tipo);

                int existe = Convert.ToInt32(cmdValidar.ExecuteScalar());

                if (existe > 0)
                {
                    ModelState.AddModelError("", "El proveedor ya existe.");
                    return View(modelo);
                }
            }
            // ============================================================

            string sql = @"INSERT INTO clipro
            (id_tipoclipro,nombre,nit,telefono,correo,direccion,nombre_empresa,numero_empresa,estado)
            VALUES (@tipo,@nombre,@nit,@tel,@correo,@dir,@emp,@num,'activo')";

            using var cmd = new MySqlCommand(sql, conexion);

            cmd.Parameters.AddWithValue("@tipo", tipo);
            cmd.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
            cmd.Parameters.AddWithValue("@nit", (object?)modelo.Nit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tel", NormalizarTelefono(modelo.Telefono));
            cmd.Parameters.AddWithValue("@correo", (object?)modelo.Correo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dir", (object?)modelo.Direccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@emp", (object?)modelo.NombreEmpresa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@num", (object?)modelo.NumeroEmpresa ?? DBNull.Value);

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        // ========================= EDITAR =========================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = "SELECT * FROM clipro WHERE id_clipro=@id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var dr = cmd.ExecuteReader();

            if (!dr.Read()) return RedirectToAction("Index");

            var m = new EditarCliente
            {
                IdClipro = id,
                Nombre = dr["nombre"].ToString(),
                Nit = dr["nit"]?.ToString(),
                Telefono = dr["telefono"].ToString(),
                Correo = dr["correo"]?.ToString(),
                Direccion = dr["direccion"]?.ToString(),
                NombreEmpresa = dr["nombre_empresa"]?.ToString(),
                NumeroEmpresa = dr["numero_empresa"]?.ToString()
            };

            return View(m);
        }

        [HttpPost]
        public IActionResult Editar(EditarCliente modelo)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            // ================= VALIDACIÓN DE DUPLICADOS =================
            int tipo = ObtenerIdTipoProveedor(conexion);

            string validar = @"SELECT COUNT(*) 
                   FROM clipro 
                   WHERE LOWER(nombre) = LOWER(@nombre)
                   AND id_clipro != @id
                   AND id_tipoclipro = @tipo";

            using (var cmdValidar = new MySqlCommand(validar, conexion))
            {
                cmdValidar.Parameters.AddWithValue("@nombre", modelo.Nombre.Trim());
                cmdValidar.Parameters.AddWithValue("@id", modelo.IdClipro);
                cmdValidar.Parameters.AddWithValue("@tipo", tipo);

                int existe = Convert.ToInt32(cmdValidar.ExecuteScalar());

                if (existe > 0)
                {
                    ModelState.AddModelError("", "Ya existe otro proveedor con ese nombre.");
                    return View(modelo);
                }
            }
            // ============================================================

            string sql = @"UPDATE clipro SET
                nombre=@n, nit=@nit, telefono=@tel,
                correo=@c, direccion=@d,
                nombre_empresa=@e, numero_empresa=@ne
                WHERE id_clipro=@id";

            using var cmd = new MySqlCommand(sql, conexion);

            cmd.Parameters.AddWithValue("@n", modelo.Nombre);
            cmd.Parameters.AddWithValue("@nit", (object?)modelo.Nit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tel", NormalizarTelefono(modelo.Telefono));
            cmd.Parameters.AddWithValue("@c", (object?)modelo.Correo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)modelo.Direccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)modelo.NombreEmpresa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ne", (object?)modelo.NumeroEmpresa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", modelo.IdClipro);

            cmd.ExecuteNonQuery();

            return RedirectToAction("Index");
        }

        // ========================= DETALLE =========================
        public IActionResult Detalle(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string sql = "SELECT * FROM clipro WHERE id_clipro=@id";

            using var cmd = new MySqlCommand(sql, conexion);
            cmd.Parameters.AddWithValue("@id", id);

            using var dr = cmd.ExecuteReader();

            if (!dr.Read()) return RedirectToAction("Index");

            var m = new ClienteDetalleViewModel
            {
                IdClipro = id,
                Nombre = dr["nombre"].ToString(),
                Nit = dr["nit"]?.ToString(),
                Telefono = dr["telefono"].ToString(),
                Correo = dr["correo"]?.ToString(),
                Direccion = dr["direccion"]?.ToString(),
                NombreEmpresa = dr["nombre_empresa"]?.ToString(),
                NumeroEmpresa = dr["numero_empresa"]?.ToString(),
                Estado = dr["estado"].ToString()
            };

            return View(m);
        }

        // ========================= ESTADO =========================
        [HttpPost]
        public IActionResult CambiarEstado(int id)
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();

            string estado = new MySqlCommand(
                "SELECT estado FROM clipro WHERE id_clipro=@id", conexion)
            { Parameters = { new("@id", id) } }
            .ExecuteScalar()?.ToString();

            string nuevo = estado == "activo" ? "inactivo" : "activo";

            new MySqlCommand(
                "UPDATE clipro SET estado=@e WHERE id_clipro=@id", conexion)
            {
                Parameters =
                {
                    new("@e", nuevo),
                    new("@id", id)
                }
            }.ExecuteNonQuery();

            return RedirectToAction("Index");
        }
    }
}