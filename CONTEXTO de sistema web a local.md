# CONTEXTO de sistema web a local

## Cambio general

El sistema de gestión del hotel ya no será publicado ni utilizado como una plataforma accesible desde internet. Su funcionamiento estará limitado al entorno local del hotel y será utilizado únicamente por el personal autorizado.

Este cambio elimina la necesidad de mantener los procesos que fueron diseñados exclusivamente para clientes externos o para el alojamiento público del sistema.

## Forma de funcionamiento esperada

El sistema podrá conservar su tecnología actual ASP.NET MVC y ejecutarse localmente. Que la interfaz se abra en un navegador no significa que el sistema continúe siendo público: puede funcionar mediante `localhost` en una sola computadora o dentro de una red interna privada del hotel.

Antes de implementar el cambio deberá definirse cuál de estas modalidades se utilizará:

- Una sola computadora para todo el sistema.
- Una computadora principal que funcione como servidor local y permita el acceso desde otros equipos del hotel mediante la red interna.

La segunda modalidad permitiría que recepción y administración trabajen simultáneamente sin publicar el sistema en internet.

## Procesos en línea que dejarán de utilizarse

Deberán eliminarse o deshabilitarse los procesos creados exclusivamente para el funcionamiento público, entre ellos:

- Registro de cuentas por parte de huéspedes.
- Inicio de sesión para clientes.
- Consulta de reservaciones por parte del huésped.
- Creación y cancelación de reservaciones desde internet.
- Recuperación de contraseñas mediante correo electrónico y enlaces externos.
- Configuración de correo SMTP que ya no sea necesaria.
- Alojamiento público, dominio y exposición del sistema a internet.
- Integraciones destinadas a emitir o certificar facturas automáticamente.
- Credenciales de certificadores que dejen de utilizarse.

Los clientes continuarán existiendo como registros administrativos, pero serán creados y gestionados por el personal del hotel.

## Facturación

El sistema no emitirá ni certificará facturas automáticamente. La emisión de Documentos Tributarios Electrónicos se realizará manualmente en la Agencia Virtual o en las herramientas autorizadas de la SAT.

El sistema local únicamente conservará el control administrativo de la facturación y permitirá registrar o adjuntar los documentos emitidos externamente.

## Seguridad y continuidad

Trabajar localmente no elimina la necesidad de autenticación, roles y auditoría. Cada operación sensible deberá seguir asociada al usuario que la realizó.

También será necesario establecer:

- Respaldos automáticos de la base de datos.
- Copias almacenadas en un dispositivo diferente al equipo principal.
- Respaldo de las facturas y demás archivos adjuntos.
- Un procedimiento de recuperación ante daño, pérdida o robo del equipo.
- Restricción de acceso desde dispositivos ajenos a la red del hotel.
- Protección de las cuentas administrativas y separación de permisos entre recepción y administración.

## Decisión arquitectónica

El cambio a local no implica necesariamente convertir el proyecto en una aplicación de escritorio ni reescribir toda su interfaz. La prioridad será retirar los procesos públicos y configurar una instalación local segura, reutilizando la aplicación existente cuando resulte conveniente.

La modalidad exacta —una sola computadora o varios equipos conectados a un servidor local— deberá decidirse antes de modificar la arquitectura y el despliegue.
