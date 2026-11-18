# HR ProTools - Analisis de reutilizacion

## Alcance y fuentes revisadas

Analisis realizado el 17/11/2026, sin modificar `C:\iMatch2` ni ejecutar migraciones, servicios o bases de datos.

| Fuente | Resultado |
|---|---|
| `C:\HRProTools\Agent.md` | Leido y adoptado como instruccion oficial del proyecto. |
| `C:\HRProTools\base.csv` | Leido: 361 columnas, 52 tablas, todas declaradas en `public`. El archivo no contiene tipos, claves, indices ni relaciones. |
| `C:\HRProTools\mockup\mockup.png` | Revisado como referencia visual; muestra dashboard, solicitudes, vacaciones, funcionarios, departamentos, cargos y autorizadores. |
| `C:\HRProTools\logos\logo.png` y `C:\HRProTools\mockup\logo.png` | Identificados; deberan reutilizarse en la interfaz. |
| `C:\iMatch2\src`, `tests`, `docs` y `deploy` | Revisados en modo lectura. |
| `C:\HRProTools` | No es actualmente un checkout Git: no existe `.git`. |

No habia documentos funcionales o tecnicos adicionales dentro de `C:\HRProTools\docs` al iniciar esta fase.

## Resultado ejecutivo

iMatch aporta una base reutilizable para .NET 10, Blazor Web App, Identity, PostgreSQL, roles, invitaciones, SMTP, Data Protection y despliegue nativo. La reutilizacion debe ser selectiva: los conceptos de autenticacion y tenant son validos, pero el dominio bancario, el `ApplicationDbContext` monolitico y las tablas SQL creadas manualmente por tenant no deben copiarse.

La mayor adaptacion tecnica es sustituir el esquema por banco (`TenantBankCode`) por un grupo empresarial (`TenantId`) y evolucionar cada esquema tenant mediante migraciones versionadas. El dominio de RR. HH. debe desarrollarse desde cero usando los conceptos utiles del CSV, no sus procesos BPM.

## Componentes concretos encontrados en iMatch

