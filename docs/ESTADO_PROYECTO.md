# Estado del proyecto HR ProTools

**Fecha:** 18/11/2026
**Fase actual:** Fase 2/5 - primera aplicacion funcional
**Estado:** Completada, corregida despues de la prueba manual y verificada; Fase 3 no iniciada.

## Implementado en Fase 2

- Solucion .NET 10 con Blazor Web App en modo Interactive Server y proyecto xUnit.
- PostgreSQL 18 real con migraciones EF separadas para el catalogo global y cada schema tenant.
- ASP.NET Core Identity, roles globales/tenant, bloqueo de usuarios inactivos y sesion revalidada.
- Administracion global de grupos empresariales, provisioning de schema, primera empresa y administrador tenant.
- Invitaciones de 24 horas mediante token Identity; envio SMTP configurable y enlace manual cuando SMTP no esta configurado.
- Configuracion SMTP protegida con Data Protection; secretos locales fuera del repositorio.
- Organizacion tenant: empresas, departamentos y cargos con altas, edicion y activacion/desactivacion controlada.
- Personas y vinculos laborales con legajo unico asignado automaticamente, asignaciones historicas, historial salarial y cierre de relacion laboral.
- Interfaz responsive basada en la identidad visual proporcionada.
- Inicializacion del repositorio Git `main` y remoto oficial `https://github.com/Orloch314/HRProTools`.

## Verificacion realizada

- `dotnet build`: correcta, 0 advertencias y 0 errores.
- `dotnet test`: 7 pruebas superadas, 0 fallidas, 0 omitidas.
- Prueba de integracion PostgreSQL: crea y migra dos schemas temporales, inserta datos distintos, verifica aislamiento y elimina solamente esos schemas.
- Prueba de integridad laboral: confirma legajo unico y preservacion del historial organizativo y salarial con una sola vigencia abierta.
- Prueba funcional en navegador: login global, formulario interactivo y provisioning completo del grupo sintetico `QA27`.
- Verificacion directa de PostgreSQL: schemas `auth`, `platform` y `hr_t_6e24549785b64c81bbcac3472cbb98c4`; tenant `QA27` activo y empresa inicial `EMPQ2` presente.
- No se envio correo real durante las pruebas.

## Correcciones posteriores a la prueba manual

### Activacion del administrador tenant

- Causa verificada: `AcceptInvitation.razor` es una pagina Account con renderizado estatico y su modelo no tenia `SupplyParameterFromForm`; los valores visibles no llegaban al modelo procesado por el servidor.
- El formulario ahora recibe correctamente contraseña y confirmacion, con validaciones y mensajes en español.
- La aceptacion se procesa en un servicio transaccional: verifica el token, establece la contraseña, confirma el correo y activa la cuenta como una sola operacion.
- Una invitacion aceptada no puede reutilizarse; los tokens invalidos, vencidos o ya utilizados reciben un mensaje unico sin detalles tecnicos.
- Se mantiene `DataProtectionTokenProviderOptions.TokenLifespan = 24 horas`.
- El resultado exitoso muestra una confirmacion y un enlace al login.
- Se agrego un control reutilizable de mostrar/ocultar contraseña a invitacion, login, recuperacion/cambio de contraseña y configuracion SMTP. La prueba de navegador confirmo que alternar la visibilidad no cambia el valor.

### PostgreSQL local identificado

- Entorno corregido despues de la prueba manual: container dedicado `hrprotool-postgres`, base `hrprotool` y usuario aplicativo `hrprotool_app`.
- Cadena efectiva de Development: host `127.0.0.1`, puerto `5435`, base `hrprotool`; la contraseña aleatoria permanece solamente en User Secrets.
- Imagen `postgres:18`, publicacion `127.0.0.1:5435 -> 5432`, reinicio `unless-stopped` y volumen persistente `hrprotool-postgres-data`.
- Version verificada: PostgreSQL `18.6`.
- Schemas encontrados: `auth`, `platform`, `hr_t_6e24549785b64c81bbcac3472cbb98c4` y `hr_t_fadda61e560e4314a0ce2ef5bb412fb1`.
- La prueba manual esta en el tenant `A` / `Gruppo Prova`, schema `hr_t_fadda61e560e4314a0ce2ef5bb412fb1`, empresa `AB` / `Prova SA`. El administrador `Roberto` permanece inactivo y no confirmado porque la invitacion anterior no fue completada.
- Los datos se copiaron con `pg_dump`/`pg_restore` sin eliminar la fuente; la aplicacion ya no usa la instancia compartida anterior.
- Inspeccion directa: `docker exec -it hrprotool-postgres psql -U hrprotool_app -d hrprotool`.

