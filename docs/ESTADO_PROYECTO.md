# Estado del proyecto HR ProTools

**Fecha:** 24/11/2026
**Fase actual:** Fase 5/5 - cierre funcional del MVP
**Estado:** MVP funcional implementado y verificado automaticamente; pendiente de aceptacion manual y publicacion futura.

## Calendario perpetuo y día libre empresarial — 24/11/2026

### Calendario oficial

- **Causa:** la consulta anterior solo leía `platform.official_holiday_version`. La carga inicial publicaba 2026 y no existía una regla recurrente para otros años. El selector de año no generaba ocurrencias y 2027 quedaba vacío.
- Se incorpora `platform.official_holiday_rule` como fuente global de recurrencias: trece fechas anuales según [Ley 7544/2025, arts. 1–4](https://www.bacn.gov.py/leyes-paraguayas/12908/ley-n-7544-2025). Once fechas fijas (incluidos ambos días de Independencia y Jura de la Constitución), Jueves Santo y Viernes Santo calculados mediante el algoritmo gregoriano Meeus/Jones/Butcher. Las reglas se activan técnicamente desde 2027 hasta el máximo de la interfaz, 2200; no se extrapola esta ley a años anteriores ni se sustituyen las publicaciones de 2026.
- Un único resolver alimenta el panel Global, el calendario integrado y `WorkCalendarService`. Consultar un año no inserta ocurrencias. Sus identificadores son estables; las revisiones persistidas, incluso las bajas o traslados fuera del año nominal, prevalecen sobre la base calculada. No aparece simultáneamente la fecha antigua de un traslado.
- Se distinguen recurrencia calculada, fecha nominal susceptible de traslado, traslado confirmado y extraordinario publicado. Las cuatro fechas móviles del art. 2 se mantienen en su fecha nominal mientras no se registre disposición; no se supone un traslado automático a lunes. El panel Global conserva la posibilidad de corregir o trasladar cualquier ocurrencia, con fuente, disposición e historial. Un extraordinario no se convierte en recurrencia.
- La confirmación de integridad anual sigue siendo administrativa y separada de las fechas legales conocidas. Los años futuros muestran esas fechas aunque la revisión de posibles decretos siga pendiente; no se afirma haber verificado decretos futuros. El tenant conserva filtros de año, empresa y origen, oficiales de solo lectura y adicionales de empresa, con coincidencias sin doble cómputo.

### Política empresarial y solicitud

- En `/tenant/vacaciones/politicas` se conservan los adicionales de vacaciones y se agrega **Beneficio de día libre por política empresarial**, con empresa, vigencia y selector exclusivo **0 / 0.5 / 1**. Ambos historiales se muestran separados. Sin versión, el derecho es 0; no se crean beneficios retroactivos.
- Solo Administrador Tenant publica versiones dentro de su tenant. La vigencia debe ser hoy o futura y posterior a la versión anterior. Las versiones no se editan; las solicitudes guardan política, empresa, cantidad, vigencia y versión en la instantánea del permiso existente.
- En `/empleado/permisos`, **Día libre por política empresarial** aparece cuando la empresa concede beneficio para la fecha seleccionada. Para una vigencia futura, elegir primero la fecha inicial: la lista se actualiza. Solo Employee presenta su propia solicitud; se reutilizan las aprobaciones Jefe/HR del circuito ordinario. Global no adquiere acceso personal y el código empresarial no puede publicarse como categoría legal global.
- Un disfrute por **persona y año civil** dentro del tenant, sin acumulación entre años ni conversión a vacaciones. La reserva pendiente, aprobación, rechazo y cancelación usan los estados y detalles diarios existentes. Tanto el máximo de jornadas como el disfrute único se comprueban en backend y en transacción serializable; un cambio de legajo no reinicia el derecho.
- Jornada completa: conserva jornada y minutos efectivos en el detalle diario. Media jornada: primera o segunda mitad efectiva, intervalo calculado excluyendo la pausa, y 50% de los minutos contractuales. No permite cantidades arbitrarias ni intervalos elegidos para ampliar el beneficio. Un horario con minutos efectivos impares requiere una configuración compatible antes de solicitar dos mitades exactamente iguales.

### Vacaciones, superposiciones e integridad

- Para nuevas vacaciones, un día completo empresarial **aprobado** de la misma persona deja de consumir vacaciones exclusivamente para ella. No se crea un feriado global ni de empresa. La instantánea diaria de vacaciones guarda `CompanyDayOffRequestId`, además de la jornada y condición de cómputo; el beneficio permanece identificable sin etiquetarlo como feriado nacional.
- Un beneficio pendiente bloquea el intervalo hasta resolverlo. Una media jornada pendiente o aprobada bloquea vacaciones de jornada completa con mensaje explícito de conflicto parcial: no se transforma en festivo ni en medio día de vacaciones. No se implementa un motor de vacaciones fraccionarias.
- La presentación del beneficio rechaza feriados oficiales/adicionales, descansos, vacaciones previas pendientes/aprobadas, permisos pendientes/aprobados y ausencias. No modifica esas solicitudes. Si vacaciones nuevas excluyeron un beneficio completo, no se permite cancelar ese beneficio mientras las vacaciones dependientes estén pendientes/aprobadas: primero deben cancelarse y presentarse los intervalos corregidos. La resolución es explícita y conserva trazabilidad.
- No se recalculan solicitudes anteriores por nuevas políticas o cambios de calendario. No se modifica la estructura ni la lógica del ledger FIFO: solo la cantidad de días de la nueva reserva, a partir del calendario efectivo y su instantánea.

### Migraciones y verificación

- Global: `20261124191712_PerpetualOfficialHolidayRules`, tabla de reglas y trece recurrencias. Tenant: `20261124191716_CompanyDayOffBenefit`, tabla de políticas y referencia nullable en la instantánea diaria de vacaciones. Migraciones aditivas; no recrean schemas ni cambian usuarios, roles, períodos, solicitudes o feriados existentes. Aplicadas al PostgreSQL local, incluidos A y QA27, sin arrancar workers ni enviar correos. El mismo mecanismo EF opera con PostgreSQL nativo y no requiere Docker en producción.
- Verificación sintética real sobre PostgreSQL: calendario 2026 conservado; 2027, Semana Santa 2028, 2038, 2100 y 2200; consultas idempotentes sin escrituras; traslado de una recurrencia, baja y extraordinario sin repetición; integración con adicionales y restricciones de escritura. Beneficios 0/0.5/1, alcance de empresa/tenant, versiones, reserva, aprobación, rechazo, cancelación idempotente, concurrencia, reingreso, nuevo año, descansos y superposiciones.
- Caso solicitado: saldo inicial sintético de 12 días; beneficio completo aprobado el miércoles 10/03/2027; vacaciones 08–12/03/2027. Vista previa y solicitud definitiva: **5 fechas, 4 días de vacaciones y 1 jornada empresarial**. La reserva/aprobación FIFO se comprueba separadamente de la utilización del beneficio. Otro funcionario conserva 5 días laborables; las dos medias jornadas de una jornada 08–17 con pausa 12–13 equivalen a 240 minutos, 08–12 o 13–17.
- `dotnet build --configuration Release --no-restore -m:1`: **0 advertencias, 0 errores**. Suite completa Release contra PostgreSQL: **102 superadas, 0 fallidas, 0 omitidas**, 1 min 52 s. Tras las últimas mejoras de disponibilidad de categorías y del historial de recurrencias, repetición dirigida: **12 superadas**, 0 fallidas/omitidas, 24 s. Incluye rendering de los dos paneles y regresión de administración/calendario. Ambos modelos EF sin cambios pendientes.
- Integridad comparada por conteo e impronta: de las **92 tablas previas**, solo cambian las tres tablas técnicas de historial de migraciones. Las otras **89** conservan sus datos originales (comparación de `vacation_request_day` excluyendo únicamente la nueva columna nullable). Aparecen las tres tablas previstas: reglas globales con 13 filas y políticas de A/QA27 vacías. Usuarios, roles, feriados publicados, personas, períodos, movimientos y solicitudes reales preservados; ninguna política se asignó automáticamente. Los escenarios de prueba crean y limpian exclusivamente sus datos sintéticos.

### Cómo verificar manualmente

1. Reiniciar HR ProTools. Global → **Feriados oficiales**, seleccionar 2027 y 2028: comprobar recurrentes, Semana Santa, estado y fechas nominal/efectiva. Un traslado requiere fuente y disposición; revisar que quede una sola ocurrencia efectiva.
2. Tenant → **Jornadas y feriados**, seleccionar año y empresa: comprobar integración, filtros y oficiales sin edición.
3. Tenant → **Políticas de vacaciones**, segundo panel: guardar 0.5 o 1 desde hoy o una fecha futura, sin tocar los adicionales de vacaciones.
4. Con Employee de prueba de esa empresa, **Mis permisos**: elegir fecha y **Día libre por política empresarial**, calcular, comprobar saldo y presentar. Para media jornada, seleccionar primera/segunda mitad. Resolver con los actores del circuito y comprobar reserva/consumo.
5. Tras aprobar un día completo, calcular vacaciones que lo incluyan: debe figurar como día libre empresarial sin descuento. Con media jornada, comprobar el mensaje de conflicto. Realizar aceptación visual integral con sesiones de prueba; el rendering automatizado no sustituye la comprobación completa de navegación e interacción.

Sin cambios en `site`, `mockup` o iMatch. Sin deployment, commit ni push.

## Fichas legales y auditoría funcional de cupos — 24/11/2026

### Interfaz y navegación

- En `/admin/permisos-legales` y `/tenant/permisos-legales`, **Ficha** abre un diálogo modal centrado en la pantalla, por encima del encabezado e independientemente del scroll de la tabla. Muestra **Ficha del permiso legal**, nombre, código, número de versión y **Cerrar**. Escape cierra; el foco inicial está en Cerrar, Tab/Shift+Tab permanecen en el diálogo y al cerrar el foco regresa al botón que lo abrió. El contenido largo tiene scroll interno y distribución móvil.
- Cada ficha empieza con **Duración y límites legales**: duración, ámbito del límite, unidad legible, remuneración y revisión. Se eliminan de la vista principal identificadores como `EVENT_DAYS` y la combinación ambigua «Sin máximo simple / —». No mezcla consumos personales con límites globales.
- Las categorías parametrizadas leen máximo/unidad/límites de su versión publicada. Las especializadas comparten ahora parámetros numéricos entre validación y presentación (`LegalPermissionLimits`); conservan sus reglas y excepciones. El texto libre de duración ya no sustituye los límites estructurados en la ficha. Se mantienen maternidad/paternidad/adopción en semanas consecutivas y los porcentajes/recurrencias específicos.
- En `/empleado/permisos`, la categoría y el período seleccionados muestran utilizados/aprobados, reservados y disponibles, más las solicitudes de esa categoría con fecha de presentación, intervalo, legajo, cantidad, aprobación, reserva y estado. Incluye los vínculos previos de la misma persona. «Utilizados» significa comprometidos por solicitudes aprobadas, incluso con fechas futuras; no se suma de nuevo al aprobar.
- La presentación queda deshabilitada sin cupo, mientras se consulta el saldo o cuando falta un cálculo vigente. Cambiar fechas, unidad o datos del supuesto invalida la previsualización. Las horas de acompañamiento, búsqueda y control se muestran también en horas, conservando los minutos efectivos.
- HR consulta el mismo resumen en el detalle del permiso de `/tenant/solicitudes`, sujeto a su alcance. El detalle de otras empresas no autorizadas se oculta; no se habilitan consumos para Jefe/Admin Global ni para otras personas. Los historiales de relaciones cerradas siguen consultables; el modo de lectura histórica no habilita nuevas solicitudes sobre un vínculo cerrado.

### Qué funcionaba y qué se corrigió

- Ya funcionaban la agregación por persona dentro del tenant, los dos cupos preventivos separados, la suma de exámenes dentro de su categoría, los estados pendiente/aprobado/rechazado/cancelado, el bloqueo transaccional, las fracciones de jornada, las duraciones especializadas y las instantáneas normativas.
- Se unificó la lectura de cupos e historial en `PermissionQuotaQueries`, reutilizando solicitudes, fechas y estados existentes tanto para validar como para mostrar el saldo. No se creó otro ledger ni un motor de permisos independiente.
- Se corrigió una falta de acumulación **diaria** en lactancia y búsqueda por horas: dos solicitudes no superpuestas podían satisfacer individualmente la duración diaria. Ahora se suman minutos pendientes y aprobados de la misma persona/fecha, y se rechaza el exceso aun cuando los intervalos no se crucen. La cancelación libera el cupo por estado, sin doble devolución.
- Los conflictos de serialización/concurrencia al presentar se traducen a un mensaje comprensible que pide actualizar el saldo y reintentar; no se crea una reserva adicional. Las validaciones continúan ejecutándose en backend aunque se omita la interfaz.
- La vista previa diaria no ofrece cupos en fechas sin jornada laboral aplicable. El historial utiliza las fechas almacenadas; no se reescriben solicitudes por cambios posteriores del catálogo.
- Las fechas iniciales del autoservicio y del evento se toman de America/Asuncion, independientemente de la zona horaria del servidor.

### Verificación PostgreSQL

Se utilizaron exclusivamente personas, usuarios, schemas, solicitudes y referencias sintéticos; no se prepararon pruebas modificando registros reales. Los escenarios se verificaron con servicios de presentación y decisión, consultando después filas persistidas y saldos derivados:

| Escenario | Comprobación |
|---|---|
| A y B: 1 día aprobado + 1 día aprobado | Utilizados 1/disponibles 1; después utilizados 2/disponibles 0, para ambas categorías preventivas |
| C: tercera solicitud | Rechazo del backend con mensaje de máximo/cantidad disponible; no aparecen nuevas solicitudes ni fechas |
| D: reserva pendiente | Los 2 días pendientes bloquean una presentación adicional; aprobar traslada reserva a utilizado sin duplicar |
| E: rechazo | Revisión HR de requisitos no cumplidos y rechazo liberan la reserva; permanece el historial |
| F: cancelación | Anulación válida de una solicitud aprobada y reintento idempotente liberan una sola vez |
| G: concurrencia | Dos presentaciones simultáneas de 2 días aceptan exactamente una y persisten solo 2 días reservados |
| H: cambio de año | Una solicitud con fechas en diciembre/enero se imputa por cada fecha, no por fecha UTC de creación; cupos separados y comprobación de frontera horaria America/Asuncion |
| I: reingreso | Cambiar de legajo con la misma persona no reinicia el cupo anual; el resumen conserva el consumo anterior |
| Exámenes y separación legal | PAP/mamografía y órganos preventivos comparten su propio cupo; las dos categorías no se intercambian. Colon para Sexo F conserva 2 días |
| Evento | Matrimonio y luto acumulan solicitudes parciales hasta 3 días por evento; otro evento mantiene su propio límite |
| Obligación legal | Horas de mañana/tarde suman fracciones guardadas; límite de 2 días/mes y 15/año, con separación de meses y prueba anual previa |
| Discapacidad | Mes con 20 horas disponibles y otro con 12 según jornada/calendario sintéticos; agotamiento y exceso horario rechazado |
| Prenatal | Persistencia de 4 horas y ampliación acreditada de 5 horas; bloqueo de ampliación sin aval y del incumplimiento de preaviso no urgente |
| Lactancia y búsqueda | Recurrencias y extensiones; bloqueo de duplicados diarios no superpuestos, modalidad exclusiva, período de preaviso y día semanal |
| Duraciones y excepciones | Se mantienen las pruebas previas de maternidad ordinaria/especial/múltiple/transferida, paternidad y adopción; no se convierten a días laborables |
| Aislamiento y versiones | Distintas personas y tenants no comparten consumo; HR fuera de alcance y jefe no acceden al saldo ajeno; una versión nueva no reescribe la instantánea anterior |

- Inspección agregada de A y QA27: **0 solicitudes con código legal** preexistentes. En A: 0 solicitudes legales sin detalle diario, 0 fracciones horarias legales faltantes y 0 grupos preventivos sobre el límite. Por tanto no hay una población histórica legal en estos tenants sobre la que afirmar una auditoría retrospectiva de casos reales; la verificación funcional se realiza con los escenarios sintéticos descritos. No se corrigió ningún dato histórico silenciosamente.
- Comparación de **92 tablas** de A, QA27, Identity y datos globales: mismos conteos e improntas antes/después de la suite. Usuarios, roles, catálogos, calendarios, datos personales, solicitudes reales y ledger FIFO conservados. No se requiere migración; no se modificaron entidades persistentes.
- `dotnet build --configuration Release`: compilación correcta, **0 advertencias y 0 errores**. Suite completa Release contra PostgreSQL: **94 pruebas superadas, 0 fallidas y 0 omitidas**; última ejecución de 1 min 33 s con `dotnet test --configuration Release --no-build --no-restore -m:1`. Incluye las 80 pruebas anteriores y 14 casos adicionales de auditoría/rendering. Ambos modelos EF (global y tenant) sin cambios pendientes respecto a sus migraciones. El cotejo final posterior a esta ejecución vuelve a confirmar las 92 tablas sin diferencias. La conexión de pruebas utiliza `Pooling=false`; no se cambia la conexión de la aplicación.

### Verificación de interfaz y límites de la entrega

- Rendering automatizado de las quince fichas originales y de límites parametrizados cambiados, incluyendo ausencia de unidades técnicas, duración destacada y resumen de cupo agotado.
- Prueba real en navegador sobre HTML renderizado por los componentes Blazor y su CSS/JavaScript originales, con lista sintética larga: ficha desde primera y última fila, centrado independiente del scroll, encabezado superpuesto correctamente, botón Cerrar, Escape, foco inicial, ciclo Tab/Shift+Tab y restauración al disparador. Comprobado en escritorio y viewport móvil de 375 px, con scroll interno y reglas especializada/parametrizada. El servidor de esta prueba no conecta a PostgreSQL ni inicia workers.
- Para reproducir esa prueba aislada: establecer `HRPROTOOLS_UI_EVIDENCE_DIR=C:\HRProTools\artifacts\legal-ui`, ejecutar el test `LegalDialogAndDurationRenderStructuredPublishedValues` en Release y luego `node tests/HRProTools.Tests/fixtures/serve-legal-dialog.cjs`; abrir `http://127.0.0.1:5179` y la opción móvil. Es un soporte de prueba, no otro sitio de producto.
- Para aceptación completa en la aplicación: reiniciar HR ProTools; abrir **Catálogo legal → Ficha** desde el tenant o **Permisos legales → Ficha** desde Global; en una cuenta Employee sintética elegir categoría/fechas en **Mis permisos**; verificar saldos y bloqueo; resolver desde una cuenta HR autorizada y volver a consultar. La prueba visual aislada no sustituye esta aceptación integral con sesión y navegación Blazor.
- Se conservan las condiciones jurídicas pendientes ya documentadas: cómputo de fechas de matrimonio/luto bajo revisión HR; elegibilidad documental sin inferirla exclusivamente por Sexo; excepciones de lactancia, maternidad/adopción y extensión prenatal sujetas a su revisión. No se incorpora una nueva identidad documental de control prenatal ni se inventa un cupo anual para controles: se comprueba la duración por presentación y HR verifica el control concreto. El cupo mensual de discapacidad depende de jornada/calendario aplicables; no se alteran minutos históricos aprobados por una corrección de calendario.
- Sin cambios en sitio Astro, mockups, iMatch, Identity, SMTP, reportes o calendario oficial. Sin deployment, commit ni push. La única adaptación del calendario es un modo interno de lectura de jornadas de relaciones cerradas para consultar su saldo histórico; las nuevas solicitudes siguen exigiendo vínculo activo.

## Manuales de uso por rol — 23/11/2026

Se incorporaron cinco manuales independientes en italiano, con nombres de menús y botones en español, correspondientes a las funciones actuales del MVP:

- [Administrador Tenant](Manuale-01-Admin-Tenant.md): configuración del grupo, organización, funcionarios, historiales, usuarios, jornadas, feriados, vacaciones, circuito, Excel y logo.
- [Administrador Global](Manuale-02-Admin-Globale.md): grupos, SMTP, catálogo legal versionado y calendario oficial.
- [Recursos Humanos](Manuale-03-Recursos-Humanos.md): solicitudes, verificación legal, documentos, ausencias, preaviso, alertas y reporte.
- [Jefe Autorizador](Manuale-04-Jefe-Autorizador.md): decisiones por nivel, ámbito departamental y registro de ausencias.
- [Usuario Employee](Manuale-05-Utente.md): activación, datos propios, vacaciones, permisos, documentos y declaración de ausencias.

Incluyen rutas, procedimientos, estados, límites de cada rol y resolución de problemas frecuentes. Entrega exclusivamente documental; no modifica funciones, datos ni autorizaciones.

## Ampliación autorizada — permisos legales de Paraguay y Sexo

- Catálogo estructurado y versionado: matrimonio, luto, paternidad, maternidad y sus supuestos especiales, adopción/acogimiento judicial, lactancia recurrente, control prenatal, PAP/mamografía, prevención de próstata/testículos/pene/colon, acompañamiento de discapacidad, obligación legal, búsqueda de empleo durante preaviso y permiso sin goce. El permiso genérico con goce previo se conserva para compatibilidad. Reposo médico remite al módulo existente de ausencias y no se duplica.
- No se incorporaron las categorías expresamente excluidas. No se calcularon salarios, descuentos ni subsidios IPS.
- Cada solicitud conserva tipo legal, ley/artículo, versión y vigencia de la regla operativa, duración/unidad, requisitos, excepciones, documentación/preaviso, régimen, supuesto declarado y Sexo al presentar. Los datos históricos anteriores siguen intactos y no se reclasifican automáticamente.
- Solo Employee, en su vínculo autenticado, presenta desde `/empleado/permisos`. No existe presentación por administrador, jefe ni HR en nombre de terceros. Para derechos protegidos se reutilizan los niveles HR del circuito existente como verificación de requisitos; el jefe no recibe capacidad discrecional de rechazo. Se impide resolver solicitudes propias.
- HR registra verificación fundamentada, requisitos no cumplidos o información pendiente. No puede rechazar un derecho marcado como verificado. Se guardan plazos expresos cuando corresponde documentación posterior. El circuito ordinario y vacaciones conservan su funcionamiento.
- Cupos por evento, mes, año y jornada. Pendientes cuentan como reserva una sola vez; aprobación no vuelve a consumir y cancelación/rechazo libera por estado. Creación serializable e idempotente con controles de concurrencia. PAP/mamografía comparte dos días; prevención comparte dos días entre órganos y no se reduce a un día para colon por Sexo F. Obligaciones computan fracciones por jornada y acompañamiento usa el 50% del calendario mensual real.
- Lactancia y búsqueda por horas usan un registro recurrente con detalle diario de jornada, intervalo y minutos. Las listas muestran el período completo. Búsqueda solo dentro de preaviso de despido verificado por HR con modalidad exclusiva en `/tenant/permisos/preaviso`; este respaldo no crea solicitudes.
- Documentos de permisos reutilizan el validador de adjuntos y ClamAV: PDF/JPEG/PNG, 5 MB, SHA-256, `bytea` en tenant. Empleado carga solo sobre su solicitud; HR revisa y descarga dentro de su alcance y solo con estado `Clean`. Jefe/administrador no adquieren acceso médico por su rol. Los correos omiten comentarios sensibles de decisiones legales y no incluyen examen ni diagnósticos.
- `person.Sex`: únicamente M/F como valores administrativos; altas exigen selección explícita sin inferencias. Los datos históricos faltantes se conservan temporalmente como `NULL`, sin categoría adicional ni valor predeterminado. Ficha → Datos generales permite completarlos por TenantAdministrator con motivo e historial propio de actor/fecha/valores, sin recalcular licencias.
- Plantilla `Funcionarios`: columna M `Sexo`, lista M/F, lector/generador actualizados y errores por fila. Personas nuevas requieren valor; importaciones/reingresos no sobrescriben Sexo existente. La hoja `Vacaciones` se conserva.
- Migración `20261123174711_LegalPermissionsAndPersonSex` aplicada a los tenants locales A y QA27 mediante script EF idempotente, sin iniciar workers ni correos. Catorce comprobaciones de conteos e improntas antes/después confirmaron que persona (excluido nuevo Sexo), vínculos, asignaciones, salarios, períodos, movimientos y solicitudes conservan sus datos. El valor técnico inicialmente introducido por esa migración se corrige posteriormente a `NULL` mediante `PersonSexNullableHistorical`, sin cambiar M/F ni otros datos.
- Validación automatizada: compilación Release sin advertencias ni errores; 68 pruebas superadas, 0 fallidas, 0 omitidas. Cubren sexo/Excel, licencias, supuestos especiales, recurrencia, extensión prenatal, cupos mensuales/anuales, concurrencia, cancelación, autorización, documentación e aislamiento, además de toda la regresión anterior. El proceso de prueba utiliza `Pooling=false` para no agotar conexiones de los numerosos schemas sintéticos; no se cambió la conexión aplicativa.
- Matriz jurídica, fuentes oficiales, reglas implementadas, limitaciones y rutas: [04-Permisos-Legales-Paraguay.md](04-Permisos-Legales-Paraguay.md). Pendiente aceptación manual de las nuevas pantallas y revisión jurídica especializada de vigencias, cómputos y supuestos excepcionales antes de producción. No se declara validación legal integral.
- iMatch y `site/` no se modificaron. No se realizó deployment, commit ni push de esta ampliación; los archivos del sitio y mockups previamente no versionados se mantienen fuera de su alcance.

### Corrección puntual de Sexo — 23/11/2026

- Se eliminó el valor inicial/default de Sexo y se ajustó el constraint a `Sex IS NULL OR Sex IN ('M','F')`. `NULL` es exclusivamente la situación técnica de dato histórico faltante, nunca una opción administrativa. Las altas y las personas nuevas importadas siguen exigiendo M/F y rechazando valores vacíos o desconocidos.
- Migración posterior `20261123180532_PersonSexNullableHistorical` aplicada a A y QA27. Convierte solo `person.Sex = 'ND'` a `NULL`; no toca M/F. En A se convirtió un registro histórico; QA27 no tenía personas. Verificado: cero ND y columna nullable sin default en ambos schemas.
- Dieciséis controles de conteos e improntas confirmaron conservación de los demás datos personales, historial de cambios, vínculos, asignaciones, salarios, vacaciones y solicitudes. Las migraciones previas e instantáneas históricas permanecen intactas.
- La ficha muestra el dato faltante como pendiente de completar y permite guardar M/F con autorización y motivo. El historial admite un valor anterior `NULL`, sin inventar una categoría ni recalcular licencias.
- Build Release: 0 errores y 0 advertencias. Suite completa: 70 pruebas superadas, 0 fallidas, 0 omitidas. Incluye migración desde la versión anterior con ND/M/F, conservación de datos, rechazo del sentinel en PostgreSQL y de NULL en altas, validación Excel y actualización autorizada desde la ficha. Modelo EF sin cambios pendientes.
- No se modificaron otras funcionalidades, el sitio Astro ni iMatch. No se realizó deployment, commit ni push.

## Administración global de permisos y feriados — 23/11/2026

### Paneles y navegación

- Administrador Global → **Permisos legales**, `/admin/permisos-legales`: búsqueda, filtro activo/inactivo, ficha, alta parametrizada, edición mediante nuevas versiones, activación/desactivación con vigencia y consulta de versiones anteriores. Conserva código, nombre, descripción, ley/artículo/fuente, vigencias, duración/unidad, límites, beneficiarios, requisitos documentales, preaviso, remuneración y naturaleza. No elimina categorías ni solicitudes históricas. Las cinco categorías expresamente excluidas siguen bloqueadas para nuevas publicaciones.
- Administrador Global → **Feriados oficiales**, `/admin/feriados-oficiales`: calendario anual y lista cronológica; alta, modificación de cualquier fecha efectiva (también fechas tradicionalmente fijas), desactivación e historial con actor, fecha, motivo y fuente. Distingue **traslado oficial** y **corrección administrativa**; exige disposición para traslados y extraordinarios. Mantiene fecha conmemorativa y efectiva, computando exclusivamente la efectiva.
- Administrador Tenant → **Catálogo legal**, `/tenant/permisos-legales`: consulta de la versión vigente, sin controles de escritura. GlobalAdministration/TenantAdministration protegen las rutas; los servicios vuelven a comprobar rol, cuenta activa y tenant. HR, Jefe y Employee no reciben permisos administrativos nuevos.
- Administrador Tenant → **Jornadas y feriados**, `/tenant/jornadas`: conserva jornadas y muestra el calendario integrado, con filtros por año, empresa y origen. Los oficiales son solo lectura; los adicionales se agregan por empresa y se quitan mediante baja lógica con actor, fecha y motivo. Las coincidencias permanecen identificadas, sin duplicar el día de descanso. No acepta empresas ni identificadores de otro tenant.

### Reglas, cálculos y preservación

- El catálogo operativo reside en `platform.legal_permission_version`, compartido entre tenants. La clase estática anterior se conserva como base inicial/compatibilidad de reglas, no como catálogo editable paralelo. Se adaptaron los quince códigos existentes sin reescribir solicitudes anteriores.
- Las nuevas presentaciones resuelven la versión activa cuya vigencia corresponde a la fecha de presentación, en horario de Paraguay. Guardan su identificador y regla completa en la instantánea existente. Nombres en historial, circuito y reporte proceden de esa instantánea, incluso para categorías nuevas o posteriormente desactivadas. Un reintento idempotente de una solicitud presentada sigue funcionando tras la desactivación.
- Se amplían las reglas existentes con cantidades simples: días laborables, horas/minutos laborables, semanas consecutivas y días/horas con fracciones de jornada. Una categoría simple nueva exige duración máxima numérica; puede configurar límites por evento, mes y año. Pendientes y aprobados de la misma persona consumen cupo; rechazados y cancelados no. Para preservar consumos anteriores, cambiar la unidad de una categoría requiere otra categoría. Condiciones de elegibilidad y documentación son verificadas por HR cuando su naturaleza es obligatoria.
- Maternidad, lactancia, discapacidad, controles prenatales, búsqueda de empleo y demás reglas especializadas conservan sus validaciones. El panel permite versionar datos descriptivos, fuentes y estado, pero rechaza modificaciones numéricas/de unidad/naturaleza incompatibles con su lógica. No se incorporó un lenguaje de reglas ni un segundo circuito de solicitudes. La duración textual es descriptiva; los campos numéricos gobiernan los límites de categorías simples.
- `WorkCalendarService` combina oficiales vigentes y adicionales activos por empresa; agrupa por fecha. Se selecciona primero la última revisión de cada feriado y después su fecha efectiva, evitando conservar por error la fecha anterior de un traslado.
- Vacaciones, permisos por días/horas, ausencias y licencias que usan calendario laboral consumen esa misma combinación. Las semanas consecutivas de maternidad/paternidad mantienen su cómputo propio.
- No se recalculan solicitudes presentadas, reservas, movimientos FIFO, aprobaciones ni duraciones históricas. El calendario tenant advierte cuántas solicitudes activas con fechas futuras conservan indicadores de feriado diferentes del calendario actual; corresponde revisar cada caso individualmente. No hay recálculo masivo.

### Calendario inicial y fuentes

- Base normativa: [Ley 7544/2025, BACN](https://www.bacn.gov.py/leyes-paraguayas/12908/ley-n-7544-2025-que-determina-los-feriados-nacionales-de-la-rep-blica-del-paraguay-se-establecen-los-feriados-m-viles-y-se-faculta-al-poder-ejecutivo-a-instituir-otros-feriados-en-situaciones-especiales) y [comunicación oficial MTESS](https://mtess.gov.py/?p=31706).
- Carga inicial de catorce fechas de 2026, con fuentes guardadas por registro. Incluye 01/03 → 02/03 ([Decreto 5508/2026, Radio Nacional](https://www.radionacional.gov.py/2026/02/17/ejecutivo-decreta-traslado-del-feriado-del-dia-de-los-heroes-al-lunes-2-de-marzo/)), 20/06 → 22/06 ([Decreto 6215/2026, Agencia IP](https://www.ip.gov.py/ip/2026/06/09/gobierno-traslada-feriado-por-la-jura-de-la-constitucion-de-1992-al-lunes-22/)), 29/09 → 28/09 ([Decreto 6601/2026, DNCP](https://www.contrataciones.gov.py/licitaciones/convocatoria/1f18f46a-8ce5-6880-9680-af6dce24a6d2/verificaciones/1788940704.html)) y el extraordinario del 30/06 ([Decreto 6280/2026, Ministerio Público](https://ministeriopublico.gov.py/nota/ministerio-publico-declara-feriado-institucional-con-excepciones-para-garantizar-servicios-esenciales-17544)). Semana Santa 02–03/04 contrastada con el [calendario oficial BCP 2026](https://www.bcp.gov.py/documents/20117/86461/Feriados%2BSML%2B-%2B2026%2B-%2BV190226.pdf/b16b80fa-aa45-e020-b067-25590cd7c32b?t=1781095767235).
- **2026 queda pendiente de confirmación administrativa de integridad anual.** No se presenta como calendario definitivo. Otros años, incluido 2027, no se extrapolan ni se trasladan automáticamente: se muestran pendientes/no cargados hasta su publicación documentada. La confirmación anual exige fuente y nota; cualquier revisión posterior vuelve a marcar el año pendiente.
- Poder modificar técnicamente cualquier fecha no autoriza jurídicamente un traslado. El Administrador Global debe comprobar y registrar la norma aplicable; una corrección interna queda identificada como tal.

### Migraciones y verificación

- Global aditiva: `20261123184533_GlobalLegalCatalogAndOfficialHolidays`; crea versiones legales, revisiones oficiales y estado anual en `platform`, con carga inicial documentada. Aplicada a PostgreSQL local. Los tenants consultan las tablas globales sin copiar el catálogo/calendario a sus schemas.
- Tenant aditiva: `20261123184545_CompanyHolidayLogicalRemoval`; agrega actividad y metadatos de baja a `company_holiday`, dejando activos todos los adicionales preexistentes. Aplicada a A y QA27 mediante script EF idempotente, sin iniciar workers ni correos. Ambas migraciones quedan disponibles en el mecanismo existente de actualización global/tenant; no requieren Docker en producción.
- Comparación antes/después: 48 comprobaciones de conteos e improntas disponibles de tablas operativas/globales conservaron su resultado; el único cambio adicional esperado entre esas comparaciones es el historial de migraciones. No se modificaron personas, asignaciones, salarios, solicitudes ni saldos reales. Las pruebas usan schemas y referencias sintéticas y limpian sus propios registros.
- Verificación final automatizada: **80 pruebas superadas, 0 fallidas, 0 omitidas** (`dotnet test HRProTools.slnx --configuration Release --no-restore -m:1`, PostgreSQL local, conexión de pruebas con `Pooling=false`). Compilación Release sin errores ni advertencias. Ambos contextos EF confirman que no hay cambios pendientes del modelo; `git diff --check` sin errores. Se comprueban autorización global/tenant y políticas de rutas, versionado/desactivación, vigencia futura, límites simples en cuatro unidades, reglas incompatibles, cambio de fecha fija, fuentes e historial, calendario tenant, baja lógica, coincidencias, aislamiento, vacaciones/permisos/ausencias, preservación de instantáneas/FIFO y rendering de las nuevas pantallas, además de la regresión completa del MVP. Se corrigió un redondeo decimal al validar semanas completas y se ajustó la prueba anual anterior para elegir días laborables reales con el calendario oficial.
- Prueba manual: reiniciar la aplicación; entrar como Global y abrir ambas rutas `/admin/...`; publicar una categoría de prueba con máximo y fuente, consultar su historial y desactivarla; cargar/corregir un feriado de prueba. Entrar como Tenant y consultar `/tenant/permisos-legales` y `/tenant/jornadas`, filtrar empresa/año, agregar y quitar un adicional y revisar una coincidencia. Comparar el cálculo de una nueva solicitud con el detalle de una ya presentada. Pendiente aceptación manual interactiva en navegador con usuarios de prueba; el rendering automatizado no sustituye esa aceptación.
- Sin cambios en `site/`, `mockup/` ni iMatch. Sin deployment, commit ni push de esta entrega.

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

Realizar la aceptación manual del MVP, de permisos legales/Sexo y de los paneles globales/calendarios autorizados el 23/11/2026. Completar la verificación administrativa de los calendarios anuales y obtener la revisión jurídica indicada en la matriz. La publicación será una actividad futura separada; no iniciar otras funcionalidades sin autorización expresa.