| Componente | Ubicacion real y clase | Funcionamiento observado | Decisión para HR ProTools | Dependencias |
|---|---|---|---|---|
| Arranque Blazor e Identity | `src\iMatch.Web\Program.cs` | Registra Blazor interactivo, cookies Identity, Identity Core, roles, EF Core/Npgsql, autorizacion y servicios scoped. | **Adaptar**. Es el punto de partida mas cercano; cambiar nombre de aplicacion, roles, cookies, servicios y dominio. | ASP.NET Core 10, EF Core 10, Npgsql. |
| Contexto Identity | `src\iMatch.Web\Data\ApplicationDbContext.cs`, `ApplicationUser.cs` | `IdentityDbContext<ApplicationUser>` con esquema por defecto `auth`; el usuario agrega nombre, estado y `TenantBankCode`. | **Adaptar** a `TenantId` nullable, conservando `DisplayName`, `IsActive` y la FK restrictiva. Separar claramente datos globales de datos tenant. | Identity EF Core, PostgreSQL. |
| Roles y politicas | `src\iMatch.Application\Security\AppSecurity.cs`; registro en `Program.cs` | Define administrador/operador global y administrador/operador tenant; las politicas impiden mezclar roles administrativos y operativos. | **Adaptar** a `GlobalAdministrator`, `TenantAdministrator`, `HumanResources`, `Approver`, `Employee`. En Fase 2 solo son obligatorios los dos primeros. | ASP.NET Core Authorization. |
| Bootstrap de administrador | `src\iMatch.Web\Data\IdentitySeeder.cs` | Crea roles y un administrador inicial desde variables/configuracion; normaliza roles previos. | **Reutilizacion directa con renombrado**. Mantener secretos fuera del repositorio y retirar la clave bootstrap despues del primer acceso. | `UserManager`, `RoleManager`, configuracion. |
| Login y cuenta | `src\iMatch.Web\Components\Account\Pages\Login.razor` y resto de `Components\Account` | Login por correo, bloqueo por intentos, recuperacion de clave, 2FA/passkeys del template y redireccion segun rol. Verifica tenant activo despues del login. | **Reutilizar y adaptar** marca, rutas y verificacion por `TenantId`. No habilitar registro publico. | Identity, Blazor. |
| Revalidacion de sesion | `src\iMatch.Web\Components\Account\IdentityRevalidatingAuthenticationStateProvider.cs` | Cada 15 segundos valida usuario activo, tenant activo y security stamp; invalida el circuito Blazor si cambia el acceso. | **Reutilizacion casi directa** cambiando la consulta del tenant. | Blazor Server, Identity, EF Core. |
| Registro global de tenants | `src\iMatch.Web\Data\OperationalModels.cs` (`TenantAccount`), mapeo en `ApplicationDbContext.cs`, migracion `20260725184454_AddTenantSystem.cs` | Mantiene codigo del banco, nombre de esquema, estado y fechas en tablas globales; schema name unico y FK al banco. | **Adaptar** a `platform.tenant` con UUID, codigo, nombre del grupo, `SchemaName` y estado de provision. No reutilizar el modelo bancario. | EF Core/Npgsql. |
| Alta global y provision | `src\iMatch.Web\Services\EntityAdministrationService.cs`; `Components\Pages\AdminEntities.razor` | Guarda la entidad global dentro de una transaccion; despues del commit llama a `TenantSchemaService.EnsureTenantAsync`. | **Adaptar** a un servicio `TenantProvisioningService`. Registrar estado `Provisioning/Active/Failed` y permitir reintento si la migracion tenant falla. | DbContext, `TenantSchemaService`, actividad. |
| Creacion de esquema tenant | `src\iMatch.Web\Services\TenantSchemaService.cs` | Normaliza el codigo, crea `tenant_<codigo>`, registra `TenantAccount` y ejecuta SQL `CREATE TABLE IF NOT EXISTS`. `EnsureAllAsync` repara todos al iniciar. | **Reutilizar el patron, no el SQL**. Conservar validacion/citado de identificadores y reintento; reemplazar las tablas manuales por `TenantDbContext.Database.MigrateAsync()` y una historia EF dentro de cada esquema. | EF Core, ADO.NET, PostgreSQL. |
| Resolucion del tenant activo | `src\iMatch.Web\Services\TenantAccessService.cs` (`TenantAccessService`, `TenantAuthorizationService`) | Obtiene el usuario autenticado, roles, `TenantBankCode` y estado; para endpoints recibe `ClaimsPrincipal` y devuelve el filtro permitido. | **Adaptar** a `CurrentTenant` con `TenantId` y `SchemaName` obtenidos del registro global. El schema nunca debe aceptarse desde formulario, URL o cabecera sin resolverlo contra permisos. | AuthenticationStateProvider, UserManager, EF Core. |
| Aislamiento de datos | `TenantAccessService.cs`, servicios y endpoints de iMatch | El acceso global esta en tablas comunes y muchas consultas operativas aplican manualmente `TenantBankCode`. Algunas tablas especificas viven en esquemas tenant. | **No copiar los filtros manuales**. HR ProTools alojara todos los datos del grupo dentro de su schema; un `TenantDbContext` resuelto por usuario reduce el riesgo de omitir filtros. | PostgreSQL schemas, EF Core. |
| Migraciones globales | `Program.cs`, `ApplicationDbContext.cs`, carpeta `Data\Migrations` | `Database.MigrateAsync()` opcional; historial en `auth.__ef_migrations_history`. Luego ejecuta `EnsureAllAsync`. | **Reutilizacion directa para global**. Usar migraciones EF para `auth` y `platform`. | EF Core migrations. |
| Migraciones por tenant | No hay una implementacion versionada. `TenantSchemaService.EnsureSchemaObjectsAsync` usa DDL idempotente. | Crea cuatro tablas con SQL; no registra versiones por schema. | **Desarrollar** un migrador tenant versionado. Es el principal hueco tecnico respecto del requisito de HR ProTools. | EF Core/Npgsql; cache del modelo por schema. |
| Invitacion de usuarios | `Components\Pages\AdminUsers.razor`, `TenantUsers.razor` | Crea usuario sin contraseña, asigna rol, genera token de restablecimiento, codifica Base64Url y envia enlace a `Account/ResetPassword`. | **Adaptar**. Reutilizar token y pagina de establecimiento de clave; usar una ruta/plantilla de invitacion y confirmar el email solo al aceptar. Agregar compensacion si falla rol o correo. | Identity token providers, WebEncoders, NavigationManager, SMTP. |
| Validacion de tokens | `Components\Account\Pages\ResetPassword.razor`, `ConfirmEmail.razor`, `ResendEmailConfirmation.razor`; `.AddDefaultTokenProviders()` en `Program.cs` | Identity valida tokens protegidos por Data Protection y security stamp. | **Reutilizacion directa** con textos y flujo ajustados. Configurar explicitamente la vida util del token de invitacion. | Identity, Data Protection. |
| Restriccion por dominio de email | `src\iMatch.Web\Services\TenantDomainRules.cs` | Normaliza IDN, valida el dominio exacto y evita que un subdominio pase por coincidencia parcial. | **Opcional, no requerido en Fase 2**. Puede reutilizarse si el producto adopta dominios autorizados. | `IdnMapping`, `MailAddress`. |
| SMTP | `Data\SmtpConfiguration.cs`, `Services\SmtpSettingsService.cs`, `ApplicationEmailSender.cs`, `Components\Pages\AdminSmtp.razor` | Guarda configuracion global; protege la contraseña con Data Protection; envia HTML y adjuntos mediante MailKit; soporta StartTLS/SSL y prueba de envio. | **Reutilizacion casi directa** cambiando marca, protector (`HRProTools.SmtpPassword.v1`) y textos. | MailKit, MimeKit, EF Core, Data Protection. |
| Data Protection | `Program.cs` | Persiste claves en filesystem y fija `SetApplicationName("iMatch")`. | **Reutilizacion directa con adaptacion** a `HRProTools` y ruta persistente fuera de la release. Las claves y DB deben respaldarse juntas. | ASP.NET Core Data Protection. |
| Sesiones | `Program.cs`; no existen `AddSession` ni `UseSession` | Usa cookie Identity y circuito Blazor; no depende de ASP.NET Session ni Redis. | **Reutilizar el enfoque**. No agregar Redis ni estado de sesion servidor. | Cookies Identity, Blazor Server. |
| Registro basico de actividad | `Services\OperatorActivityService.cs`, modelo `OperatorActivity` | Registra actor, accion, asunto, detalle y tenant en una tabla operativa. | **Adaptar con alcance reducido** a acciones administrativas relevantes; no copiar el dominio bancario ni construir auditoria exhaustiva. | EF Core. |
| Despliegue nativo | `docs\DEPLOY_VPS_SENZA_DOCKER.md`, `deploy\*.service` | Documenta .NET 10, PostgreSQL 18, systemd, Nginx, claves persistentes, backups y health check; la guia usa Ubuntu 24.04. | **Reutilizar como plantilla** y adaptar a Ubuntu 26.04, un solo proceso web inicial, nombres/rutas HR ProTools y ClamAV cuando haya cargas. | Linux, systemd, Nginx, PostgreSQL. |
| Pruebas reutilizables como patron | `tests\iMatch.Tests\Security\TenantBoundaryTests.cs`, `TenantSchemaServiceTests.cs`, `SmtpSettingsServiceTests.cs`, `RoleBoundaryTests.cs` | Cubren nombres de schema, limites tenant, estado activo, proteccion SMTP y roles. | **Adaptar casos**, no copiar datos bancarios. Agregar prueba PostgreSQL real para comprobar schemas y aislamiento. | xUnit, EF Core; algunas pruebas usan proveedor en memoria. |

