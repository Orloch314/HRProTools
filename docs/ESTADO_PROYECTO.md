# Estado del proyecto HR ProTools

**Fecha:** 19/11/2026
**Fase actual:** Fase 3/5 - usuarios, vacaciones e importacion Excel
**Estado:** Implementada y verificada; Fase 4 no iniciada.

## Implementado en Fase 3

### Usuarios y autoservicio

- `ApplicationUser` se vincula con `PersonId` y `ActiveEmploymentId` sin mezclar persona, legajo y cuenta Identity. El indice por tenant/persona impide cuentas duplicadas para una misma persona.
- En `Funcionarios` la columna `Acceso` muestra `Sin usuario`, `Invitacion pendiente`, `Activo` o `Inactivo`.
- La ficha incluye `Acceso al sistema`: enviar o reenviar invitacion, enlace manual cuando SMTP no esta disponible, desactivar, reactivar, asociar una cuenta existente del mismo tenant y asignar roles multiples.
- El reenvio cambia el security stamp e invalida el enlace anterior. Los errores SMTP conservan tanto el funcionario como la cuenta preparada.
- El alta manual ofrece `Enviar invitacion de acceso`, desmarcado por defecto. La importacion nunca envia invitaciones.
- El rol `Employee` dispone de `/empleado`, donde solo consulta sus datos, empresa, departamento, cargo, legajo y saldo propio; el cambio de contraseña reutiliza `/Account/Manage/ChangePassword`.
- Las operaciones administrativas de acceso vuelven a comprobar `TenantAdministration` dentro del servicio, ademas de la proteccion de la pagina.

### Jornadas y feriados

- `/tenant/jornadas` permite crear jornadas con dias de lunes a domingo, hora inicial/final y descanso opcional, y registrar feriados por empresa.
- Cada alta recibe una jornada. Los vinculos existentes sin jornada reciben `Jornada estandar` al iniciar con migraciones habilitadas.
- Los cambios de jornada crean una nueva vigencia y cierran la anterior; no se sobrescribe el historial.
- Jornadas, feriados y reglas legales de vacaciones permanecen conceptualmente separados. No se implementaron marcaciones, horas extras ni asistencia.

### Politicas, periodos y saldos de vacaciones

- `/tenant/vacaciones/politicas` crea versiones por empresa y fecha de vigencia. Solo se editan dias adicionales; los minimos legales 12/18/30 no son editables.
- La causacion usa la fecha de ingreso y aniversarios, no el ano calendario. Una politica nueva solo afecta periodos causados desde su vigencia.
- La primera ejecucion para un funcionario antiguo genera solamente el ultimo periodo causado, evitando inventar saldos historicos desconocidos. Los periodos anteriores se incorporan por Excel.
- La generacion es idempotente y se ejecuta al iniciar y cada 24 horas con fechas civiles de `America/Asuncion`; tambien existe una accion manual controlada.
- La ficha del funcionario incluye `Vacaciones`: antiguedad, proxima causacion, legales/adicionales, periodos, limite de seis meses, reservados, consumidos, disponibles y pendientes.
- Un periodo es pendiente solamente despues de su limite de seis meses y mientras conserve dias legales sin consumir; las reservas futuras no ocultan esa condicion.
- El ledger inmutable admite `CREDIT`, `RESERVE`, `RELEASE`, `CONSUME`, `REVERSAL` y `ADJUSTMENT`. Las correcciones agregan movimientos.
- El servicio FIFO reserva desde el periodo mas antiguo, convierte reservas en consumo, libera y revierte en el periodo original. Usa transacciones serializables, bloqueo del ledger y claves idempotentes para impedir duplicados y saldos negativos.

### Importacion Excel

