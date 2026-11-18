# HR ProTools - Modelo de datos provisional

## Decisiones de base

- Un tenant equivale a un grupo empresarial.
- Identity y el registro de tenants son globales; los datos HR viven exclusivamente en el schema del grupo.
- Nombre de schema propuesto: `hr_t_{TenantId:N}`. Un UUID sin guiones cabe holgadamente en el limite PostgreSQL de 63 caracteres y no depende del nombre comercial.
- No se acepta un nombre de schema enviado por el cliente. Se resuelve desde el usuario autenticado o desde una seleccion explicita autorizada para el administrador global.
- Un legajo identifica un **vinculo laboral**, no una persona. Es unico en todo el schema/grupo.
- Una persona puede tener varios vinculos historicos. Un reingreso o traslado a otra empresa crea un vinculo y un legajo nuevos.
- Empresas, departamentos, cargos y funcionarios/vinculos se desactivan o cierran; no se borran si tienen historia.

## Esquemas globales

### `auth`

Tablas estandar de ASP.NET Core Identity (`AspNetUsers`, roles, claims, tokens, logins). `ApplicationUser` agrega:

| Campo | Tipo orientativo | Regla |
|---|---|---|
| `DisplayName` | varchar(160) | Requerido. |
| `TenantId` | uuid nullable | `NULL` para administrador global; FK restrictiva a `platform.tenant`. |
| `IsActive` | boolean | `true` por defecto; usuario inactivo no inicia ni conserva sesion. |
| `InvitationSentAtUtc` | timestamptz nullable | Registro operativo basico. |
| `InvitationAcceptedAtUtc` | timestamptz nullable | Se fija al establecer la contraseña y confirmar el correo. |
| `CreatedAtUtc` | timestamptz | Requerido. |

Indices: `TenantId`; email normalizado unico segun Identity. No se habilita registro publico.

Roles previstos:

| Rol | Alcance |
|---|---|
| `GlobalAdministrator` | Registro/provision de grupos e invitacion del primer administrador. |
| `TenantAdministrator` | Configuracion del grupo y sus empresas. |
| `HumanResources` | Todo el grupo o empresas asignadas; se implementa en Fase 4. |
| `Approver` | Departamentos asignados, incluso de empresas distintas; Fase 4. |
| `Employee` | Autoservicio propio; Fase 4. |

En Fase 2 solo se necesitan `GlobalAdministrator` y `TenantAdministrator`.

### `platform`

#### `tenant`

| Campo | Tipo orientativo | Regla |
|---|---|---|
| `Id` | uuid | PK. |
| `Code` | varchar(32) | Requerido, unico, estable. |
| `Name` | varchar(200) | Nombre del grupo empresarial. |
| `SchemaName` | varchar(63) | Requerido, unico, generado por el servidor. |
| `Status` | varchar(16) | `Provisioning`, `Active`, `Inactive` o `Failed`. |
| `ProvisioningError` | varchar(1000) nullable | Mensaje controlado para diagnostico/reintento; sin secretos. |
| `CreatedAtUtc`, `UpdatedAtUtc` | timestamptz | Requeridos. |

No se elimina un tenant. La desactivacion impide acceso y conserva el schema.

#### `smtp_configuration`

Configuracion singleton global equivalente a iMatch: host, puerto, modo TLS, usuario, contraseña protegida, remitente y datos de actualizacion. Constraint `Id = 1`. La contraseña se protege con Data Protection, no se guarda en claro.

#### `operation_log`

Registro global minimo: `Id`, `OccurredAtUtc`, `ActorUserId`, `TenantId` nullable, `Action`, `Subject`, `Detail`. Solo para altas, bajas logicas, provision, invitaciones y cambios relevantes; no es una auditoria completa de cada campo.

## Schema de cada tenant

Las siguientes entidades son el minimo para comenzar Fase 2.

### `company`

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `Code` varchar(32) | Unico dentro del grupo. |
| `LegalName` varchar(200) | Requerido. |
| `TradeName` varchar(200) nullable | Opcional. |
| `TaxId` varchar(32) | RUC/identificador; unico dentro del grupo. |
| `Email`, `Phone`, `Address` | Datos basicos opcionales. |
| `IsActive`, `DeactivatedAtUtc` | Baja logica. |
| `CreatedAtUtc`, `UpdatedAtUtc` | Trazabilidad basica. |

### `department`

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `CompanyId` uuid | FK restrictiva a `company`. |
| `Code` varchar(32), `Name` varchar(160) | Unique (`CompanyId`, `Code`). |
| `IsActive`, `DeactivatedAtUtc` | Baja logica. |
| Fechas de creacion/actualizacion | Requeridas. |

No se reutiliza la tabla legacy `departamento`, que corresponde a geografia.

### `position`

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `CompanyId` uuid | FK restrictiva a `company`. |
| `Code` varchar(32), `Name` varchar(160) | Unique (`CompanyId`, `Code`). |
| `Level` varchar(80) nullable | Preparado para clasificacion sin imponer un catalogo legacy. |
| `IsActive`, `DeactivatedAtUtc` | Baja logica. |
| Fechas de creacion/actualizacion | Requeridas. |

### `person`

Representa a la persona, independientemente de sus contrataciones.

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `DocumentType`, `DocumentNumber`, `NormalizedDocumentNumber` | Unique (`DocumentType`, `NormalizedDocumentNumber`) dentro del grupo. |
| `GivenNames`, `FamilyNames` | Requeridos. |
| `BirthDate` date nullable | Fecha civil, no UTC. |
| `Email`, `Phone`, `Address` | Contacto basico. |
| `CreatedAtUtc`, `UpdatedAtUtc` | Requeridos. |

### `employment`