## Organizacion de servicios y dependencias

iMatch tiene cinco proyectos, pero la funcionalidad reutilizable de esta fase esta casi toda en `iMatch.Web`; `iMatch.Infrastructure` solo contiene un `Class1.cs` vacio. Replicar todas las capas no aporta valor al MVP.

Propuesta minima para Fase 2:

- `HRProTools.Web`: Blazor, componentes, Identity, EF Core, servicios de tenant, SMTP y configuracion.
- `HRProTools.Tests`: pruebas de reglas y pruebas de integracion PostgreSQL esenciales.
- Carpetas internas claras: `Data`, `Services`, `Security`, `Components`, `Features`.
- Extraer otro proyecto solo cuando un modulo tenga una dependencia o ciclo de vida realmente independiente.

Paquetes iniciales necesarios: Identity EF Core, EF Core Design/Tools, Npgsql EF Core y MailKit. ClosedXML se incorpora en Fase 3; PDF y `tusdotnet` no son necesarios para el incremento inicial.

## Analisis del CSV legacy BPM

El CSV permite verificar nombres de tablas y columnas, no semantica completa. No se deducen claves, tipos, cardinalidades ni relaciones.

### Conceptos utiles

| Dominio | Tablas/columnas observadas | Concepto aprovechable | Tratamiento propuesto |
|---|---|---|---|
| Persona/funcionario | `bd_empleado`: `id_empleado`, nombre, apellido, `nro_documento`, direccion, telefonos, email, estado civil, estudios, `id_cargo`; `bd_hijos` | Datos personales y contactos; dependientes como modulo futuro. | Crear `person` separada del vinculo laboral. No copiar IDs ni el agregado plano. |
| Empresa | `empresa`: `id`, `razon_social`, `ruc`, contacto, direccion, `activo`, fechas | Identidad legal y estado de la empresa. | Crear `company` dentro del schema del grupo. Campos BPM/proveedor/centro de costo quedan fuera hasta aclaracion. |
| Departamento | `departamento`: `id`, `nombre`, `paisid`; junto con `distrito`, `ciudad`, `barrio` | Es una division geografica, no hay evidencia de que sea un departamento organizacional. | Desarrollar `department` desde cero. No reutilizar esta tabla como estructura interna. |
| Cargo | `cargo`, `bd_cargo`, `cc_cargo` (esta ultima incluye `nivel`) | Catalogo de cargos y posible nivel. Hay tres fuentes ambiguas. | Crear un solo `position` por empresa; no mezclar catalogos sin una regla de importacion validada. |
| Vinculo laboral/movimientos | `cc_movimiento`: `nro_legajo`, tipo, fechas, sueldo, contrato, estado laboral, cargo, desvinculacion, reingreso y otros; `cc_estadolaboral`, `cc_estmovimiento`, `cc_tipomovimiento`, `cc_tipocontrato` | Legajo, altas/bajas, vigencia, contrato y cambios laborales. | Separar `employment` de `employment_assignment`. Cada reingreso/traslado de empresa crea un nuevo legajo. |
| Salario | `cc_movimiento.sueldo`, `cc_tiposalario`; `cc_var_mov`: monto, desde/hasta, legajo | Monto, tipo y vigencia. | Reservar un historial salarial efectivo por fechas para un modulo futuro. No copiar salario dentro de una tabla de movimientos general. |
| Vacaciones | `cc_vacaciones`, `cc_vac_parc`, `cc_vac_parc_reg`, `cc_vacparametros`, `cc_desv_vac`, `cc_feriado` | Periodos, dias correspondientes/usados, fraccionamiento, parametros por antiguedad y feriados. | Modelar periodos y un libro de movimientos inmutable. No usar un saldo mutable ni copiar los registros de proceso. |
| Permisos | `per_permisos`: fechas, dias, retorno, motivo, observaciones | Solicitud/periodo de permiso. | Crear solicitudes tipadas en Fase 4. La tabla no contiene legajo verificable, por lo que no puede importarse sin resolver el `nro_proceso`. |
| Ausencias/reposos | `cc_ausencias`, `cc_tipoausencia`, `cc_estadoausencia`, `rep_reposo`, `cc_tip_reposo`, `cc_motivo_injustificado`; `sup_embarazo` | Fechas, retorno, tipo, motivo, reposo IPS, justificacion. | Crear ausencias y documentos asociados en Fase 5; mantener la referencia al vinculo laboral. |
| Desvinculacion | `cc_desvinculacion`, `cc_tipodesv`, `cc_motivoren`, `leg_motdesv` | Motivo, conceptos y liquidacion de salida. | En el MVP solo cerrar el vinculo con fecha/motivo. Liquidacion queda para futuro. |