- `/tenant/importacion` descarga una plantilla `.xlsx`, carga hasta 10 MB, escanea con ClamAV, valida las dos hojas y muestra errores por fila antes de confirmar.
- `Funcionarios` conserva legajo importado y exige empresa, departamento, cargo y jornada existentes. Reutiliza la persona por cedula cuando corresponde y no actualiza funcionarios existentes.
- `Vacaciones` acepta multiples periodos por legajo, conserva fechas y dias originales y registra usos previos mediante movimientos de consumo de importacion.
- Si existe un error no se persiste ninguna fila. La confirmacion usa una transaccion unica y guarda nombre, SHA-256, estado antivirus y resultado del lote.
- Se incorporo ClosedXML 0.105.0; no se agregaron servicios externos de almacenamiento.

### Regla legal y limitaciones

- Ley 213/93, arts. 218-226: derecho despues de cada ano continuo, tramos 12/18/30, otorgamiento dentro de seis meses y registro de goce. Fuente: https://www.bacn.gov.py/leyes-paraguayas/2608/ley
- La comunicacion MTESS sobre Resolucion 1290/2024 confirma aniversario, tramos, seis meses, inicio lunes/habil, preaviso y fraccionamiento: https://www.mtess.gov.py/?p=30145
- La ley publicada usa `dias corridos` y la comunicacion MTESS usa `dias habiles`. En Fase 3 se conserva la cantidad reconocida y se separan jornada/feriados; el computo de fechas de una solicitud se implementara en Fase 4 con la regla funcional definitiva.
- No se implementaron trabajadores zafrales, liquidacion/indemnizacion, solicitudes, aprobaciones, notificaciones de alerta ni alcances por empresa/departamento.

### Rutas de navegacion Fase 3

- Nomina y acceso: `/tenant/funcionarios` -> `Ficha` -> `Acceso al sistema`.
- Vacaciones individuales: `/tenant/funcionarios/{employmentId}` -> `Vacaciones`.
- Autoservicio: `/empleado`.
- Jornadas y feriados: `/tenant/jornadas`.
- Politicas: `/tenant/vacaciones/politicas`.
- Importacion: `/tenant/importacion`.

### Verificacion Fase 3

- `dotnet build --configuration Release`: 0 advertencias, 0 errores.
- `dotnet test`: 19 pruebas superadas, 0 fallidas, 0 omitidas.
- Limites legales verificados en 1, 5, 6, 10 y 11 anos.
- Politica no retroactiva, causacion idempotente y criterio de periodo pendiente verificados sobre PostgreSQL.
- FIFO, consumo, liberacion, reversion y dos reservas concurrentes sin saldo negativo verificados.
- Plantilla Excel abierta de nuevo con ClosedXML; importacion valida con dos periodos, consumo historico y rechazo posterior del legajo duplicado verificados.
- Migraciones global y tenant aplicadas al PostgreSQL local; ambos modelos EF no tienen cambios pendientes.
- Arranque runtime verificado en puerto temporal y `/health` respondio `Healthy` con HTTP 200.

## Implementado en Fase 2

- Solucion .NET 10 con Blazor Web App en modo Interactive Server y proyecto xUnit.
- PostgreSQL 18 real con migraciones EF separadas para el catalogo global y cada schema tenant.
- ASP.NET Core Identity, roles globales/tenant, bloqueo de usuarios inactivos y sesion revalidada.
- Administracion global de grupos empresariales, provisioning de schema, primera empresa y administrador tenant.
- Invitaciones de 24 horas mediante token Identity; envio SMTP configurable y enlace manual cuando SMTP no esta configurado.
- Configuracion SMTP protegida con Data Protection; secretos locales fuera del repositorio.
- Organizacion tenant: empresas, departamentos y cargos con altas, edicion y activacion/desactivacion controlada.
- Personas y vinculos laborales con legajo unico asignado automaticamente, asignaciones historicas, historial salarial y cierre de relacion laboral.
- Ficha individual del funcionario con datos generales, historial laboral e historial salarial, respetando autorizaciones y aislamiento tenant.
- Identidad visual por grupo empresarial con logo propio persistido en el schema tenant y mostrado en el encabezado compartido.
- Interfaz responsive basada en la identidad visual proporcionada.
- Inicializacion del repositorio Git `main` y remoto oficial `https://github.com/Orloch314/HRProTools`.