Representa el funcionario/legajo y su relacion con una empresa.

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `EmployeeNumber` varchar(32) | Unique en todo el schema/grupo. Nunca se reutiliza. |
| `PersonId` uuid | FK restrictiva a `person`. |
| `CompanyId` uuid | FK restrictiva a `company`; no cambia durante el vinculo. |
| `StartDate` date | Requerida. |
| `EndDate` date nullable | Check: nula o mayor/igual a inicio. |
| `Status` varchar(16) | `Active` o `Closed` para Fase 2. |
| `ContractType` varchar(80) nullable | Dato inicial, sin catalogo complejo. |
| `ClosureReason` varchar(200) nullable | Obligatorio al cerrar. |
| `CreatedAtUtc`, `UpdatedAtUtc` | Requeridos. |

No se impone que una persona tenga un unico vinculo historico. El servicio de aplicacion debe impedir editar `CompanyId` y `EmployeeNumber` despues del alta.

### `employment_assignment`

Conserva cambios de departamento y cargo sin sobrescribir historia.

| Campo principal | Regla |
|---|---|
| `Id` uuid PK | Identificador estable. |
| `EmploymentId`, `CompanyId` | FK compuesta al vinculo. |
| `DepartmentId`, `CompanyId` | FK compuesta; garantiza que el departamento pertenece a la misma empresa. |
| `PositionId`, `CompanyId` | FK compuesta; garantiza que el cargo pertenece a la misma empresa. |
| `ValidFrom` date, `ValidTo` date nullable | Check de rango. |
| `Reason` varchar(200) nullable | Motivo basico del cambio. |
| `CreatedAtUtc`, `CreatedByUserId` | Registro minimo. |

Crear indices unicos auxiliares (`Id`, `CompanyId`) en las tres tablas referenciadas y un indice unico parcial por `EmploymentId WHERE ValidTo IS NULL`, para permitir una sola asignacion vigente. El cambio se ejecuta en una transaccion: cerrar asignacion actual y abrir la nueva.

### `operation_log`

Registro tenant minimo con actor global (`UserId`), fecha, accion, tipo/id de entidad y detalle breve. Se usa en altas, ediciones relevantes, cierres y desactivaciones.

## Relaciones principales

```text
platform.tenant 1 --- N auth.ApplicationUser

tenant schema:
company 1 --- N department
company 1 --- N position
person  1 --- N employment N --- 1 company
employment 1 --- N employment_assignment
department 1 --- N employment_assignment N --- 1 position
```

`TenantId` no se repite en las tablas del schema tenant: el schema ya constituye la frontera de aislamiento.

## Migraciones y provision del schema

Dos contextos:

1. `GlobalDbContext`: Identity en `auth` y registro/configuracion en `platform`; historia global fija.
2. `TenantDbContext`: entidades HR; `HasDefaultSchema(CurrentTenant.SchemaName)` y una historia `__ef_migrations_history` dentro del mismo schema.

Como el modelo EF depende del schema, implementar un `IModelCacheKeyFactory` que incluya `SchemaName`. El nombre se obtiene solo del registro global validado.

Secuencia de provision:

1. Crear `platform.tenant` con estado `Provisioning` y `SchemaName` generado.
2. Crear el schema con identificador validado/citado.
3. Construir `TenantDbContext` para ese schema y ejecutar `Database.MigrateAsync()`.
4. Crear la primera `company` en el mismo flujo funcional.
5. Marcar tenant `Active`.
6. Si falla, marcar `Failed`, registrar causa controlada y permitir reintento idempotente. No ocultar un tenant parcialmente provisionado como activo.

Las migraciones se ejecutan en provision/despliegue, no en cada request.

## Autorizacion preparada para fases futuras

- El usuario siempre pertenece como maximo a un tenant; el administrador global tiene `TenantId = NULL`.
- En Fase 4, `HumanResources` tendra una asignacion de alcance de grupo o una lista de `CompanyId`.
- En Fase 4, `Approver` tendra asignaciones a `DepartmentId`; las IDs estables permiten departamentos de distintas empresas del mismo grupo.
- El empleado se vinculara a su `EmploymentId` activo para autoservicio. Ese enlace no es necesario para el CRUD administrativo de Fase 2.
- El circuito por defecto sera Jefe -> Recursos Humanos, almacenado como configuracion sencilla; no se implementara un motor BPM.

## Convenciones para modulos futuros

- Fechas laborales (`StartDate`, periodos, solicitudes) usan `date`; instantes tecnicos/auditoria usan `timestamptz` en UTC.
- Importes futuros usan `numeric` y codigo de moneda, no `float`.
- Catalogos y maestros se desactivan; los movimientos/solicitudes no se borran.
- Salarios futuros se modelan como historial con vigencia, no como un campo mutable en persona.
- Politicas de vacaciones llevaran `EffectiveFrom`; una version nueva no modifica periodos ya generados.
- Vacaciones usaran `vacation_period` y un libro inmutable `vacation_movement` con tipos como credito, reserva, liberacion, consumo, reversion y ajuste de importacion. El saldo sera la suma de movimientos.
- El consumo se asignara al periodo mas antiguo en una transaccion. Una solicitud reserva; aprobacion final convierte la reserva; rechazo/cancelacion libera o revierte con nuevos movimientos.
- Archivos importados guardaran metadatos, hash y estado de escaneo ClamAV antes de ser procesados.
- Nomina, asistencia, reclutamiento y evaluacion agregaran tablas propias referenciando `person`/`employment`; no alteraran la identidad del legajo.

## Fuera del modelo inicial de Fase 2

No se crean aun tablas de vacaciones, permisos, ausencias, aprobaciones, salarios, asistencia, reclutamiento, evaluacion, adjuntos ni reportes. Las convenciones anteriores evitan bloquearlas sin anticipar su implementacion.
