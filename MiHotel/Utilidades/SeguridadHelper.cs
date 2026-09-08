using System.Security.Cryptography;
using System.Text;

namespace MiHotel.Utilidades
{
    public static class SeguridadHelper
    {
        private const int IteracionesPbkdf2 = 210_000;
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;
        private const string PrefijoPbkdf2 = "PBKDF2-SHA256";

        public static string CrearHashClave(string clave)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(TamanoSal);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                clave,
                sal,
                IteracionesPbkdf2,
                HashAlgorithmName.SHA256,
                TamanoHash);

            return $"{PrefijoPbkdf2}${IteracionesPbkdf2}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerificarClave(string clave, string valorAlmacenado, out bool requiereActualizacion)
        {
            requiereActualizacion = false;
            if (string.IsNullOrWhiteSpace(valorAlmacenado)) return false;

            if (valorAlmacenado.StartsWith(PrefijoPbkdf2 + "$", StringComparison.Ordinal))
            {
                string[] partes = valorAlmacenado.Split('$');
                if (partes.Length != 4 || !int.TryParse(partes[1], out int iteraciones)) return false;

                try
                {
                    byte[] sal = Convert.FromBase64String(partes[2]);
                    byte[] esperado = Convert.FromBase64String(partes[3]);
                    byte[] obtenido = Rfc2898DeriveBytes.Pbkdf2(
                        clave,
                        sal,
                        iteraciones,
                        HashAlgorithmName.SHA256,
                        esperado.Length);
                    bool coincide = CryptographicOperations.FixedTimeEquals(esperado, obtenido);
                    requiereActualizacion = coincide && iteraciones < IteracionesPbkdf2;
                    return coincide;
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            // Compatibilidad temporal: al iniciar sesión correctamente, un hash SHA-256
            // antiguo se reemplaza inmediatamente por PBKDF2 sin pedir otra contraseña.
            if (valorAlmacenado.Length == 64 && valorAlmacenado.All(Uri.IsHexDigit))
            {
                byte[] esperado = Convert.FromHexString(valorAlmacenado);
                byte[] obtenido = SHA256.HashData(Encoding.UTF8.GetBytes(clave));
                bool coincide = CryptographicOperations.FixedTimeEquals(esperado, obtenido);
                requiereActualizacion = coincide;
                return coincide;
            }

            return false;
        }

        [Obsolete("Utilice CrearHashClave y VerificarClave. Se conserva únicamente para compatibilidad.")]
        public static string ObtenerSha256(string texto)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytesTexto = Encoding.UTF8.GetBytes(texto);
                byte[] bytesHash = sha256.ComputeHash(bytesTexto);

                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytesHash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
