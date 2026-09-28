# AppInsegura — versión corregida

Programació de serveis i processos · Grupo 5

`AppInsegura` es una aplicación de consola en C# (.NET 10) que gestiona usuarios
y partidas: registro, inicio de sesión, búsqueda de usuarios, perfil, panel de
administración y sincronización de la puntuación con un servidor. La versión
original contenía fallos de seguridad deliberados. Este repositorio contiene la
versión **auditada y corregida**.

## Cómo ejecutarla

Requisitos: [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
cd AppInsegura
dotnet run
```

- **Primer arranque:** la aplicación pide crear la cuenta de administrador. Ya
  no existen usuarios con contraseñas fijas. Los datos se guardan solo en
  memoria, así que se pide en cada arranque.
- **Contraseñas:** mínimo 10 caracteres, con letras y números, sin contener el
  nombre de usuario y fuera de una lista de contraseñas comunes. Al escribirlas
  solo se ven asteriscos.
- **Sincronización (opcional):** la clave de la API se lee de la variable de
  entorno `MIAPP_API_KEY`. Si no está definida, la opción 6 avisa de que no está
  configurada.

  ```powershell
  $env:MIAPP_API_KEY = "tu-clave"
  dotnet run
  ```

## Estructura

```
AppInsegura/
├── Program.cs                 Menú de consola y lectura segura de entradas
├── Modelos/Usuario.cs         Usuario inmutable (nombre, rol, sal, hash)
├── Modelos/Sesion.cs          Sesión en memoria con caducidad
├── Datos/BaseDatosUsuarios.cs Tabla de usuarios simulada
└── Servicios/
    ├── AuthService.cs         Registro, login, hashing, sesiones y bloqueo
    └── RedService.cs          Envío de la puntuación al servidor
```

## Vulnerabilidades encontradas y corrección

| # | Vulnerabilidad | Dónde estaba | Cómo se ha corregido |
|---|---|---|---|
| 1 | Control de acceso roto: el panel de administración solo se ocultaba en el menú; escribiendo `5` entraba cualquiera | `Program.cs` | El rol se comprueba dentro de `PanelAdministracion()` con `auth.EsAdministrador()` |
| 2 | Inyección SQL en la búsqueda (`' OR '1'='1` devolvía el admin) | `BaseDatosUsuarios.cs` | Se elimina la concatenación; búsqueda equivalente a una consulta parametrizada y validación del formato del nombre |
| 3 | API key `sk_live_...` escrita en el código | `RedService.cs` | Se lee de la variable de entorno `MIAPP_API_KEY` |
| 4 | La API key viajaba por HTTP, en la URL, y se imprimía por pantalla | `RedService.cs` | HTTPS, petición POST con JSON y la clave en la cabecera `X-Api-Key`; no se imprime la URL |
| 5 | Contraseñas con MD5 sin sal | `AuthService.cs` | PBKDF2-HMAC-SHA256, 600.000 iteraciones y sal aleatoria por usuario |
| 6 | Credenciales de prueba mostradas al arrancar y en el README | `Program.cs`, README | Eliminadas |
| 7 | Usuarios por defecto con contraseñas débiles (`admin1234`) | `Program.cs` | El administrador se crea en el primer arranque con contraseña robusta |
| 8 | La contraseña se veía al escribirla | `Program.cs` | Lectura con `Console.ReadKey(true)` mostrando `*` |
| 9 | Token de sesión de 6 dígitos con `System.Random` | `AuthService.cs` | 32 bytes con `RandomNumberGenerator` |
| 10 | El token se escribía en el log | `AuthService.cs` | Eliminado |
| 11 | El token se mostraba en "Ver mi perfil" | `Program.cs` | Eliminado; solo se muestra la hora de caducidad |
| 12 | Sesión guardada en `sesion.txt` en texto plano | `AuthService.cs` | La sesión vive solo en memoria |
| 13 | Sin cierre de sesión y el token no caducaba | `AuthService.cs` | Opción 7 "Cerrar sesión" y caducidad de 30 minutos |
| 14 | Se mostraba la traza completa de las excepciones | `Program.cs`, `RedService.cs` | Mensajes genéricos sin detalles internos |
| 15 | Se imprimía la consulta SQL (`[DB] SELECT ...`) | `BaseDatosUsuarios.cs` | Eliminado |
| 16 | Enumeración de usuarios sin iniciar sesión | `Program.cs` | Buscar, perfil y sincronizar exigen sesión válida |
| 17 | Enumeración por tiempo de respuesta y comparación no constante | `AuthService.cs` | Hash ficticio si el usuario no existe y `CryptographicOperations.FixedTimeEquals` |
| 18 | Sin protección contra fuerza bruta | `AuthService.cs` | Bloqueo de 5 minutos tras 5 intentos fallidos (también para nombres inexistentes) |
| 19 | Registro sin validación y con nombres duplicados | `AuthService.cs`, `BaseDatosUsuarios.cs` | Formato de nombre, política de contraseñas, confirmación y unicidad sin distinguir mayúsculas |
| 20 | Rol asignable desde fuera y propiedades modificables | `AuthService.cs`, `Usuario.cs` | El registro público siempre crea `jugador`; `Usuario` es inmutable y se reservan nombres como `admin` o `root` |
| 21 | `ListarTodos()` devolvía la lista interna | `BaseDatosUsuarios.cs` | Devuelve una copia de solo lectura |
| 22 | El usuario no se codificaba en la URL (*parameter pollution*) | `RedService.cs` | Los datos van en el cuerpo JSON |
| 23 | Símbolos de depuración y `sesion.txt` con un token en `bin/` | proyecto | En Release no se generan `.pdb`; `bin/`, `obj/` y `.vs/` están en `.gitignore` |

## Verificación realizada

Sobre un clon limpio de este repositorio:

- Compila en Debug y en Release sin avisos ni errores, y en Release no se genera `.pdb`.
- Se probaron los ataques de la auditoría: acceso al panel sin permiso,
  `' OR '1'='1` en búsqueda y login, contraseñas débiles, nombres duplicados o
  reservados, y fuerza bruta. Todos quedan bloqueados.
- La salida del programa no contiene contraseñas, tokens, la API key, consultas
  SQL ni trazas de excepciones, y la ejecución no escribe ningún fichero en disco.
- El historial de git no contiene la API key ni las contraseñas originales.

## Limitaciones conocidas

- La base de datos es una lista en memoria; con un motor real habría que usar
  consultas parametrizadas como indica el comentario de `BaseDatosUsuarios.cs`.
- `internal` en `Usuario` y `Sesion` documenta la intención, pero no aísla nada
  dentro de un único proyecto.
- La puntuación la sigue enviando el cliente: el servidor debe validarla por su
  cuenta y no fiarse de ella.
