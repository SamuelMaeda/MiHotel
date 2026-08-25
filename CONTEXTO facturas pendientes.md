# CONTEXTO facturas pendientes

## Objetivo

La facturación se realizará manualmente fuera del sistema, mediante la Agencia Virtual o las herramientas autorizadas de la SAT. El sistema del hotel deberá controlar qué reservaciones necesitan factura y permitir asociar posteriormente los documentos emitidos.

La facturación no deberá bloquear la salida del huésped ni mantener ocupada una habitación después de que la estadía terminó.

## Decisión durante el checkout

En la pantalla de checkout, antes de registrar la salida, el recepcionista deberá responder obligatoriamente:

```text
¿El huésped solicitó factura para esta estadía?

( ) Sí, requiere factura
( ) No requiere factura
```

Ninguna opción estará seleccionada inicialmente. Se utilizarán opciones explícitas en lugar de una casilla simple para evitar que una omisión se interprete accidentalmente como una respuesta negativa.

La existencia de NIT, nombre o dirección fiscal en el perfil del cliente no determinará automáticamente la respuesta. Un huésped puede tener información fiscal registrada y no solicitar factura para una estadía específica.

## Flujo sin factura solicitada

Cuando el recepcionista seleccione que el huésped no requiere factura:

- Se registrará la salida.
- La habitación será liberada inmediatamente.
- Si no existe saldo pendiente, la reservación podrá finalizar normalmente.
- La reservación no aparecerá en la bandeja de facturas pendientes.
- La decisión quedará registrada para auditoría.

## Flujo con factura solicitada

Cuando el recepcionista seleccione que el huésped requiere factura:

- Se registrará la salida.
- La habitación será liberada inmediatamente.
- La reservación pasará al estado o condición `pendiente_de_factura`.
- Aparecerá automáticamente en el apartado **Facturas pendientes**.
- El administrador emitirá el DTE manualmente fuera del sistema.
- Posteriormente registrará y adjuntará el documento a la reservación.
- Al completar la facturación, la reservación podrá cerrar su proceso administrativo.

El botón de checkout podrá adaptar su texto según la selección:

- **Registrar salida y enviar a facturación**, cuando se requiera factura.
- **Finalizar estadía**, cuando no se requiera factura y la cuenta esté pagada.

## Naturaleza de la bandeja de pendientes

El apartado **Facturas pendientes** será una bandeja de trabajo para administración, pero no será el único lugar desde el cual se puedan registrar documentos fiscales.

Todas las reservaciones deberán permitir asociar documentos fiscales desde su detalle, aunque inicialmente no hayan aparecido en esa bandeja. También deberá ser posible hacerlo desde el detalle de una cuenta por cobrar o desde el historial del cliente.

Esto permitirá atender casos en los que el huésped cambie de decisión después de la salida.

## Caso de pago posterior a la estadía

Cuando un huésped frecuente no pueda pagar al momento de salir, la dueña o un usuario autorizado podrá aprobar la salida con deuda. En ese caso:

```text
Checkout con deuda autorizada
→ Se registra la salida
→ Se libera la habitación
→ La deuda permanece en Cuentas por cobrar
→ El huésped paga posteriormente
→ Se registra el pago
→ Se confirma si requiere factura
→ Si la requiere, pasa a Facturas pendientes
→ Administración emite y adjunta el DTE
→ Se cierra el expediente
```

Al registrar el pago final de una cuenta por cobrar, el sistema deberá permitir indicar que el huésped ahora solicita factura. La reservación ingresará automáticamente a la bandeja correspondiente, aunque durante el checkout se hubiera indicado inicialmente que no la requería.

## Estados independientes

La disponibilidad de la habitación, el pago y la facturación representan situaciones diferentes y no deben depender de un único estado general.

Se deberán distinguir al menos las siguientes dimensiones:

- **Estado de estadía:** pendiente, en curso, en checkout, salida registrada o finalizada.
- **Estado de cuenta:** pendiente, pago parcial o pagada.
- **Estado de facturación:** no solicitada, pendiente, emitida o anulada.
- **Estado administrativo:** pendiente de revisión o cerrado.

Por ejemplo, una reservación puede tener la salida registrada, la habitación libre, una cuenta pendiente y ninguna factura solicitada. Posteriormente puede pasar a cuenta pagada y factura pendiente sin alterar la disponibilidad de la habitación.

## Registro de documentos fiscales

No se almacenará una única factura directamente en la reservación. Cada reservación podrá tener varios documentos fiscales asociados para contemplar facturas, anulaciones, sustituciones, notas de crédito o notas de débito.

De cada documento se deberá conservar, como mínimo:

- Tipo de documento.
- Número de autorización.
- Serie y número.
- Fecha de emisión.
- Monto.
- Estado del documento: vigente, anulado o sustituido.
- Archivo PDF.
- Archivo XML, cuando esté disponible.
- Usuario que realizó el registro.
- Fecha y hora del registro.

## Auditoría y prevención de abuso

Seleccionar que una estadía no requiere factura no deberá ser una acción invisible. El sistema conservará:

- Usuario que tomó la decisión.
- Fecha y hora.
- Reservación y monto relacionados.
- Cambios posteriores en la decisión.
- Usuario que agregó, anuló o sustituyó un documento.

Administración deberá poder consultar reportes de estadías con y sin factura por fecha, recepcionista y monto.

La factura no será el único control contra el robo. La seguridad financiera dependerá principalmente de que los pagos no puedan eliminarse silenciosamente, de que cada cobro se asocie al usuario y al turno correspondiente, y de que exista un cierre de caja comparado contra el efectivo, transferencias y otros medios recibidos.

## Consideración fiscal pendiente

Antes de implementar las reglas definitivas deberá consultarse al contador del hotel para establecer cuándo corresponde emitir el DTE y cuál debe ser su fecha. Que un huésped no solicite una copia, que posea información fiscal o que pague después de la estadía son hechos diferentes de la obligación tributaria de emitir el documento.

El sistema deberá aplicar la política fiscal confirmada por el contador y no asumir automáticamente que la ausencia de una solicitud elimina la obligación de facturar.
