# Estado del proyecto HR ProTools

**Fecha:** 17/11/2026
**Fase actual:** Fase 2/5 - primera aplicacion funcional
**Estado:** Completada y verificada; Fase 3 no iniciada.

## Implementado en Fase 2

- Solucion .NET 10 con Blazor Web App en modo Interactive Server y proyecto xUnit.
- PostgreSQL 18 real con migraciones EF separadas para el catalogo global y cada schema tenant.
- ASP.NET Core Identity, roles globales/tenant, bloqueo de usuarios inactivos y sesion revalidada.
- Administracion global de grupos empresariales, provisioning de schema, primera empresa y administrador tenant.
- Invitaciones de 24 horas mediante token Identity; envio SMTP configurable y enlace manual cuando SMTP no esta configurado.
- Configuracion SMTP protegida con Data Protection; secretos locales fuera del repositorio.
- Organizacion tenant: empresas, departamentos y cargos con altas, edicion y activacion/desactivacion controlada.
- Personas y vinculos laborales con legajo unico, asignaciones historicas, historial salarial y cierre de relacion laboral.
- Interfaz responsive basada en la identidad visual proporcionada.
- Inicializacion del repositorio Git `main` y remoto oficial `https://github.com/Orloch314/HRProTools`.

## Verificacion realizada

- `dotnet build`: correcta, 0 advertencias y 0 errores.
- `dotnet test`: 3 pruebas superadas, 0 fallidas, 0 omitidas.
- Prueba de integracion PostgreSQL: crea y migra dos schemas temporales, inserta datos distintos, verifica aislamiento y elimina solamente esos schemas.
- Prueba de integridad laboral: confirma legajo unico y preservacion del historial organizativo y salarial con una sola vigencia abierta.
- Prueba funcional en navegador: login global, formulario interactivo y provisioning completo del grupo sintetico `QA27`.
- Verificacion directa de PostgreSQL: schemas `auth`, `platform` y `hr_t_6e24549785b64c81bbcac3472cbb98c4`; tenant `QA27` activo y empresa inicial `EMPQ2` presente.
- No se envio correo real durante las pruebas.

## Configuracion local

- La cadena PostgreSQL y las credenciales bootstrap estan en User Secrets del proyecto Web; no se versionan.
- Base local: `hrprotools`, independiente de las otras bases del contenedor PostgreSQL 18 existente.
- Para ejecutar: `dotnet run --project src/HRProTools.Web`.
- Para habilitar correo real, un administrador global debe completar `/admin/smtp` con un servidor SMTP de prueba valido.
- El tenant sintetico `QA27` y su usuario invitado permanecen en la base local como evidencia reproducible de la prueba funcional.

## Pendientes y bloqueos reales

1. Probar el envio y la recepcion de una invitacion real cuando se proporcionen credenciales SMTP de prueba.
2. Sustituir las credenciales bootstrap locales antes de cualquier ambiente compartido o publicado.
3. Antes de Fase 3 debe aprobarse la especificacion exacta del calculo legal paraguayo y el formato de importacion Excel.

## Proxima actividad autorizada

Ninguna implementacion de Fase 3 fue iniciada. El siguiente trabajo requiere autorizacion explicita y las definiciones legales indicadas arriba.
