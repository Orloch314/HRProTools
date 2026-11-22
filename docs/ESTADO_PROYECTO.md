# Estado del proyecto HR ProTools

**Fecha:** 22/11/2026
**Fase actual:** Fase 5/5 - cierre funcional del MVP
**Estado:** MVP funcional implementado y verificado automaticamente; pendiente de aceptacion manual y publicacion futura.

## Implementado en Fase 5

### Ausencias y justificaciones

- `/empleado/ausencias` permite al funcionario declarar una ausencia propia medica, injustificada u otra justificada. La declaracion queda pendiente y nunca se considera justificada definitivamente sin revision HR.
- `/tenant/ausencias` permite al autorizador registrar ocurrencias solamente para sus departamentos y a Recursos Humanos hacerlo dentro de sus empresas autorizadas.
- La revision y reclasificacion final son exclusivas de Recursos Humanos. Declaracion, cambios de estado, clasificacion, usuario, fecha y comentario se conservan en un historial funcional; una correccion cancela logicamente y no elimina registros.
- El computo reutiliza jornadas y feriados existentes y guarda una instantanea por fecha. Se rechazan ausencias duplicadas; los conflictos con vacaciones o permisos se muestran y solamente un usuario operativo puede confirmarlos, sin modificar la solicitud preexistente.

### Documentos justificativos seguros

- Recursos Humanos puede cargar PDF, JPEG o PNG de hasta 5 MB desde el detalle de la ausencia. Se validan extension, MIME, firma binaria y tamano antes de escanear con ClamAV.
- El archivo se guarda en `bytea` dentro del schema tenant con nombre, MIME, tamano, SHA-256, usuario, fecha y estado antivirus. No existe ruta publica ni se registra contenido medico en logs.
- La descarga vuelve a comprobar rol HR, cuenta, tenant y alcance empresarial. Solamente se entregan archivos con estado `Clean`; en Development un archivo no escaneado queda identificado y con descarga bloqueada. TenantAdministrator y Approver no reciben acceso por sus roles.

### Alertas operativas y dashboard

- Un proceso diario genera alertas idempotentes por tenant y permite marcarlas como atendidas. Incluye ausencias pendientes, injustificadas, documentacion medica pendiente y riesgo de segundo periodo legal pendiente.
- La alerta preventiva de vacaciones requiere un periodo ya pendiente con saldo legal y otro periodo posterior cuyo limite de seis meses se alcanza dentro de 60 dias. El calculo usa causacion, `UseBy` y consumo legal; las reservas no ocultan la situacion.
- `/dashboard` muestra datos reales segun rol: administracion del grupo sin salario ni datos medicos; RR. HH. por alcance empresarial; autorizadores por departamentos; y funcionario solamente con su informacion propia.
- Las solicitudes pendientes se obtienen del nivel actualmente habilitado y de los actores copiados al circuito; no se generan avisos para etapas futuras.

### Reporte Excel de permisos

- `/tenant/reportes/permisos` esta disponible exclusivamente para Recursos Humanos y limita empresas y departamentos a su alcance configurado.
- Exporta un unico `.xlsx` con filtros por empresa, departamento, fechas y estado, e incluye empresa, departamento, legajo, nombre, tipo, goce, unidad, fechas, cantidad, fecha de solicitud, estado y resolucion final.
- ClosedXML genera un solo hoja operativa con tabla filtrable, encabezados, fechas nativas, formatos, anchos legibles y panel congelado. No contiene salarios, ausencias ni documentos medicos.

### Persistencia, migracion y verificacion

- La migracion tenant `Phase5AbsencesAlerts` agrega `absence`, detalle diario, historial, documentos y alertas de forma aditiva. No recrea schemas ni altera historiales, saldos, solicitudes o datos manuales existentes.
- La migration se aplico mediante el arranque local a todos los tenants activos; se verificaron expresamente las nuevas tablas en `Gruppo Prova` (`A`) y `Grupo QA Funcional` (`QA27`).
- `dotnet build --configuration Release`: 0 advertencias y 0 errores.
- `dotnet test`: 47 pruebas superadas, 0 fallidas y 0 omitidas. Las cinco pruebas nuevas cubren autorizacion y trazabilidad de ausencias, documentos, alertas sin duplicados, dashboard multirol y reapertura del Excel con filtros.
- Ambos contextos EF quedan sin cambios de modelo pendientes y `/health` respondio `Healthy` en una instancia Release temporal.

### Cierre Git de la Fase 5