### Elementos propios del BPM/XML que no se reproducen

- `nro_proceso`, `id_proceso`, `status`, `control`, firmas y estados de circuito embebidos en tablas de dominio.
- Referencias repetidas a `id_servicio` sin una tabla de servicio ni relacion verificable en el CSV.
- Usuarios legacy `bd_usuarios`, especialmente `pass`; no se importan contraseñas.
- Tablas de acreditacion, firma, busqueda, evaluacion y apercibimientos que no son necesarias para el MVP inmediato.
- Tablas anchas que mezclan alta, salario, equipamiento, horario, desvinculacion y reingreso (`cc_movimiento`).
- Duplicacion de catalogos (`cargo`, `bd_cargo`, `cc_cargo`) y estados genericos por proceso.

### Informacion que el CSV no permite resolver

- No hay relacion verificable entre `bd_empleado.id_empleado` y `cc_movimiento.nro_legajo`.
- `empresa.nro_legajo` es ambiguo y no demuestra que la empresa pertenezca a un funcionario.
- No existe una tabla identificable de departamentos organizacionales.
- No se puede determinar que catalogo de cargos es la fuente vigente.
- No se pueden verificar reservas provisionales de vacaciones, reglas de consumo, constraints ni transacciones.
- No se conocen tipos, nulabilidad, claves primarias/foraneas, indices ni volumen de datos.

