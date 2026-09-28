using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AppInsegura.Datos;
using AppInsegura.Modelos;

namespace AppInsegura.Servicios
{
    public enum ResultadoLogin
    {
        Correcto,
        Incorrecto,
        Bloqueado
    }

    public class AuthService
    {
        // PBKDF2-HMAC-SHA256 con los parámetros recomendados por OWASP.
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;
        private const int Iteraciones = 600_000;

        private const int LongitudMinimaContrasena = 10;
        private const int LongitudMaximaContrasena = 128;
        private const int MaxIntentosFallidos = 5;
        private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DuracionSesion = TimeSpan.FromMinutes(30);

        private static readonly Regex FormatoNombre = new Regex("^[a-zA-Z0-9_]{3,20}$");
        private static readonly string[] NombresReservados =
        {
            "admin", "administrador", "administrator", "root", "sistema", "soporte"
        };
        private static readonly string[] ContrasenasComunes =
        {
            "1234567890", "0123456789", "contrasena1", "password123", "qwertyuiop",
            "admin12345", "123456789a", "abcdefghij", "iloveyou12"
        };

        private readonly BaseDatosUsuarios baseDatos;
        private readonly Dictionary<string, Sesion> sesionesActivas = new Dictionary<string, Sesion>();
        private readonly Dictionary<string, IntentosLogin> intentos =
            new Dictionary<string, IntentosLogin>(StringComparer.OrdinalIgnoreCase);

        // Se usan cuando el usuario no existe, para que el login tarde lo mismo
        // y no se pueda averiguar qué usuarios existen midiendo el tiempo.
        private readonly byte[] salFicticia = RandomNumberGenerator.GetBytes(TamanoSal);
        private readonly byte[] hashFicticio;

        public AuthService(BaseDatosUsuarios baseDatos)
        {
            this.baseDatos = baseDatos;
            hashFicticio = CalcularHash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), salFicticia);
        }

        public static bool EsNombreValido(string nombre)
        {
            return FormatoNombre.IsMatch(nombre);
        }

        // El rol no se recibe desde fuera: los registros públicos siempre son de jugador.
        public bool Registrar(string nombre, string contrasena, out string error)
        {
            // Evita que un jugador se haga pasar por el administrador o el sistema.
            if (NombresReservados.Contains(nombre, StringComparer.OrdinalIgnoreCase))
            {
                error = "Ese nombre de usuario no está disponible.";
                return false;
            }

            return CrearUsuario(nombre, contrasena, Roles.Jugador, out error);
        }

        // Solo permite crear el administrador en la configuración inicial.
        public bool CrearAdministradorInicial(string nombre, string contrasena, out string error)
        {
            if (baseDatos.HayAdministrador())
            {
                error = "Ya existe un administrador.";
                return false;
            }

            return CrearUsuario(nombre, contrasena, Roles.Admin, out error);
        }

        public ResultadoLogin IniciarSesion(string nombre, string contrasena, out Sesion? sesion)
        {
            sesion = null;

            if (!EsNombreValido(nombre))
            {
                CalcularHash(contrasena, salFicticia);
                return ResultadoLogin.Incorrecto;
            }

            if (EstaBloqueado(nombre))
            {
                return ResultadoLogin.Bloqueado;
            }

            Usuario? usuario = baseDatos.BuscarPorNombre(nombre);
            byte[] hashIntento = CalcularHash(contrasena, usuario?.Sal ?? salFicticia);
            bool hashCorrecto = CryptographicOperations.FixedTimeEquals(
                hashIntento, usuario?.ContrasenaHash ?? hashFicticio);

            if (usuario == null || !hashCorrecto)
            {
                RegistrarFallo(nombre);
                return ResultadoLogin.Incorrecto;
            }

            intentos.Remove(nombre);
            sesion = new Sesion(usuario, GenerarTokenSesion(), DateTime.UtcNow + DuracionSesion);
            sesionesActivas[sesion.Token] = sesion;
            return ResultadoLogin.Correcto;
        }

        public bool EsSesionValida(Sesion? sesion)
        {
            if (sesion == null
                || !sesionesActivas.TryGetValue(sesion.Token, out Sesion? registrada)
                || !ReferenceEquals(registrada, sesion))
            {
                return false;
            }

            if (sesion.Caducada)
            {
                sesionesActivas.Remove(sesion.Token);
                return false;
            }

            return true;
        }

        public bool EsAdministrador(Sesion? sesion)
        {
            return EsSesionValida(sesion) && sesion!.Usuario.Rol == Roles.Admin;
        }

        public void CerrarSesion(Sesion sesion)
        {
            sesionesActivas.Remove(sesion.Token);
        }

        private bool CrearUsuario(string nombre, string contrasena, string rol, out string error)
        {
            if (!EsNombreValido(nombre))
            {
                error = "El nombre debe tener entre 3 y 20 caracteres (letras, números o '_').";
                return false;
            }

            if (!EsContrasenaValida(nombre, contrasena, out error))
            {
                return false;
            }

            byte[] sal = RandomNumberGenerator.GetBytes(TamanoSal);
            var nuevo = new Usuario(nombre, rol, sal, CalcularHash(contrasena, sal));

            if (!baseDatos.Agregar(nuevo))
            {
                error = "Ese nombre de usuario no está disponible.";
                return false;
            }

            error = "";
            return true;
        }

        private static bool EsContrasenaValida(string nombre, string contrasena, out string error)
        {
            error = "";

            if (contrasena.Length < LongitudMinimaContrasena || contrasena.Length > LongitudMaximaContrasena)
            {
                error = $"La contraseña debe tener entre {LongitudMinimaContrasena} y {LongitudMaximaContrasena} caracteres.";
            }
            else if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
            {
                error = "La contraseña debe contener letras y números.";
            }
            else if (contrasena.Contains(nombre, StringComparison.OrdinalIgnoreCase))
            {
                error = "La contraseña no puede contener el nombre de usuario.";
            }
            else if (ContrasenasComunes.Contains(contrasena, StringComparer.OrdinalIgnoreCase))
            {
                error = "Esa contraseña es demasiado común.";
            }

            return error == "";
        }

        private bool EstaBloqueado(string nombre)
        {
            return intentos.TryGetValue(nombre, out IntentosLogin? registro)
                && registro.BloqueadoHastaUtc > DateTime.UtcNow;
        }

        private void RegistrarFallo(string nombre)
        {
            if (!intentos.TryGetValue(nombre, out IntentosLogin? registro))
            {
                registro = new IntentosLogin();
                intentos[nombre] = registro;
            }

            registro.Fallos++;
            if (registro.Fallos >= MaxIntentosFallidos)
            {
                registro.Fallos = 0;
                registro.BloqueadoHastaUtc = DateTime.UtcNow + DuracionBloqueo;
            }
        }

        private static byte[] CalcularHash(string contrasena, byte[] sal)
        {
            return Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        }

        // Generador criptográfico y 256 bits: imposible de adivinar por fuerza bruta.
        private static string GenerarTokenSesion()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private class IntentosLogin
        {
            public int Fallos { get; set; }
            public DateTime BloqueadoHastaUtc { get; set; }
        }
    }
}
