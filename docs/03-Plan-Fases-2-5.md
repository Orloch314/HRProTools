# HR ProTools - Plan de fases 2 a 5

## Principios de ejecucion

- Entregar incrementos verticales verificables desde navegador y PostgreSQL.
- Reutilizar los componentes Identity/tenant/SMTP identificados en iMatch con las adaptaciones documentadas.
- No incorporar microservicios, Docker, Redis, Kubernetes ni un motor BPM.
- Mantener un solo proceso Blazor Web durante el MVP, salvo necesidad demostrada.
- Priorizar una aplicacion navegable y funcional desde el primer incremento.
- No iniciar una fase siguiente sin aceptar la anterior y actualizar `ESTADO_PROYECTO.md`.

## Fase 2 - Aplicacion funcional y gestion basica del grupo

### Funcionalidades

1. Preparar el checkout Git oficial y crear la solucion .NET 10 Blazor Web App.
2. Configurar PostgreSQL, migraciones globales, Identity, roles y administrador global bootstrap.
3. Login/logout, bloqueo basico y revalidacion de usuario/tenant activo.
4. Crear grupo empresarial, registro global, schema y migraciones tenant.
5. Crear la primera empresa y permitir nuevas empresas dentro del grupo.
6. Configurar SMTP e invitar al primer `TenantAdministrator` mediante token Identity.
7. CRUD con baja logica de departamentos y cargos.
8. Alta de persona + vinculo + asignacion inicial; listado, consulta y edicion de datos basicos.
9. Cierre de vinculo y cambios de departamento/cargo conservando historia minima.
10. Historial salarial basico con vigencia, sin calculo de nomina ni payroll.
11. Pruebas esenciales de aislamiento, unicidad de legajo y persistencia PostgreSQL real.

### Dependencias

- Checkout del repositorio oficial en `C:\HRProTools`.
- SDK .NET 10 y una instancia PostgreSQL 18 de desarrollo.
- Credenciales SMTP de prueba antes de aceptar la invitacion end-to-end.
- Logo y mockup existentes como referencia visual.
- Componentes iMatch en modo lectura.

### Orden de incrementos

| Incremento | Resultado navegable |
|---|---|
| 2.1 | Administrador global inicia sesion; migraciones globales aplicadas. |
| 2.2 | Crea grupo, schema y primera empresa; estado de provision visible y reintentable. |
| 2.3 | Invita y activa al primer administrador tenant por email. |
| 2.4 | Administrador tenant crea empresas, departamentos y cargos. |
| 2.5 | Registra, lista, consulta y edita funcionarios/legajos. |

### Criterios minimos de aceptacion

- La aplicacion abre desde navegador y un administrador global autenticado crea un grupo.
- PostgreSQL contiene el registro global, un schema distinto y su historial de migraciones.
- Un tenant no puede consultar ni modificar datos de otro, probado con dos schemas.
- La primera empresa se persiste dentro del schema correcto.
- El enlace de invitacion valida token, permite establecer contraseña y no es reutilizable despues de aceptarlo.
- Departamento, cargo y funcionario se crean/editan desde formularios y sobreviven al reinicio.
- Existe constraint unico del legajo en todo el grupo.
- Un traslado de empresa cierra el vinculo anterior y exige uno nuevo con otro legajo.
- `dotnet build` y pruebas esenciales pasan; no hay secretos versionados.

## Fase 3 - Vacaciones, saldos, politicas e importacion Excel

### Funcionalidades

1. Vincular cuentas Identity con persona y vinculo activo, invitar/reactivar/desactivar y asignar roles multiples.
2. Autoservicio Employee basico con datos propios, vinculo, saldo de vacaciones y cambio de contraseña.
3. Especificar y validar las reglas legales paraguayas antes de codificar sus tramos.
4. Jornadas con vigencia y feriados empresariales, separados del computo legal de vacaciones.
5. Politicas versionadas por fecha efectiva, incluyendo dias adicionales sobre los mismos tramos de antiguedad.
6. Generacion de periodos por vinculo laboral sin recalculo retroactivo por cambios de politica.
7. Libro de movimientos de vacaciones, saldos y servicio FIFO transaccional preparado para Fase 4.
8. Importacion Excel de funcionarios y periodos anteriores con validacion previa, ClamAV y confirmacion transaccional.
9. Plantilla de dos hojas, vista de errores por fila y conservacion de multiples periodos por legajo.

### Dependencias

- Modelo de persona/vinculo estable de Fase 2.
- Regla legal y criterio de dias laborales aprobados por responsable funcional/legal.
- ClosedXML y servicio ClamAV disponible en desarrollo/prueba.
- Formato de Excel aprobado y datos legacy de muestra si se realizara migracion.

### Criterios minimos de aceptacion

