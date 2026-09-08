using MiHotel.Data;
using MySql.Data.MySqlClient;

namespace MiHotel.Services;

public sealed class PermisosUsuarioService
{
    private readonly ConexionBD _conexionBD;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private HashSet<string>? _permisos;

    public PermisosUsuarioService(
        ConexionBD conexionBD,
        IHttpContextAccessor httpContextAccessor)
    {
        _conexionBD = conexionBD;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool TienePermiso(string nombrePermiso)
    {
        HttpContext? contexto = _httpContextAccessor.HttpContext;
        if (contexto == null || string.IsNullOrWhiteSpace(contexto.Session.GetString("IdUsuario")))
            return false;

        string rol = contexto.Session.GetString("NombreRol")?.Trim().ToLowerInvariant() ?? "";
        if (rol == "admin") return true;

        if (!int.TryParse(contexto.Session.GetString("IdRol"), out int idRol))
            return false;

        _permisos ??= CargarPermisos(idRol);
        return _permisos.Contains(nombrePermiso.Trim());
    }

    public bool TieneAlguno(params string[] nombresPermisos) =>
        nombresPermisos.Any(TienePermiso);

    private HashSet<string> CargarPermisos(int idRol)
    {
        var permisos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var conexion = _conexionBD.ObtenerConexion();
            conexion.Open();
            using var comando = new MySqlCommand(@"
                SELECT p.nombre_permiso
                FROM rol_permiso rp
                INNER JOIN permisos p ON p.id_permiso = rp.id_permiso
                INNER JOIN rol r ON r.id_rol = rp.id_rol
                WHERE rp.id_rol = @id_rol
                  AND p.estado = 1
                  AND LOWER(r.estado) = 'activo';", conexion);
            comando.Parameters.AddWithValue("@id_rol", idRol);

            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                string nombre = lector["nombre_permiso"]?.ToString()?.Trim() ?? "";
                if (nombre.Length > 0) permisos.Add(nombre);
            }
        }
        catch
        {
            // Ante una falla de base de datos se deniega el acceso.
        }

        return permisos;
    }
}
