using System;
using System.IO;
using System.Text;
using AppInsegura.Datos;
using AppInsegura.Modelos;
using AppInsegura.Servicios;

namespace AppInsegura
{
    public class Program
    {
        private const int LongitudMaximaEntrada = 128;

        private static readonly BaseDatosUsuarios baseDatos = new BaseDatosUsuarios();
        private static readonly AuthService auth = new AuthService(baseDatos);
        private static Sesion? sesionActual = null;

        public static void Main(string[] args)
        {
            Console.WriteLine("=== Gestor de Usuarios y Partidas ===");
            Console.WriteLine();

            try
            {
                ConfigurarAdministradorInicial();
                BuclePrincipal();
            }
            catch (EndOfStreamException)
            {
                // Se ha cerrado la entrada estándar: salimos sin quedarnos en bucle.
            }

            Console.WriteLine("Hasta luego.");
        }

        private static void BuclePrincipal()
        {
            bool salir = false;
            while (!salir)
            {
                ComprobarSesion();
                MostrarMenu();
                string opcion = LeerLinea();

                try
                {
                    switch (opcion)
                    {
                        case "1":
                            Registrar();
                            break;
                        case "2":
                            IniciarSesion();
                            break;
                        case "3":
                            BuscarUsuario();
                            break;
                        case "4":
                            VerPerfil();
                            break;
                        case "5":
                            PanelAdministracion();
                            break;
                        case "6":
                            SincronizarConServidor();
                            break;
                        case "7":
                            CerrarSesion();
                            break;
                        case "0":
                            salir = true;
                            break;
                        default:
                            Console.WriteLine("Opción no válida.");
                            break;
                    }
                }
                catch (Exception ex) when (ex is not EndOfStreamException)
                {
                    // Nunca se muestran detalles internos (trazas, rutas, clases).
                    Console.WriteLine("Ha ocurrido un error inesperado. Inténtalo de nuevo.");
                }

                Console.WriteLine();
            }
        }

        // Sustituye a los usuarios con contraseñas fijas en el código: la primera
        // vez se pide crear el administrador con una contraseña robusta.
        private static void ConfigurarAdministradorInicial()
        {
            if (baseDatos.HayAdministrador())
            {
                return;
            }

            Console.WriteLine("Configuración inicial: crea la cuenta de administrador.");
            while (!baseDatos.HayAdministrador())
            {
                Console.Write("Nombre del administrador: ");
                string nombre = LeerLinea();
                string? contrasena = PedirContrasenaNueva();
                if (contrasena == null)
                {
                    continue;
                }

                if (!auth.CrearAdministradorInicial(nombre, contrasena, out string error))
                {
                    Console.WriteLine(error);
                }
            }

            Console.WriteLine("Administrador creado.");
            Console.WriteLine();
        }

        private static void MostrarMenu()
        {
            Console.WriteLine("------------------------------------");
            Console.WriteLine($"Usuario actual: {(sesionActual != null ? sesionActual.Usuario.Nombre : "ninguno")}");
            Console.WriteLine("1. Registrar usuario");
            Console.WriteLine("2. Iniciar sesión");
            Console.WriteLine("3. Buscar usuario por nombre");
            Console.WriteLine("4. Ver mi perfil");
            if (auth.EsAdministrador(sesionActual))
            {
                Console.WriteLine("5. Panel de administración");
            }
            Console.WriteLine("6. Sincronizar partida con el servidor");
            if (sesionActual != null)
            {
                Console.WriteLine("7. Cerrar sesión");
            }
            Console.WriteLine("0. Salir");
            Console.Write("Elige una opción: ");
        }