### Revision SMTP y correo no deseado

- HR ProTools e iMatch usan MailKit 4.17, remitente configurable, autenticacion SMTP y el mismo esquema StartTLS/SSL segun configuracion.
- Configuracion efectiva no secreta de HR ProTools: `smtps.aruba.it:587`, StartTLS, nombre `HR ProTools`, dominio remitente `imatchpy.com`.
- Diferencia de codigo corregida: HR ProTools enviaba solo una parte HTML minima. Ahora genera alternativa texto plano + HTML, agrega `Reply-To`, asunto descriptivo y contenido de invitacion completo.
- Se agrego `Application:PublicBaseUrl` opcional para construir enlaces con el dominio publico HTTPS cuando exista; si queda vacio se conserva el origen local actual.
- No se recibieron encabezados completos del mensaje, por lo que no existe evidencia para atribuir el SPAM a SPF, DKIM o DMARC. La clasificacion debe volver a probarse manualmente.

### Codigos automaticos y nomina basica

- Las empresas nuevas reciben un codigo correlativo del sistema (`EMP-0001`, `EMP-0002`, etc.); el codigo ya no se solicita al crear el grupo ni desde Organizacion.
- Los departamentos nuevos reciben un codigo correlativo por empresa (`DEP-0001`, `DEP-0002`, etc.); el codigo asignado se conserva al editar.
- Las altas manuales de funcionarios reciben un legajo numerico igual al mayor legajo numerico existente mas uno.
- La ruta de servicio para futuras importaciones conserva el legajo informado y valida duplicados antes de insertar; el indice unico de PostgreSQL mantiene una segunda garantia de integridad.
- La asignacion de codigos y legajos usa bloqueos transaccionales por schema tenant para impedir duplicados entre operaciones simultaneas.
- Se corrigio la consulta de nomina que ordenaba una proyeccion no traducible por Entity Framework y producia `InvalidOperationException`.
- El formulario de alta carga las empresas activas del tenant, selecciona una por defecto y ajusta departamentos/cargos al cambiar de empresa.
- Verificacion de datos: `Prova2` y `Prova SA` estan activas en `hr_t_fadda61e560e4314a0ce2ef5bb412fb1`.

### Pruebas especificas ejecutadas

- Activacion Identity con contraseña valida y persistencia de cuenta activa/confirmada.
- Rechazo de contraseña y confirmacion diferentes en español.
- Rechazo de token invalido sin activar la cuenta.
- Rechazo del intento de reutilizacion del token.
- POST HTTP real del formulario estatico con binding correcto y mensajes en español.
- Mostrar/ocultar contraseña en navegador sin alterar el contenido.
- Asignacion correlativa posterior a codigos y legajos existentes, y ejecucion real de la consulta de nomina sobre PostgreSQL.

## Configuracion local

- La cadena PostgreSQL y las credenciales bootstrap estan en User Secrets del proyecto Web; no se versionan.
- Base local: `hrprotool`, alojada en su propio container y volumen PostgreSQL 18.
- Para ejecutar: `dotnet run --project src/HRProTools.Web`.
- Para habilitar correo real, un administrador global debe completar `/admin/smtp` con un servidor SMTP de prueba valido.
- El tenant sintetico `QA27` y su usuario invitado permanecen en la base local como evidencia reproducible de la prueba funcional.

## Pendientes y bloqueos reales

1. Reiniciar la aplicacion y volver a abrir la invitacion recibida; si ya vencio, crear una nueva invitacion controlada.
2. Repetir manualmente el envio/recepcion y aportar los encabezados completos si el correo vuelve a SPAM.
3. Configurar `Application:PublicBaseUrl` con el dominio HTTPS real antes de enviar invitaciones desde un ambiente publicado.
4. Sustituir las credenciales bootstrap locales antes de cualquier ambiente compartido o publicado.
5. Antes de Fase 3 debe aprobarse la especificacion exacta del calculo legal paraguayo y el formato de importacion Excel.

## Proxima actividad autorizada

Ninguna implementacion de Fase 3 fue iniciada. El siguiente trabajo requiere autorizacion explicita y las definiciones legales indicadas arriba.
