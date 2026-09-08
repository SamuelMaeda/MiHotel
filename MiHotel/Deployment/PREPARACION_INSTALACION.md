# Preparación de MiHotel para instalación autónoma

Este directorio contiene los componentes técnicos que utilizará el instalador final. Todavía no constituye el instalador `.exe`.

## Resultado actual

- La versión preparada corresponde a MiHotel 1.0.0.
- La aplicación puede publicarse para Windows incluyendo el runtime de .NET; el equipo del hotel no necesitará Visual Studio ni instalar ASP.NET por separado.
- La conexión de producción queda fuera de los archivos publicados y se almacena en `%ProgramData%\MiHotel\Config\database.json`.
- Los registros se guardan diariamente en `%ProgramData%\MiHotel\Logs`.
- Las claves de sesión sobreviven a reinicios en `%ProgramData%\MiHotel\Keys`.
- Los respaldos se guardan por defecto en `%ProgramData%\MiHotel\Backups` y se conservan durante 90 días.
- Las migraciones SQL pendientes se ejecutan automáticamente antes de que la aplicación acepte solicitudes.
- Las contraseñas nuevas utilizan PBKDF2-SHA256; las contraseñas SHA-256 antiguas se actualizan tras un inicio de sesión válido.
- El servidor escucha únicamente en `127.0.0.1:5265` y además rechaza solicitudes remotas.

## Datos previstos para la instalación del hotel

- 12 habitaciones disponibles inicialmente.
- Una única subcategoría de habitación: `Viajero`, con tarifa de Q250.00.
- Catálogo de productos y servicios sin registros operativos iniciales.
- Configuración predeterminada: recargo por tarjeta Q25.00, IVA 12 % e INGUAT 10 %.
- Usuarios iniciales: Alejandra como administradora y dos cuentas de recepción, `recepciondia` y `recepcionnoche`. Sus contraseñas se almacenan únicamente como hashes PBKDF2 en la base de datos.
- Las cuentas de recepción comparten el rol y los permisos operativos de recepcionista, pero conservan identidades separadas para distinguir cada turno en los registros del sistema.
- El inicio de sesión admite tanto correo electrónico como nombre de usuario.
- La sesión no vence por inactividad. Se cierra manualmente, al detenerse el sistema por apagado o después de que el equipo se reanuda desde una suspensión.

## Herramientas preparadas

- `PrepararPublicacion.ps1`: genera la carpeta que posteriormente empaquetará el instalador.
- `InicializarInstalacion.ps1`: crea una base vacía, un usuario MySQL exclusivo y el primer administrador de MiHotel.
- `CrearRespaldo.ps1`: produce un respaldo transaccional completo y un manifiesto SHA-256.
- `RestaurarRespaldo.ps1`: valida y restaura un respaldo, conservando primero una copia del estado reemplazado.
- `IniciarMiHotel.ps1`: inicia la aplicación en segundo plano, comprueba `/estado` y abre el navegador.
- `RegistrarTareasMiHotel.ps1`: prepara el inicio automático y un respaldo diario a las 23:00.
- `DesregistrarTareasMiHotel.ps1`: retira esas tareas durante una desinstalación sin borrar los datos.

Los scripts nunca deben incorporarse al menú cotidiano del hotel. El instalador y las herramientas administrativas los utilizarán con los permisos correspondientes.

## Publicación de comprobación

Desde PowerShell, en este directorio:

```powershell
.\PrepararPublicacion.ps1
```

El resultado queda en `Publicacion\MiHotel` y está excluido de Git. La publicación predeterminada es autocontenida para Windows x64.

## Trabajo reservado para la etapa del instalador

El instalador final deberá:

1. comprobar o instalar MySQL;
2. copiar `Aplicacion` a Archivos de programa;
3. ejecutar la inicialización mediante una interfaz sencilla;
4. limitar los permisos de lectura del archivo `database.json`;
5. ejecutar el registro ya preparado del inicio automático y del respaldo diario;
6. crear los accesos directos;
7. ofrecer reparación y desinstalación sin eliminar datos ni respaldos por defecto.

## Validación final antes de instalar en el hotel

Aunque las funciones se han probado manualmente durante el desarrollo, antes del despliegue definitivo se debe comprobar una instalación limpia completa, un reinicio de Windows, la creación de un respaldo y su restauración en una base de comprobación. Esta validación corresponde al empaquetado, no a una duda sobre las reglas operativas ya probadas.