Estas incertidumbres no bloquean el desarrollo de Fase 2; si se importa informacion legacy mas adelante, exigiran una muestra de datos y un diccionario de correspondencias aprobado.

## Que debe desarrollarse desde cero

- Entidades HR: grupo, empresa, departamento, cargo, persona, vinculo laboral y asignacion historica.
- `TenantDbContext` dinamico y migraciones EF versionadas por schema.
- Provision transaccional/reintentable con estado global del tenant.
- Formularios y casos de uso HR de Fase 2.
- Libro de movimientos de vacaciones, politicas efectivas e importador Excel (Fase 3).
- Circuito configurable de aprobaciones y alcances por empresa/departamento (Fase 4).
- Ausencias, alertas, reportes y escaneo ClamAV (Fases 3/5 segun el tipo de archivo).

## Problemas tecnicos reales

1. `C:\HRProTools` no contiene `.git`; antes de programar debe convertirse en el checkout del repositorio oficial sin perder `Agent.md`, `base.csv`, logos, mockup ni estos documentos.
2. iMatch no tiene migraciones versionadas por tenant: su DDL manual no satisface el mecanismo requerido para evolucionar el modelo HR.
3. `EntityAdministrationService` confirma la entidad global antes de crear el schema. Un fallo posterior puede dejar un tenant incompleto; HR ProTools necesita estado de provision y reintento seguro.
4. La invitacion de iMatch marca `EmailConfirmed = true` antes de que el destinatario acepte y no compensa fallos entre crear usuario, asignar rol y enviar correo. Debe adaptarse.
5. El nombre `tenant_<codigo>` se trunca a 63 caracteres y podria colisionar. Usar un nombre derivado del UUID completo evita esa ambiguedad.
6. El aislamiento operativo de iMatch depende en varios casos de filtros manuales. HR ProTools debe resolver un `TenantDbContext` hacia un solo schema y evitar consultas multi-tenant accidentales.
7. iMatch no contiene integracion ClamAV ni dominio HR reutilizable.
8. No se comprobo una instancia PostgreSQL 18 ni credenciales SMTP para HR ProTools; no impiden escribir el codigo, pero son necesarias para aceptar Fase 2.