- Una politica nueva solo afecta periodos generados bajo su vigencia.
- Cada periodo importado conserva sus fechas, dias otorgados, consumidos/ajustados y origen.
- El saldo se deriva de movimientos; no se edita directamente.
- El importador no persiste filas si falla la validacion transaccional del lote.
- Un archivo no limpio o no escaneado no se procesa.
- Un funcionario invitado activa su cuenta Employee y solamente consulta sus propios datos.
- Las cuentas de funcionario no se duplican en reingresos de la misma persona.
- Se prueba al menos un funcionario nuevo, uno con periodos importados y un cambio de politica no retroactivo.

## Fase 4 - Permisos, solicitudes, aprobaciones y cancelaciones

### Funcionalidades

1. Autoservicio para solicitudes de vacaciones y permisos.
2. Circuito configurable por tenant; valor inicial Jefe -> Recursos Humanos.
3. Asignacion de autorizadores a departamentos de una o varias empresas del grupo.
4. Alcance de RR. HH. global al grupo o limitado por empresas.
5. Bandejas de pendientes, aprobacion/rechazo con comentario y notificaciones email.
6. Reserva provisional de vacaciones al presentar la solicitud.
7. Conversion de reserva a consumo en aprobacion final.
8. Liberacion de reserva en rechazo/cancelacion y reversion registrada cuando corresponda.
9. Cancelacion con reglas de estado y registro basico de operaciones.
10. Configuracion tenant para habilitar o deshabilitar la cancelacion de solicitudes.
11. Alcances de Recursos Humanos por empresa y autorizadores por departamento.
12. Permisos con/sin goce por dias u horas, con instantanea de jornada y control de superposiciones.
13. Outbox PostgreSQL para notificaciones de vacaciones y permisos, envio MailKit y reintentos controlados.

### Dependencias

- Saldos y libro de movimientos de Fase 3.
- Usuarios vinculados a funcionarios y roles `Employee`, `Approver`, `HumanResources` entregados en Fase 3.
- Configuracion de alcances por empresa/departamento.
- SMTP operativo.

### Criterios minimos de aceptacion

- El circuito predeterminado funciona de extremo a extremo y puede configurarse sin codigo.
- Un autorizador solo ve solicitudes de sus departamentos; RR. HH. respeta su alcance empresarial.
- Dos solicitudes concurrentes no pueden sobrerreservar el mismo saldo.
- Reserva, consumo, liberacion y reversion quedan como movimientos trazables.
- El consumo se aplica siempre al periodo disponible mas antiguo.
- Reintentos o doble clic no duplican aprobaciones ni movimientos.
- Un usuario normal solo accede a su autoservicio y sus solicitudes.
- Un permiso reutiliza el circuito sin crear movimientos en el ledger de vacaciones.
- Las notificaciones persisten aunque SMTP falle y una misma transicion no duplica eventos.

## Fase 5 - Ausencias, alertas, dashboard, reporte Excel y cierre funcional

### Funcionalidades

1. Registro de ausencias, reposos y justificaciones asociados al vinculo, sin reconstruir los permisos de Fase 4.
2. Adjuntos escaneados con ClamAV y acceso autorizado.
3. Alertas operativas: vacaciones por vencer, solicitudes pendientes y ausencias relevantes.
4. Dashboard coherente con el mockup, sin metricas no respaldadas por datos.
5. Un unico reporte Excel inicial con resumen de permisos concedidos, rechazados, pendientes y cancelados.
6. Filtros del reporte por empresa/departamento/periodo segun autorizacion.
7. Dashboard por rol con metricas reales y alcances de empresa/departamento.
8. Verificacion final de regresion, migraciones, aislamiento y health check local.
9. Actualizacion de documentacion, commit y push del cierre funcional del MVP.

### Dependencias

- Flujos y permisos aceptados en Fase 4.
- Definicion funcional de tipos de ausencia y alertas.
- PostgreSQL y ClamAV locales para pruebas de persistencia y seguridad.
- Plantilla/columnas de reportes aprobadas.

### Criterios minimos de aceptacion

- Ausencias y adjuntos se registran sin alterar historia del funcionario.
- El reporte Excel de permisos refleja filtros y no expone datos de empresas fuera del alcance.
- Alertas se generan a partir de reglas verificables, sin duplicados visibles.
- El dashboard respeta roles multiples sin ampliar alcances ni exponer salario o documentos medicos.
- Compilacion, pruebas esenciales, modelos EF y `/health` local finalizan correctamente.
- No hay secretos, documentos medicos reales ni datos personales de prueba incorporados al repositorio.

### Estado de cierre

- La implementacion funcional de la Fase 5 esta completada en el entorno local.
- El deployment fue excluido expresamente porque no existe aun el VPS de HR ProTools. Ubuntu, Nginx, systemd, DNS, HTTPS, backups remotos, actualizacion y rollback quedan para una actividad futura separada.
- El MVP requiere todavia aceptacion manual y publicacion futura; no se considera desplegado en produccion.

## Elementos expresamente posteriores al MVP

Liquidacion salarial, asistencia/relojes, reclutamiento, evaluacion de desempeno, otras localizaciones legales y carga de certificados/documentos desde el celular. El modelo mantiene IDs e historial suficientes para agregarlos, pero ninguna de estas funciones debe anticiparse en Fases 2 a 5.