## Verificacion realizada

- `dotnet build`: correcta, 0 advertencias y 0 errores.
- `dotnet test`: 10 pruebas superadas, 0 fallidas, 0 omitidas.
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
- Los cargos nuevos reciben un codigo correlativo por empresa (`CAR-0001`, `CAR-0002`, etc.); el formulario ya no solicita el codigo y `Nivel` es opcional tanto en la interfaz como en PostgreSQL.
- Las altas manuales de funcionarios reciben un legajo numerico igual al mayor legajo numerico existente mas uno.
- La ruta de servicio para futuras importaciones conserva el legajo informado y valida duplicados antes de insertar; el indice unico de PostgreSQL mantiene una segunda garantia de integridad.
- La asignacion de codigos y legajos usa bloqueos transaccionales por schema tenant para impedir duplicados entre operaciones simultaneas.
- Al iniciar con `Database:ApplyMigrations`, la aplicacion actualiza tambien todos los schemas tenant activos; la migracion de `Nivel` nullable fue aplicada y verificada en `A` y `QA27`.
- Se corrigio la consulta de nomina que ordenaba una proyeccion no traducible por Entity Framework y producia `InvalidOperationException`.
- El formulario de alta carga las empresas activas del tenant, selecciona una por defecto y ajusta departamentos/cargos al cambiar de empresa.
- Los valores asignados por el sistema no se representan como campos de formulario: se retiraron codigo de empresa, codigo de departamento y legajo de las altas, junto con los campos visibles deshabilitados de las pantallas de cuenta.
- La revision completa de componentes no encontro otros campos visibles `disabled`, `readonly` o de asignacion automatica; permanecen solamente campos ocultos tecnicos requeridos por los POST.
- El login ya no muestra el aviso tecnico de proveedores externos ni la opcion Passkey no adoptada por el MVP; conserva solamente correo, contraseña, sesion persistente y recuperacion de contraseña, con textos unificados en español.
- Verificacion de datos: `Prova2` y `Prova SA` estan activas en `hr_t_fadda61e560e4314a0ce2ef5bb412fb1`.

### Pruebas especificas ejecutadas

- Activacion Identity con contraseña valida y persistencia de cuenta activa/confirmada.
- Rechazo de contraseña y confirmacion diferentes en español.
- Rechazo de token invalido sin activar la cuenta.
- Rechazo del intento de reutilizacion del token.
- POST HTTP real del formulario estatico con binding correcto y mensajes en español.
- Mostrar/ocultar contraseña en navegador sin alterar el contenido.
- Asignacion correlativa posterior a codigos y legajos existentes, y ejecucion real de la consulta de nomina sobre PostgreSQL.

### Ficha e historiales del funcionario

- Desde `Funcionarios`, la accion `Ficha` abre `/tenant/funcionarios/{employmentId}`.
- La nomina abre con el formulario de alta cerrado; el usuario lo despliega expresamente con `Nuevo funcionario` y puede volver a cerrarlo desde el mismo boton.
- La ficha se organiza en `Datos generales`, `Historial laboral` e `Historial salarial`; los registros aparecen del mas reciente al mas antiguo y la vigencia actual queda destacada.
- El historial laboral muestra legajo, empresa, departamento, cargo, fechas, estado y motivo, e incluye vinculos laborales anteriores de la misma persona dentro del tenant cuando existen.
- El historial salarial reutiliza las vigencias existentes y muestra salario bruto, moneda, fechas y estado. Para el MVP la moneda presentada es PYG, coherente con el modelo salarial actual.
- La lectura salarial usa una autorizacion separada de la ficha laboral; el rol de autorizador de solicitudes no recibe acceso salarial automatico.
- La prueba PostgreSQL confirma que los cambios reales de departamento, cargo y salario cierran la vigencia anterior y crean una nueva sin sobrescritura ni superposiciones. Renombrar un departamento o cargo no genera una nueva asignacion historica.