- Rama de cierre: `codex/phase5-mvp-completion`.
- Remoto oficial: `origin` -> `https://github.com/Orloch314/HRProTools`.
- Commit funcional final de la Fase 5: `a248a44500f0dfc75f1ac133b140f1213a88e6d0` (`feat: complete phase 5 absences alerts dashboards and reports`).
- Publicacion verificada: `refs/heads/codex/phase5-mvp-completion` en GitHub apunta al mismo commit `a248a44500f0dfc75f1ac133b140f1213a88e6d0`.
- Estado Git comprobado antes de incorporar este cierre documental: arbol de trabajo limpio, rama local enlazada con `origin/codex/phase5-mvp-completion` y diferencia local/remota `0/0`.
- La actualizacion documental de este apartado se incorpora en un commit posterior, sin cambios funcionales; su hash y el resultado real del push se consignan en el informe final de la tarea.

### Prueba manual recomendada

- Funcionario: `/empleado/ausencias` -> declarar -> consultar estado.
- Autorizador o HR: `/tenant/ausencias` -> registrar dentro del alcance -> abrir detalle.
- Recursos Humanos: resolver clasificacion, cargar un archivo de prueba no sensible y comprobar su estado antivirus/descarga.
- Todos los roles tenant: `/dashboard`; Recursos Humanos: `/tenant/reportes/permisos` -> aplicar filtros -> descargar y abrir Excel.
- Confirmar que un autorizador no ve documentos, un administrador tenant no ve datos medicos y un empleado no ve registros ajenos.

## Implementado en Fase 4

### Solicitudes de permisos

- `/empleado/permisos` permite solicitar permisos con o sin goce de salario, sin ejecutar calculos de nomina ni deducciones.
- Los permisos por dias computan solamente dias contractuales de trabajo y excluyen descansos y feriados de la empresa. Los permisos por horas validan el intervalo dentro de la jornada y descuentan la interseccion con el descanso configurado.
- La duracion horaria se conserva en minutos enteros y nunca se convierte automaticamente en un dia. Se rechazan intervalos invertidos, fuera de la jornada o compuestos solamente por el descanso.
- Cada solicitud conserva empresa, departamento, tipo, unidad, motivo y una instantanea diaria de jornada, horarios, descanso y feriado. Cambios posteriores no alteran la duracion historica.
- Se rechazan en backend superposiciones entre permisos pendientes/aprobados y entre permisos y vacaciones pendientes/aprobadas. Las solicitudes rechazadas o canceladas no bloquean intervalos nuevos.
- Los permisos reutilizan la configuracion y los alcances del circuito de vacaciones, pero sus aprobaciones no reservan, consumen ni modifican el ledger de vacaciones.
- El funcionario consulta estado, nivel pendiente e historial completo; puede cancelar solicitudes pendientes o aprobadas cuando la configuracion tenant lo permite. Las decisiones anteriores permanecen intactas.

### Circuito compartido y bandeja operativa

- `/tenant/solicitudes` ofrece pestañas de vacaciones y permisos, separa pendientes de solicitudes procesadas y muestra el circuito copiado al presentar.
- Jefe/Autorizador y Recursos Humanos mantienen los mismos controles de rol, departamento, empresa, nivel actual, tenant, cuenta activa y prohibicion de autoaprobacion.
- `TenantAdministrator` configura el circuito en `/tenant/vacaciones/circuito`, pero no recibe capacidad operativa ni datos personales por ese rol.
- Los reintentos y dobles clics no duplican decisiones. Un rechazo en cualquier nivel finaliza el circuito; una aprobacion intermedia avanza solamente al nivel siguiente.

### Notificaciones persistentes

- Presentacion, avance de nivel, aprobacion final, rechazo y cancelacion generan avisos en español para vacaciones y permisos.
- Los autorizadores reciben el enlace a `/tenant/solicitudes`; el funcionario recibe el enlace a su propio historial. Solamente se agrega enlace cuando `Application:PublicBaseUrl` contiene una URL HTTPS valida.
- Los eventos se guardan en `notification_outbox` dentro del schema tenant y usan una clave unica por transicion y destinatario. La transaccion de la solicitud termina antes del envio SMTP.
- Un worker intenta el envio con MailKit, registra intentos, exito o error, aplica espera progresiva y marca fallida una notificacion despues de diez intentos. El administrador puede preparar nuevamente las fallidas desde la pantalla del circuito.
- Si SMTP no esta configurado, las solicitudes siguen funcionando, los avisos permanecen pendientes y la configuracion tenant muestra expresamente la situacion.

### Jornada contractual obligatoria