        private static void Registrar()
        {
            Console.Write("Nombre de usuario: ");
            string nombre = LeerLinea();
            string? contrasena = PedirContrasenaNueva();
            if (contrasena == null)
            {
                return;
            }

            if (!auth.Registrar(nombre, contrasena, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            Console.WriteLine($"Usuario '{nombre}' registrado.");
        }

        private static void IniciarSesion()
        {
            Console.Write("Nombre de usuario: ");
            string nombre = LeerLinea();
            Console.Write("Contraseña: ");
            string contrasena = LeerContrasena();

            switch (auth.IniciarSesion(nombre, contrasena, out Sesion? sesion))
            {
                case ResultadoLogin.Correcto:
                    if (sesionActual != null)
                    {
                        auth.CerrarSesion(sesionActual);
                    }
                    sesionActual = sesion;
                    Console.WriteLine($"Bienvenido, {sesion!.Usuario.Nombre}.");
                    break;
                case ResultadoLogin.Bloqueado:
                    Console.WriteLine("Demasiados intentos fallidos. Espera unos minutos y vuelve a intentarlo.");
                    break;
                default:
                    // Mismo mensaje exista o no el usuario, para no revelar cuáles existen.
                    Console.WriteLine("Usuario o contraseña incorrectos.");
                    break;
            }
        }

        private static void BuscarUsuario()
        {
            if (!RequiereSesion())
            {
                return;
            }

            Console.Write("Nombre a buscar: ");
            string nombre = LeerLinea();
            if (!AuthService.EsNombreValido(nombre))
            {
                Console.WriteLine("Nombre no válido.");
                return;
            }

            Usuario? encontrado = baseDatos.BuscarPorNombre(nombre);
            Console.WriteLine(encontrado != null
                ? $"Encontrado: {encontrado.Nombre}"
                : "No se ha encontrado ningún usuario con ese nombre.");
        }

        private static void VerPerfil()
        {
            if (!RequiereSesion())
            {
                return;
            }

            // El token de sesión no se muestra nunca.
            Console.WriteLine($"Nombre: {sesionActual!.Usuario.Nombre}");
            Console.WriteLine($"Rol: {sesionActual.Usuario.Rol}");
            Console.WriteLine($"La sesión caduca a las {sesionActual.ExpiraUtc.ToLocalTime():HH:mm}.");
        }

        private static void PanelAdministracion()
        {
            // El permiso se comprueba aquí, no solo al pintar el menú.
            if (!auth.EsAdministrador(sesionActual))
            {
                Console.WriteLine("Opción no válida.");
                return;
            }

            Console.WriteLine("=== PANEL DE ADMINISTRACIÓN ===");
            Console.WriteLine("Lista de usuarios registrados:");
            foreach (Usuario u in baseDatos.ListarTodos())
            {
                Console.WriteLine($" - {u.Nombre} ({u.Rol})");
            }
        }

        private static void SincronizarConServidor()
        {
            if (!RequiereSesion())
            {
                return;
            }

            var red = new RedService();
            red.EnviarPuntuacion(sesionActual!.Usuario.Nombre, 1000);
        }

        private static void CerrarSesion()
        {
            if (sesionActual == null)
            {
                Console.WriteLine("Opción no válida.");
                return;
            }

            auth.CerrarSesion(sesionActual);
            sesionActual = null;
            Console.WriteLine("Sesión cerrada.");
        }

        private static bool RequiereSesion()
        {
            if (!auth.EsSesionValida(sesionActual))
            {
                sesionActual = null;
                Console.WriteLine("Primero debes iniciar sesión.");
                return false;
            }

            return true;
        }

        private static void ComprobarSesion()
        {
            if (sesionActual != null && !auth.EsSesionValida(sesionActual))
            {
                sesionActual = null;
                Console.WriteLine("Tu sesión ha caducado. Vuelve a iniciar sesión.");
            }
        }

        private static string? PedirContrasenaNueva()
        {
            Console.Write("Contraseña (mínimo 10 caracteres, con letras y números): ");
            string contrasena = LeerContrasena();
            Console.Write("Repite la contraseña: ");
            string repetida = LeerContrasena();

            if (contrasena != repetida)
            {
                Console.WriteLine("Las contraseñas no coinciden.");
                return null;
            }

            return contrasena;
        }

        private static string LeerLinea()
        {
            string linea = Console.ReadLine() ?? throw new EndOfStreamException();
            linea = linea.Trim();
            return linea.Length > LongitudMaximaEntrada ? linea.Substring(0, LongitudMaximaEntrada) : linea;
        }

        // Lee la contraseña sin mostrarla en pantalla (se ve un '*' por carácter).
        private static string LeerContrasena()
        {
            if (Console.IsInputRedirected)
            {
                return Console.ReadLine() ?? throw new EndOfStreamException();
            }

            var contrasena = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo tecla = Console.ReadKey(intercept: true);
                if (tecla.Key == ConsoleKey.Enter)
                {
                    break;
                }

                if (tecla.Key == ConsoleKey.Backspace)
                {
                    if (contrasena.Length > 0)
                    {
                        contrasena.Length--;
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(tecla.KeyChar) && contrasena.Length < LongitudMaximaEntrada)
                {
                    contrasena.Append(tecla.KeyChar);
                    Console.Write('*');
                }
            }

            Console.WriteLine();
            return contrasena.ToString();
        }
    }
}