### Identidad visual del grupo empresarial

- El administrador tenant configura el logo desde `Identidad visual`, ruta `/tenant/identidad-visual`.
- La pantalla permite seleccionar PNG, JPEG o WebP, previsualizar, guardar o sustituir el logo y eliminarlo. El limite es 2 MB y el backend valida la firma real del contenido, no solamente el nombre o tipo declarado.
- El logo se guarda como `bytea` con tipo MIME, nombre y metadatos de escaneo en `tenant_branding`, dentro del schema del tenant. La migracion se aplico a los tenants existentes `A` y `QA27` sin modificar sus datos funcionales.
- El encabezado tenant muestra logo, separador discreto, nombre HR ProTools y subtitulo sin deformar la imagen; conserva el encabezado anterior, sin huecos ni imagen generica, cuando no existe logo. La administracion global mantiene la identidad propia de HR ProTools.
- La consulta y modificacion derivan siempre el tenant de la identidad autenticada; no se acepta un tenant por parametro de ruta. Solamente `TenantAdministrator` puede guardar o eliminar el logo.
- Las cargas pasan por ClamAV mediante el protocolo `INSTREAM`. En Development puede quedar explicitamente como no escaneado si no hay daemon configurado; en Production la carga falla de forma segura si ClamAV no esta configurado o no esta disponible.
- Para produccion debe configurarse al menos `Antivirus__ClamAv__Host` (por ejemplo `127.0.0.1`); el puerto predeterminado es `3310`. No se incorporaron servicios externos ni dependencias de almacenamiento.
- Las pruebas verifican firmas validas e invalidas, limite de tamaño, exigencia de antivirus en Production, persistencia tras recrear el contexto, sustitucion, eliminacion y aislamiento entre dos schemas tenant.
- Se corrigio la inicializacion concurrente del encabezado y la pagina Blazor: la resolucion del tenant usa ahora un contexto global independiente por operacion, evitando compartir el `DbContext` de Identity durante el renderizado paralelo.
- Una prueba de regresion ejecuta veinte resoluciones simultaneas del tenant y confirma que todas finalizan sobre contextos independientes.

## Configuracion local

- La cadena PostgreSQL y las credenciales bootstrap estan en User Secrets del proyecto Web; no se versionan.
- Base local: `hrprotool`, alojada en su propio container y volumen PostgreSQL 18.
- Para ejecutar: `dotnet run --project src/HRProTools.Web`.
- Para habilitar correo real, un administrador global debe completar `/admin/smtp` con un servidor SMTP de prueba valido.
- Para habilitar cargas de logo en produccion, ClamAV debe estar activo y accesible mediante `Antivirus__ClamAv__Host` y, si corresponde, `Antivirus__ClamAv__Port`.
- El tenant sintetico `QA27` y su usuario invitado permanecen en la base local como evidencia reproducible de la prueba funcional.

## Pendientes y bloqueos reales

1. Reiniciar la aplicacion y volver a abrir la invitacion recibida; si ya vencio, crear una nueva invitacion controlada.
2. Repetir manualmente el envio/recepcion y aportar los encabezados completos si el correo vuelve a SPAM.
3. Configurar `Application:PublicBaseUrl` con el dominio HTTPS real antes de enviar invitaciones desde un ambiente publicado.
4. Sustituir las credenciales bootstrap locales antes de cualquier ambiente compartido o publicado.
5. Antes de Fase 4 debe aprobarse la regla definitiva para convertir cantidades de vacaciones en fechas de solicitud ante la diferencia `dias corridos`/`dias habiles` documentada.

## Proxima actividad autorizada

Ninguna implementacion de Fase 4 fue iniciada. El siguiente trabajo requiere autorizacion explicita.
