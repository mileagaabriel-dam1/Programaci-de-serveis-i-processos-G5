using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AppInsegura.Servicios
{
    public class RedService
    {
        // La clave nunca va en el código: se lee de una variable de entorno.
        private const string VariableApiKey = "MIAPP_API_KEY";

        // Siempre HTTPS, para que la clave y los datos viajen cifrados.
        private static readonly Uri UrlServidor = new Uri("https://api.miapp-insegura.local/puntuaciones");
        private static readonly HttpClient Cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Nota: el servidor debe validar la puntuación por su cuenta y no fiarse
        // de lo que le envía el cliente.
        public void EnviarPuntuacion(string nombreUsuario, int puntuacion)
        {
            string? apiKey = Environment.GetEnvironmentVariable(VariableApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("La sincronización con el servidor no está configurada.");
                return;
            }

            try
            {
                bool aceptada = EnviarPuntuacionAsync(apiKey, nombreUsuario, puntuacion).GetAwaiter().GetResult();
                Console.WriteLine(aceptada
                    ? "Puntuación sincronizada."
                    : "El servidor no ha aceptado la puntuación.");
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Console.WriteLine("No se ha podido conectar con el servidor. Inténtalo más tarde.");
            }
        }

        private static async Task<bool> EnviarPuntuacionAsync(string apiKey, string nombreUsuario, int puntuacion)
        {
            // Los datos van en el cuerpo JSON (codificados correctamente) y la clave
            // en una cabecera, no en la URL, que acaba guardada en logs y proxies.
            using var peticion = new HttpRequestMessage(HttpMethod.Post, UrlServidor)
            {
                Content = JsonContent.Create(new { usuario = nombreUsuario, puntos = puntuacion })
            };
            peticion.Headers.Add("X-Api-Key", apiKey);

            using HttpResponseMessage respuesta = await Cliente.SendAsync(peticion);
            return respuesta.IsSuccessStatusCode;
        }
    }
}