- El alta individual exige seleccionar expresamente una jornada laboral activa. No se asigna una jornada estandar ni la primera jornada disponible de forma silenciosa.
- La importacion Excel exige la columna `Jornada laboral` con una jornada activa existente; una fila sin jornada se rechaza antes de confirmar el lote.
- Los cambios de jornada conservan las vigencias historicas. El calculo resuelve la jornada aplicable fecha por fecha y falla de forma segura si existe una fecha sin jornada o con vigencias superpuestas.

### Computo y evidencia de la solicitud

- `/empleado/vacaciones` permite indicar un intervalo inclusivo y muestra, antes de confirmar, cada fecha, la jornada vigente, el descanso semanal, el feriado aplicable y la cantidad exacta de dias que se reservaran.
- Se cuentan solamente los dias contractuales de trabajo. Se excluyen los feriados de la empresa y no se descuentan los dias de descanso semanal. Una jornada contractual que incluye domingo computa ese domingo salvo que sea feriado.
- La solicitud persiste una instantanea por fecha con la jornada, el feriado y la decision de computo. Cambios posteriores del calendario o de la jornada no alteran la solicitud registrada.
- La regla funcional fue aprobada por producto. Queda **pendiente de validacion juridica** confirmar su correspondencia con la expresion legal `dias habiles corridos`; el sistema no presenta esta interpretacion como dictamen legal.

### Reserva, aprobacion y cancelacion FIFO

- La creacion reserva siempre el saldo del periodo mas antiguo, incluso si esta parcialmente consumido, y continua por los periodos siguientes cuando el primero no alcanza.
- Se guardan las asignaciones exactas por periodo. Aprobar convierte esas mismas reservas en consumo; rechazar libera las reservas originales; cancelar libera una solicitud pendiente o revierte exactamente los consumos originales de una aprobada.
- Las transiciones son transaccionales e idempotentes. Repetir una operacion no duplica movimientos y el ledger no permite saldos negativos.
- La presentacion crea la reserva FIFO y una copia inmutable del circuito vigente. La aprobacion de Jefe mantiene la reserva; solamente la aprobacion final de Recursos Humanos la convierte en consumo.
- Un rechazo de Jefe o de Recursos Humanos libera exactamente las reservas originales. El funcionario consulta sus propias solicitudes y puede cancelarlas solamente cuando la configuracion tenant lo permite.
- Se reutilizaron `WorkCalendarService`, los historiales de jornada, los feriados y `VacationService`; no se creo un segundo motor de calendario ni de FIFO.

### Circuito de aprobacion corregido

- El circuito predeterminado y obligatorio es `Funcionario -> Jefe / Autorizador -> Recursos Humanos`. Puede contener niveles adicionales de las dos categorias soportadas, siempre manteniendo Jefatura antes de Recursos Humanos.
- `/tenant/vacaciones/circuito`, disponible exclusivamente para `TenantAdministrator`, configura niveles y orden, autorizadores por departamento, alcance HR de grupo o empresas especificas, cancelacion por el funcionario y estado del outbox.
- Un autorizador puede cubrir varios departamentos y un departamento puede tener varios autorizadores. La decision valida de uno es suficiente; el bloqueo transaccional impide decisiones duplicadas o contradictorias.
- Los usuarios HR reciben solicitudes solamente dentro de su alcance empresarial. Las asignaciones autorizadas quedan copiadas en cada solicitud, por lo que cambios posteriores de configuracion afectan solamente solicitudes nuevas.
- `TenantAdministrator` no obtiene permiso operativo por su rol. Puede consultar el estado administrativo del circuito sin recibir datos personales; solamente actua si posee ademas el rol operativo y figura en el alcance copiado de la solicitud.
- Cada aprobacion o rechazo vuelve a verificar en backend usuario activo, tenant, rol operativo, alcance copiado, nivel actual, estado y prohibicion de autoaprobacion. No es posible saltar niveles mediante identificadores o rutas.
- `/tenant/solicitudes` muestra vacaciones y permisos pendientes de mi aprobacion, solicitudes procesadas, nivel actual e historial. La ruta anterior `/tenant/vacaciones/solicitudes` se conserva como compatibilidad.
- La migracion tenant `CorrectVacationApprovalCircuit` agrega configuracion, alcances, pasos y actores. Inserta el circuito predeterminado sin alterar solicitudes ni movimientos existentes.
- Antes de preparar la migracion se reviso el PostgreSQL local: ningun schema local contenia aun `vacation_request`. En ambientes donde existan solicitudes pendientes del circuito anterior, se conservan intactas con su reserva y aparecen como pendientes de regularizacion; el administrador debe aplicar expresamente el circuito actual desde la pantalla de configuracion. Solicitudes aprobadas o rechazadas no se modifican.

### Verificacion de esta entrega

- Casos cubiertos: lunes a viernes, lunes a sabado, jornada que incluye domingo, uno o varios feriados, cambio de jornada con vigencia y consumo en varios periodos empezando por uno parcialmente consumido.
- Tambien se verifica la instantanea inmutable, aprobacion/rechazo/cancelacion repetidos, ausencia de duplicados, saldos no negativos, jornada obligatoria en alta individual y rechazo del Excel sin jornada.
- La migracion tenant `Phase4VacationRequests` agrega solicitudes, detalle diario y asignaciones FIFO sin modificar los motores existentes.
- Los casos del circuito verifican reserva al presentar, aprobacion intermedia sin consumo, aprobacion final, rechazo en ambos niveles, administrador sin rol operativo, alcance incorrecto de departamento/empresa, autoaprobacion, reintentos, multiples autorizadores, configuracion inmutable, cancelacion configurable y regularizacion sin alterar la reserva.
- Los permisos verifican uno o varios dias, con/sin goce, horas, descuento de descanso, intervalos invalidos, limites de jornada, feriados y superposiciones con permisos o vacaciones.
- El circuito de permisos verifica Jefe -> HR, rechazo en ambos niveles, administrador sin rol operativo, alcances incorrectos, autoaprobacion, doble clic y cancelacion configurable con conservacion del historial.
- El outbox verifica destinatarios del primer y siguiente nivel, funcionario, error SMTP sin perdida de solicitud, reintento y ausencia de eventos duplicados. Las transiciones de vacaciones tambien generan avisos sin alterar FIFO.
- La migracion tenant `Phase4PermissionsAndNotifications` agrega permisos, sus instantaneas/pasos y el outbox de forma aditiva. Se aplico a `Gruppo Prova` y `Grupo QA Funcional` sin recrear schemas ni modificar legajos o datos funcionales.
- Compilacion Release: 0 advertencias y 0 errores. Las 42 pruebas completas finalizaron correctamente y ambos modelos EF no tienen cambios pendientes.

### Rutas y prueba manual

- Funcionario: `/empleado/permisos` -> calcular -> presentar -> consultar detalle o cancelar.
- Autorizador/HR: `/tenant/solicitudes` -> `Permisos` -> resolver el nivel pendiente.
- Administrador tenant: `/tenant/vacaciones/circuito` -> niveles, alcances, cancelacion y estado de notificaciones.
- Casos manuales recomendados: permiso de varios dias con un feriado; permiso 11:00-14:00 con descanso 12:00-13:00; rechazo de un solapamiento; aprobacion Jefe -> HR; cancelacion posterior; SMTP deshabilitado con evento pendiente.

### Limitaciones pendientes

- Los permisos con/sin goce son clasificaciones administrativas: no existen liquidacion, descuento salarial ni integracion payroll.
- Al cierre de la Fase 4 todavia no existian adjuntos, ausencias, alertas ni reportes; quedaron incorporados posteriormente en la Fase 5 descrita al inicio de este documento.

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
- Cada alta individual y cada fila de importacion deben indicar una jornada; no existe asignacion estandar implicita.
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
- La ley publicada usa `dias corridos` y la comunicacion MTESS usa `dias habiles`. La regla funcional de solicitud fue definida en Fase 4, pero su correspondencia con `dias habiles corridos` sigue pendiente de validacion juridica.
- No se implementaron trabajadores zafrales, liquidacion/indemnizacion, notificaciones de alerta ni alcances configurables por empresa/departamento.

### Rutas de navegacion Fase 3

- Nomina y acceso: `/tenant/funcionarios` -> `Ficha` -> `Acceso al sistema`.
- Vacaciones individuales: `/tenant/funcionarios/{employmentId}` -> `Vacaciones`.
- Autoservicio: `/empleado`.
- Solicitud propia de vacaciones: `/empleado/vacaciones`.
- Bandeja operativa y estado del circuito: `/tenant/vacaciones/solicitudes`.
- Configuracion tenant del circuito: `/tenant/vacaciones/circuito`.
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
5. Obtener validacion juridica documentada sobre la correspondencia entre la regla funcional aprobada y la expresion legal `dias habiles corridos`.
6. Ejecutar la aceptacion manual completa de la Fase 5 con usuarios reales de prueba para cada rol.
7. El deployment permanece pendiente por falta de VPS. No se configuro Ubuntu, Nginx, systemd, DNS, HTTPS ni infraestructura de produccion.
8. ClamAV debe estar disponible y configurado como obligatorio antes de aceptar documentos en produccion.

## Proxima actividad autorizada

Realizar la aceptacion manual del MVP. La publicacion sera una actividad futura separada cuando exista el VPS; no iniciar funcionalidades posteriores al MVP sin autorizacion expresa.
