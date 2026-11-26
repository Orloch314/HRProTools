# HR ProTools — Auditoría previa y plan de producción

**Fecha:** 26/11/2026, inspección remota aproximadamente 16:45–16:50 America/Asuncion (19:45–19:50 UTC).

**Estado:** Fase 1 terminada; preparación y validación local posteriormente autorizadas y completadas. **NO se realizó deployment ni se autorizaron modificaciones remotas en esta etapa.**

## 1. Alcance y método

Destino autorizado: `root@95.217.134.178`, clave local indicada por el usuario. SSH con `BatchMode=yes` y `StrictHostKeyChecking=yes`; no se copiaron claves ni se cambió SSH. Se consultaron recursos, servicios, sockets, configuraciones seleccionadas, certificados públicos, catálogos PostgreSQL y metadatos de backups. No se consultaron tablas de negocio ni se abrieron/exportaron dumps de otras aplicaciones.

No se instalaron paquetes, crearon usuarios/bases/directorios remotos, emitieron certificados, modificaron DNS, cambiaron configuraciones, reiniciaron/recargaron servicios ni enviaron mensajes. La única escritura de esta tarea es la documentación local. Las conexiones SSH/HTTP/SMTP de diagnóstico pueden dejar los registros operativos normales del servidor.

Commit aplicativo inspeccionado: `7c878a8969974a984ad293d4c3eccd83d186423c`, rama `codex/phase5-mvp-completion`. El sitio `site/` y dos mockups siguen sin seguimiento Git; no deben confundirse con un artefacto del commit publicado.

## 2. Sistema y capacidad

| Dato | Resultado observado |
|---|---|
| Host / sistema | `Eqmatik`, Ubuntu **26.04 LTS**, x86_64 |
| CPU | 2 vCPU Intel Xeon virtualizadas, carga 0.14/0.21/0.23 |
| RAM | 3.7 GiB total, 2.0 GiB usada, **1.7 GiB disponible**; libre sin caché 166 MiB |
| Swap | 2 GiB, aproximadamente 341 MiB usada |
| Disco raíz | `/dev/sda1`, ext4, 38 GiB, 23 GiB usados, **14 GiB disponibles**, 62% |
| Otros montajes | EFI 253 MiB; `/run`, `/tmp`, `/dev/shm` en tmpfs; sin volumen aplicativo independiente observado |
| Uptime | 148 días; existe `/run/reboot-required`, sin actuar sobre él |
| Runtime compartido | ASP.NET Core/.NET **8.0.28**; **no se encontró .NET 10** compartido |
| Nginx | **1.31.2**, activo, configuración válida en la inspección |
| PostgreSQL | **18.6**, dos clusters activos |
| ClamAV | **1.5.4**, daemon/freshclam activos; daemon aproximadamente 955 MiB |

Otros consumos aproximados: EQ2 142 MiB, Eqmatchpy 138 MiB, Tributa 202 MiB, Eqpair Beta 25 MiB, Nginx 15 MiB. Son instantáneas, no máximos ni una prueba de capacidad concurrente.

**Valoración:** viable para una puesta en marcha controlada reutilizando PostgreSQL/ClamAV existentes y publicando Astro estático. No se puede garantizar capacidad para una cantidad no especificada de usuarios Blazor. Propuesta inicial para el servicio HR: `MemoryHigh=384M`, `MemoryMax=768M`, `CPUQuota=100%` de una CPU, con observación de memoria/latencia y revisión antes de ampliar carga. No instalar otro antivirus, Node permanente ni Docker. No actualizar SO ni runtime compartido de otras aplicaciones.

## 3. Servicios y sitios que deben preservarse

| Servicio | Estado / puerto observado | Baseline HTTPS desde el VPS, SNI correcto |
|---|---|---|
| `nginx.service` | Activo; 80/443 públicos IPv4/IPv6 | Maestro PID 2616305 |
| `eq2.service` | Activo, `127.0.0.1:5000`, usuario `eq2` | `app.eqpair.com`: 302 |
| `eqpair-beta.service` | Activo, 5001 en loopback IPv4/IPv6, usuario `www-data` | Sin vhost activo asociado observado; no alterar |
| `eqmatchpy.service` | Activo, `127.0.0.1:5087`, usuario `eqmatchpy` | `eqmatchpy.eqmatik.com`: 302 |
| `tributa.service` | Activo, `127.0.0.1:5090`, usuario `tributa` | `tributa.eqmatik.com`: 302 |
| `postgresql@18-main` | Activo, 5432 solo loopback | Sin exposición pública |
| `postgresql@18-eqmatchpy` | Activo, 5433 solo loopback | Sin exposición pública |
| `clamav-daemon`, `clamav-freshclam` | Activos, socket Unix | PING devuelve `PONG` |
| `eqmatch.service` | **Anomalía previa:** `activating/auto-restart`, `Result=core-dump`, salida 6, más de 1 millón de reinicios acumulados | Usuario root en su configuración; no se cambió |

Sitios estáticos existentes: `eqmatik.com` y `www.eqmatik.com`: 200; `eqpair.com` y `www.eqpair.com`: 200. Las respuestas 302 son respuestas iniciales de la aplicación, no una prueba autenticada. Los nombres heredados `eqmatch.eqmatik.com` y `eqpair.eqmatik.com` devolvieron HTTP 200 en el servidor, pero no aparecen en los vhosts realmente cargados: ese 200 puede ser el sitio por defecto, no evidencia de salud de la aplicación heredada.

El certificado almacenado de `eqmatch.eqmatik.com` venció el **29/08/2026**; no se observó usado en la configuración Nginx cargada. Los certificados activos observados de los demás sitios siguen vigentes: Eqmatik hasta 27/12/2026, App Eqpair hasta 27/01/2027, Eqpair hasta 28/01/2027, Eqmatchpy hasta 16/02/2027 y Tributa hasta 21/01/2027. No se intentó reparar ni renovar certificados ajenos.

UFW activo: entrada denegada por defecto, salida permitida, entradas explícitas **22/80/443** para IPv4/IPv6. `fail2ban`, cron y timers existentes preservados. Ninguna regla adicional resulta necesaria para HR si escucha en loopback.

## 4. DNS y Nginx

Comprobado con **1.1.1.1 y 8.8.8.8**, sin discrepancias entre ambos:

| Nombre | A / CNAME | AAAA |
|---|---|---|
| `hrprotools.com` | A `95.217.134.178` | Sin registro AAAA publicado observado |
| `app.hrprotools.com` | A `95.217.134.178` | Sin registro AAAA publicado observado |
| `www.hrprotools.com` | CNAME `hrprotools.com`, termina en el mismo IPv4 | Sin AAAA final observado |

NS: `dns.technorail.com`, `dns2.technorail.com`, `dns3.arubadns.net`, `dns4.arubadns.cz`. **No hace falta reconfigurar los registros web en Aruba.** `www` puede incluirse en el certificado del sitio y redirigirse al dominio principal.

Nginx carga realmente `/etc/nginx/conf.d/*.conf` desde `/etc/nginx/nginx.conf`. Existen `sites-available`/`sites-enabled`, pero no se observó su inclusión en la configuración cargada; no usar esas rutas para HR. Vhosts activos en `conf.d`: `default.conf`, `eqmatik.conf`, `eqpair-com.conf`, `eqpair-site.conf`, `eqmatchpy.conf`, `tributa.eqmatik.com.conf`. Hay un `map` de WebSocket ya definido: evitar redefinir el mismo identificador global; usar directivas o nombres propios de HR.

No existen vhosts ni certificados de HR ProTools. Las tres comprobaciones HTTPS de HR fallan por certificado que no corresponde al nombre: resultado esperado antes del deployment, **no son sitios HR publicados**.

Certbot **4.0.0**, plugin Nginx instalado y `certbot.timer` activo. Las renovaciones existentes combinan plugin Nginx y webroot; no se ejecutó renovación/dry-run en Fase 1. Para HR se propone `certonly --webroot` con raíz propia, evitando que el instalador reescriba configuraciones ajenas. Certificados separados: `hrprotools.com` + `www.hrprotools.com`, y `app.hrprotools.com`. Incorporar un hook de recarga condicionado a `nginx -t`, limitado a la renovación de estos certificados, conservando el timer existente. Probar renovación por `--cert-name` solo para HR después de autorización. [Referencia Certbot](https://eff-certbot.readthedocs.io/en/stable/using.html#webroot).

## 5. PostgreSQL y aislamiento

| Cluster | Puerto / directorio | Bases de usuario observadas |
|---|---|---|
| `18/main` | 5432, `/var/lib/postgresql/18/main` | `eq2` 20 MB, `eqpair_beta` 8.5 MB, `tributa_demo` 11 MB |
| `18/eqmatchpy` | 5433, `/var/lib/postgresql/18/eqmatchpy` | `eqmatchpy` 67 MB |

Ambos escuchan en `localhost`, tienen `max_connections=100` y autenticación TCP SCRAM. El cluster main tenía 3 conexiones EQ2 y 1 Tributa, además de procesos internos y la conexión de auditoría. No se leyó ningún hash de contraseña.

Roles main: `postgres` (administrador), `eq2_app`, `eqpair_beta_app`, `tributa_runtime` (login sin superusuario), `tributa_app` (sin login). En 5433: `postgres` y `eqmatchpy_app`. **No existen `hrprotool` ni `hrprotool_app`.**

Propuesta: crear únicamente en **18/main:5432** `hrprotool`, propietario `hrprotool_app`, rol exclusivo con LOGIN, sin SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS ni membresías ajenas. Conceder creación de schemas/objetos solo dentro de su propia base, necesaria para el aprovisionamiento multitenant. Revocar privilegios PUBLIC de la **nueva** base/schema público según corresponda, sin cambiar ACL de bases existentes. Contraseña aleatoria protegida; conexión loopback, límite inicial del rol de 20 conexiones y pools pequeños (p. ej. máximo 5 por pool). El factory actual crea pools distintos por `SearchPath`; el límite del rol evita que la suma de tenants agote las conexiones de terceros.

**Punto que requiere aprobación específica:** las bases existentes conservan CONNECT/TEMP concedidos a PUBLIC; por ello un nuevo rol también recibe CONNECT por esa vía, aunque no sea propietario ni tenga grants sobre tablas ajenas. `NOINHERIT` o un `REVOKE CONNECT` solo al rol nuevo no elimina ese permiso PUBLIC. Para prohibir estrictamente conexiones del nuevo rol a otras bases sin alterar sus ACL, se propone un bloque HBA dirigido exclusivamente a `hrprotool_app`: permitir `hrprotool`, rechazar otras bases, antes de las reglas generales. El bloque debe cubrir TCP IPv4/IPv6 y las conexiones Unix del rol nuevo. Esto **sí toca configuración global**: requiere autorización posterior conforme al encargo. No se hizo en Fase 1. Validar con `pg_hba_file_rules`, recargar configuración sin reiniciar PostgreSQL y comprobar que las reglas de otros usuarios no cambiaron. No modificar el cluster 5433. [Privilegios PostgreSQL](https://www.postgresql.org/docs/18/ddl-priv.html), [orden y recarga HBA](https://www.postgresql.org/docs/18/auth-pg-hba-conf.html).

Producción empieza sin tenants, empresas, funcionarios ni solicitudes. Aplicar migraciones globales y los catálogos normativos requeridos; no importar A/QA27 ni copiar usuarios locales. Las migraciones tenant se aplicarán al aprovisionar el primer tenant real, no creando un tenant ficticio para probarlas. Antes de ejecutar, revisar scripts Up completos sobre base vacía y comprobar que el endpoint de aprovisionamiento funciona con el rol restringido. No ejecutar Down ni operaciones DROP.

## 6. ClamAV, aplicación e identidad

ClamAV usa `/run/clamav/clamd.ctl`, grupo `clamav`, modo observado 666; no hay listener TCP 3310. `StreamMaxLength=25M`, `MaxFileSize=25M`, `MaxScanSize=100M`; son suficientes para los límites actuales de cargas HR. Firmas observadas con fecha 25/11/2026, freshclam activo; PING Unix respondió. No se enviaron documentos reales ni se cambió el daemon compartido.

**Incompatibilidad concreta:** `ClamAvFileSecurityScanner.cs` solo usa `TcpClient`. Se propone agregar soporte Unix manteniendo INSTREAM y los rechazos actuales ante error, sin abrir TCP ni reiniciar ClamAV. Es un cambio mínimo aplicativo que debe ser aprobado antes de implementarse. Alternativas con bridge o un segundo daemon añaden recursos/dependencias y no son la propuesta. [Sockets ClamAV](https://docs.clamav.net/manual/Usage/Scanning.html).

Otros hallazgos aplicativos que deben resolverse antes de publicar:

| Hallazgo | Tratamiento previsto, todavía no implementado |
|---|---|
| `AllowedHosts=localhost;127.0.0.1` | Configuración de producción para `app.hrprotools.com` |
| No se configura middleware de forwarded headers | Confiar solo en Nginx loopback y aplicar headers antes de redirección HTTPS/autenticación; verificar cookies seguras y ausencia de bucles. No confiar en proxies arbitrarios. [Referencia ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) |
| Data Protection por defecto relativa al release | Configurar `/var/lib/hrprotools/keys`, permisos restringidos y backup separado del binario |
| Identity admite cambio de contraseña, pero no lo obliga | Agregar estado persistente de cambio obligatorio y bloqueo de acceso a funcionalidades hasta completarlo; no basta una redirección del formulario de login. Pruebas de no bypass y migración aditiva |
| Bootstrap lee contraseña de configuración | Aprovisionamiento de una sola cuenta `orloch314@gmail.com` con entrada efímera segura por consola/entrada estándar, sin argumento, archivo temporal, log, historial ni contraseña fija; eliminar el secreto de bootstrap después de usarlo. El mecanismo concreto se prepara y valida en Fase 2 antes del acceso público |
| Health check actual | `/health` verifica disponibilidad del proceso; no incluye prueba PostgreSQL. Comprobar DB separadamente y no afirmar conectividad por un 200 de ese endpoint |

Se propone publicación **self-contained `linux-x64` .NET 10**: incluye su runtime y no necesita instalar/alterar el runtime .NET 8 compartido. ICU 78, OpenSSL 3, libc/zlib están presentes; verificar dependencias nativas del artefacto final antes de habilitar servicio. Las actualizaciones de seguridad de ese runtime deben llegar en futuras publicaciones HR. [Referencia .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

## 7. Correo y sitio Astro

DNS público de correo: MX `mx.hrprotools.com` prioridad 10; SPF `v=spf1 include:_spf.aruba.it ~all`; DMARC `v=DMARC1; p=none; adkim=r; aspf=r;` (monitorización). Consultas a `aruba._domainkey` y `default._domainkey` devolvieron NXDOMAIN: **no demuestra que DKIM esté ausente**, porque no se conoce el selector real. Hace falta selector confirmado de Aruba o una cabecera firmada de una prueba autorizada.

Desde el VPS se completó STARTTLS con `smtps.aruba.it:587`, TLS 1.3 y certificado verificado. El sondeo separado de 465 no completó handshake dentro del timeout de 15 s; no se declara esa modalidad disponible ni definitivamente bloqueada. No se hizo AUTH, MAIL FROM, envío ni prueba de recuperación/invitación.

La configuración HR local anterior corresponde al dominio remitente `imatchpy.com`, según documentación. **No sirve como prueba de autorización para `info@hrprotools.com`**. Reutilizar `SmtpSettingsService`, pero configurar/verificar la cuenta y remitente nuevos. Producción vacía no contiene la configuración SMTP ni las claves Data Protection locales; no copiar ciphertext de otra instalación/proyecto. Las credenciales nuevas se introducen por canal seguro, se protegen con las claves de producción y se prueba un único envío autorizado antes de invitaciones/recuperación.

Sitio actual: Astro estático, sin proceso Node permanente. `PUBLIC_APP_LOGIN_URL` permite configurar el enlace sin duplicar autenticación. Ruta real comprobada en el código: **`https://app.hrprotools.com/Account/Login`**. Las páginas/accesos secundarios consumen la misma variable. Actualmente Contacto no publica dirección comercial: agregar `info@hrprotools.com` y `mailto:` en Fase 2. El formulario está deshabilitado y dice expresamente que no envía ni guarda datos; conservar ese comportamiento hasta contar con entrega real, sin crear otro backend.

Antes de generar release, revisar/versionar el sitio actualmente no seguido por Git y comprobar build estático, las seis páginas principales, correo, enlace de login y mobile. No publicar `node_modules`, archivos .env, fuentes ni mocks; solo `dist` verificado.

## 8. Backup, riesgos y rollback

Existe `eq2-backup.timer` diario; último resultado observado correcto y dump del 26/11/2026 02:38 UTC, aproximadamente 1.49 MB, con archivo de checksum. Se observaron backups manuales de Tributa de octubre, Eqpair de julio y Eqmatchpy de agosto. Esto no excluye otros backups externos; no se verificaron snapshots del proveedor ni recuperación real. **No se certifica recuperabilidad por la mera existencia de archivos.**

Antes de Fase 2: confirmar backup recuperable del alcance que vaya a tocarse, guardar copia y hashes de configuración que efectivamente se modificará y baseline de respuestas/servicios. Al ser una base HR nueva y no modificar datos ajenos, no se necesita restaurar otras bases como parte de ningún rollback. Después de crear estructura/datos mínimos HR, backup propio `pg_dump -Fc`, checksum, claves y configuración cifrada/protegida; definir retención y copia fuera del VPS, pendiente de acordar. Para futuras actualizaciones, backup propio previo y validación de restauración en entorno aislado.

Riesgos principales, con alcance concreto:

1. **Memoria compartida limitada:** ClamAV ya consume cerca de 1 GiB; evitar otra instancia y monitorizar HR con límites. No se efectuó carga ni benchmark.
2. **Anomalía previa Eqmatch y reinicio SO pendiente:** informar y conservar baseline; ninguna reparación/reinicio está autorizada en este trabajo. Acordar cómo se registrará su estado previo al aceptar el deployment.
3. **Nginx compartido:** un include inválido afecta la recarga de todos los sitios. Solo dos archivos nuevos; `nginx -t` obligatorio antes de cualquier reload, rollback quitando únicamente los includes HR.
4. **Aislamiento PostgreSQL:** no cambiar bases/roles existentes; la excepción HBA propuesta requiere autorización específica. No reiniciar el cluster.
5. **Credenciales/cambio obligatorio/antivirus/proxy:** pendientes identificados; no publicar el servicio sin resolverlos y probarlos.
6. **Email:** TLS y DNS básicos no prueban credenciales ni DKIM ni autorización de remitente. Sin envíos reales hasta completarlos.

Rollback previsto: detener únicamente `hrprotools.service`, retirar de carga solo los vhosts nuevos de HR, validar y recargar Nginx; cambiar symlink HR a release anterior cuando exista. Conservar DB, claves y backups; **sin DROP ni migraciones Down**. Si se autorizó HBA, revertir solo el bloque HR con validación y reload, sin tocar reglas previas. No restaurar toda la configuración global ni el cluster, no cambiar servicios de terceros. Para una primera publicación fallida, puede quedar HR detenido y sus datos conservados mientras los sitios previos siguen funcionando.

## 9. Cambios propuestos, archivo por archivo

**Nada de esta tabla fue creado/modificado en el VPS durante Fase 1.**

| Ruta / objeto | Acción prevista en Fase 2 |
|---|---|
| `/opt/hrprotools/releases/<id>/web/` | Nuevo artefacto self-contained; usuario de ejecución sin capacidad de modificar binarios |
| `/opt/hrprotools/current` | Nuevo symlink al release autorizado |
| `/var/www/hrprotools/releases/<id>/` y `/var/www/hrprotools/site` | `dist` estático y symlink; lectura para el usuario Nginx real `nginx` |
| `/var/www/hrprotools/acme/` | Nueva raíz HTTP-01 compartida únicamente por los dominios HR |
| `/var/lib/hrprotools/keys/` | Claves persistentes, propietario usuario HR y modo restrictivo |
| `/etc/hrprotools/production.env` | Production, loopback `127.0.0.1:5091`, AllowedHosts, URL pública, conexión protegida, claves y antivirus; root 600. **Sin contraseña inicial de bootstrap persistida** |
| `/etc/systemd/system/hrprotools.service` | Nuevo servicio no root, restart on-failure, journald, límites de recursos, hardening y rutas de escritura propias |
| `/etc/nginx/conf.d/hrprotools-site.conf` | Nuevo vhost sitio, `www` → raíz, HTTPS, ACME, estáticos; no editar otros vhosts |
| `/etc/nginx/conf.d/hrprotools-app.conf` | Nuevo vhost app → 5091, HTTP/1.1, WebSocket, forwarded headers, límites de carga/timeouts apropiados |
| `/etc/letsencrypt/live/hrprotools.com/` y `live/app.hrprotools.com/` | Nuevos certificados con Certbot webroot, sin reemplazar certificados ajenos |
| `/etc/letsencrypt/renewal/hrprotools.com.conf`, `app.hrprotools.com.conf` | Nuevas renovaciones; hook condicionado a prueba Nginx y a estos certificados, sin renovar otros dominios como prueba |
| `/etc/postgresql/18/main/pg_hba.conf` | **Excepción pendiente de aprobación:** include/bloque dirigido al rol nuevo, sin editar reglas previas; validar y recargar, no reiniciar |
| DB `hrprotool`, rol `hrprotool_app` | Nuevos, límites y grants propios; ninguna modificación de bases ajenas |
| `/var/backups/hrprotools/` | Backups y manifiesto propios protegidos; programación y destino externo por confirmar |
| `src/HRProTools.Web/Services/ClamAvFileSecurityScanner.cs` | Soporte Unix, conservando TCP y política de fallo seguro |
| `src/HRProTools.Web/Data/ApplicationUser.cs`, `Services/IdentitySeeder.cs` | Estado de contraseña inicial y bootstrap seguro, sin credencial fija |
| `src/HRProTools.Web/Program.cs`, `Components/Account/Pages/Login.razor`, `Manage/ChangePassword.razor`, migración global nueva y pruebas afectadas | Forwarded headers confiables y enforcement de cambio obligatorio; diseñar mínimo antes de implementar, sin alterar reglas HR |
| `site/src/pages/contacto.astro` y componente de pie/configuración si corresponde | Dirección comercial y `mailto:`; formulario sigue sin confirmación ficticia |
| Variable de build `PUBLIC_APP_LOGIN_URL` | URL de login HTTPS; revisar todos los accesos del sitio |
| `docs/ESTADO_PROYECTO.md`, este documento | Registrar cambios realmente autorizados/aplicados y pruebas de Fase 2 |

Puerto **5091** estaba libre en la inspección; volver a comprobar antes de asignarlo. Usuarios/grupos `hrprotools`, rutas `/opt/hrprotools`, `/etc/hrprotools`, `/var/lib/hrprotools`, `/var/www/hrprotools` y los dos vhosts propuestos no existían. No crear un proceso Node para Astro, no instalar Docker ni herramientas administrativas públicas. Evitar cualquier actualización automática de paquetes como parte del deployment; no hace falta instalar .NET 10 global con la opción self-contained.

## 10. Secuencia y puntos de aprobación

1. **Detenerse ahora** y revisar este informe. Para Fase 2, autorizar las adaptaciones HR identificadas y, de forma explícita, el bloque HBA que afecta configuración global. Confirmar provisión segura del remitente Aruba, selector DKIM y método seguro de contraseña inicial; no pegar contraseñas en la conversación.
2. Resolver primero los cambios aplicativos y probarlos localmente; versionar el sitio. Generar releases, checksums y scripts de migración revisados. No publicar una build con bypass de ClamAV o contraseña fija.
3. Revalidar recursos/puerto/DNS/servicios, confirmar backup del alcance, crear aislamiento HR, DB y configuración protegida. Aplicar solo migraciones HR; bootstrap único, sin tenants ficticios.
4. Arrancar HR únicamente en loopback. Probar DB, antivirus, primer cambio de contraseña y ausencia de accesos alternativos; configurar SMTP protegido y URL pública.
5. Incorporar vhosts nuevos/ACME, validar Nginx, recarga controlada; emitir solo los certificados HR, habilitar HTTPS y probar su renovación dirigida. No reiniciar Nginx/PostgreSQL ni VPS.
6. Verificar login/dashboard/health más DB, cookies, navegación Blazor/SignalR, sesión tras reinicio solo del servicio HR, claves persistentes y enlaces de invitación/recuperación mediante una prueba de correo autorizada. No crear tenant de prueba sin permiso adicional.
7. Verificar inicio, funcionalidades, planes, RSE, preguntas frecuentes, contacto, correo, login y mobile del sitio. Repetir baseline de todos los sitios y servicios anteriores; distinguir la anomalía Eqmatch preexistente. Documentar y detener/retroceder HR si surge una regresión ajena.

## 11. Resultado de cierre de Fase 1

Las 23 improntas de archivos seleccionados de Nginx, renovaciones, PostgreSQL, servicios y ClamAV coincidieron entre la primera toma y la comprobación final. Nginx, ambos clusters, EQ2, Eqmatchpy, Eqpair Beta, Tributa, ClamAV y freshclam seguían activos al cierre; Eqmatch conserva la anomalía ya descrita. No se iniciaron migraciones, cambios de contraseña, instalación ni pruebas de carga/envío. Este fue el cierre de la auditoría, anterior a la autorización local descrita a continuación.

## 12. Preparación local autorizada — 26/11/2026

El usuario aprobó el plan técnico, incluida la excepción HBA, pero limitó expresamente esta etapa a código, artefactos y scripts locales. **No hubo conexiones SSH, instalaciones, cambios, cargas ni ejecuciones en el VPS durante esta preparación. No se reparó Eqmatch.**

### Implementación preparada

- ClamAV admite `UnixSocketPath`, conservando TCP para entornos que ya lo utilizan. Si se configura socket, no hay fallback a un escaneo omitido. Se mantienen INSTREAM, timeout y rechazo ante infección, error o falta de confirmación. Producción apunta al socket existente y exige análisis.
- Forwarded headers solo desde loopback IPv4/IPv6, un salto, sin confiar en `X-Forwarded-Host`. Cookies y antiforgery seguras en Production. `appsettings.Production.json` define dominio, URL pública y rutas no secretas.
- Data Protection exige ruta absoluta fuera del release; en Linux crea/verifica permisos 0700. El nombre de aplicación permanece `HRProTools`. El bundle excluye claves, configuraciones locales, Development y símbolos.
- Nueva migración global **`20261126200702_InitialAdministratorPasswordChange`**: columna `MustChangePassword`, default false para preservar cuentas existentes. Solo el bootstrap inicial nuevo la activa. No se modifica el modelo tenant.
- Cambio inicial obligatorio protegido por claim/políticas, middleware que vuelve a consultar el estado en DB (incluidas cookies antiguas sin claim) y bloqueo del canal Blazor. Cambiar la contraseña y quitar la marca es una operación transaccional; no admite repetir la inicial. La revalidación del circuito también rechaza cuentas pendientes. No basta con redirigir el login.
- Modos CLI sin iniciar servidor/workers: `--migrate-only` y `--bootstrap-admin-stdin`. El bootstrap requiere Production, entrada redirigida protegida y base sin usuarios, con bloqueo transaccional; no cambia cuentas existentes ni crea tenants. La configuración persistida `BootstrapAdmin:Password` se rechaza en Production.
- El wrapper `hrprotools_admin.py bootstrap` pide una **nueva** credencial y su confirmación mediante `getpass` en terminal interactivo, la entrega al proceso por stdin y no la escribe en archivos, argumentos, entorno persistido, logs ni documentación. No se leyó ni reutilizó ninguna contraseña inicial anterior para el aprovisionamiento. La nueva contraseña de DB se genera aparte y solo se guarda en configuración root 600; SQL recibe un verificador SCRAM, no el texto de esa contraseña.
- Astro publica `info@hrprotools.com`, `mailto:` y acceso a `https://app.hrprotools.com/Account/Login`, incluyendo enlaces secundarios. Conserva el formulario deshabilitado sin confirmación ficticia. No incluye backend de contacto.

### Scripts y protecciones

Todos están bajo `deploy/production`, sin SSH ni upload automático. Los administrativos requieren `--apply` y hostname aprobado; son archivos preparados, **no ejecutados contra producción**.

| Archivo | Preparado para |
|---|---|
| `hrprotools_admin.py` | Provisión inicial exclusiva, validación de hash/reglas HBA previas, copia privada de rollback, bloque dirigido solo a HR, comparación de reglas ajenas y recarga sin restart. Escritura HBA atómica preservando propietario/modo; restauración inmediata si falla validación. Cuenta, DB y rutas existentes provocan rechazo en vez de sobrescritura. Modos independientes migrate/bootstrap/rollback-hba |
| `hrprotools.service` | Binario self-contained sin root, configuración protegida, grupo suplementario ClamAV, claves fuera del release, límites de RAM/CPU y journald |
| `install-release.sh` | Verificar SHA256SUMS/ELF y referencias estáticas, crear únicamente symlinks/unidad HR; no arrancar la aplicación ni cambiar Nginx. Primera instalación se niega a sustituir un servicio/symlink existente |
| `nginx-acme.conf`, `activate-nginx.sh` | Fase HTTP-01 aislada antes de certificados; prueba Nginx previa/posterior, backup de solo los archivos HR, recarga condicionada y rollback de esos archivos |
| `nginx-site.conf`, `nginx-app.conf` | TLS posterior, redirección www, estáticos, reverse proxy loopback y map WebSocket propio sin colisión con el existente |
| `renew-hrprotools.sh` | Hook solo para las dos lineages HR, con `nginx -t` antes de reload |
| `hrprotools_backup.py` (ejecución manual futura) | Dump de **solo hrprotool**, configuración y claves persistentes en directorio 0700, archivos 0600 y manifiesto SHA-256. No sigue symlinks ni acepta miembros inseguros en el archivo; no confunde verificación de hash con una recuperación real |
| `rollback-hrprotools.sh` | Detener solo HR y retirar solo sus includes; validar antes de reload, conservar DB/roles/claves. No ejecuta DROP, Down ni restauración global |

No se debe ejecutar el timer ni la provisión antes de aprobar el deployment remoto. Los backups iniciales están protegidos por permisos locales: **destino externo, retención y cifrado externo siguen pendientes de acordar**; no se simula una copia externa que no existe.

### Artefacto reproducible

`deploy/Build-Production.ps1` publica **.NET 10 self-contained linux-x64**, compila/verifica Astro, genera SQL global y tenant idempotente y prepara un tar con manifiesto SHA-256. `deploy/package_release.py` rechaza claves, .env, secretos locales, símbolos y paths inseguros. No sube archivos ni cambia el runtime compartido.

El archivo `version.json` identifica el commit base y declara cambios locales sin commit: no se presenta esta build como procedente de un commit nuevo publicado. El SQL tenant es plantilla para aprovisionamiento futuro; no se aplica al schema público ni se crea un tenant ficticio para iniciar producción.

Entrega local final: `C:\HRProTools\artifacts\production\prepared-20261126.tar.gz` (**71,655,291 bytes**), con directorio expandido `prepared-20261126`, `SHA256SUMS`, scripts SQL y manifiesto de versión. SHA-256 del tar: `852a82b2907a0d3fb32d3cbc7f914c0ed507b1afe426af0ac061f54173130686`. Es un paquete preparado localmente, no cargado al VPS. Los artefactos y claves/datos sintéticos de QA permanecen ignorados por Git.

### Validación local realizada

- Build Release correcta. **111 pruebas .NET superadas**, 0 fallidas/omitidas, contra bases QA separadas del DB local original; incluye 9 casos nuevos de Unix/TCP antivirus, rechazo seguro, proxies no confiables, claves persistentes y cambio de contraseña/no bypass. Los casos de ClamAV usan un daemon simulado para verificar el protocolo y los resultados: no se afirma una prueba de firmas del daemon real del VPS.
- Astro: build estático de **9 páginas**, verificación de **210 enlaces/recursos locales**, mailto y acceso HTTPS, formulario sin envío. Sin rediseño ni cambio de contenido funcional ajeno al contacto/acceso.
- Ejecución del **binario Linux self-contained real** como usuario no root dentro del contenedor HR local, en un cluster PostgreSQL propio bajo un directorio temporal único y puerto interno 55438. El cluster original 5432 del contenedor no se reconfiguró. Inicialización, migraciones, bootstrap sintético sin tenants, login HTTP con esquema HTTPS forwarded, cookies Secure, bloqueo admin y Blazor antes del cambio, cambio efectivo y acceso posterior: correctos.
- HBA en ese cluster aislado: parser PostgreSQL sin errores; reglas originales idénticas al excluir solo el bloque nuevo; HR conecta a su base y es rechazado en otra; usuario distinto mantiene acceso. Rollback devuelve las reglas originales y su funcionamiento, **sin reiniciar PostgreSQL**.
- **Recuperación real**: `pg_dump` custom → `pg_restore` en base QA vacía, conteos e improntas iguales de todas las tablas auth/platform, con usuario y estado de contraseña sintéticos. Se restauraron claves desde el backup, reinició solo la app QA y una sesión anterior volvió a autenticarse con las claves recuperadas. No es solo una inspección de la cabecera del dump.
- Nginx **1.31.2**: `nginx -t` correcto para ACME y TLS en contenedores efímeros locales con red desactivada, sin puertos publicados y certificados autofirmados de QA; no son certificados de producción.
- Scripts Python compilados y shell revisados con `bash -n`; el provisioning sin `--apply` fue rechazado. Los clusters de prueba propios fueron detenidos; no se detuvieron servicios del usuario. Las bases QA separadas conservadas sirven como evidencia, no como datos para desplegar.

### Orden preparado para futura ejecución, todavía no autorizado

1. Revalidar inventario/DNS/puerto, hash HBA y backups del alcance. Revisar hashes y script SQL completos. Extraer únicamente el bundle verificado en un release nuevo HR, sin seguir rutas o symlinks externos.
2. Ejecutar provisión con `--apply --expected-host Eqmatik --expected-hba-sha256 <hash-revalidado>`. Si un paso parcial falla, inspeccionar estado y usar el rollback HBA; no borrar/recrear roles o bases automáticamente.
3. Instalar release/unidad HR y ejecutar migrate. Introducir la **nueva** credencial por el wrapper interactivo de bootstrap; no pasarla en un comando. Arrancar únicamente HR en loopback y comprobar su estado.
4. Instalar ACME HR validado, emitir con el Certbot existente usando webroot propio (certificados sitio/www y app por separado), luego activar TLS. Nunca usar standalone que obligue a detener Nginx. Instalar el hook HR y probar renovación dirigida solo a esos certificados.
5. Completar SMTP por canal protegido con credenciales Aruba verificadas, selector DKIM y permiso para el remitente. Hacer únicamente el envío de prueba autorizado. No copiar configuración/ciphertext de otro proyecto.
6. Tomar backup propio, verificar manifest y realizar prueba de recuperación en entorno aislado antes de declarar el procedimiento operativo. En producción, el restore se dirige a una base vacía previamente autorizada y a rutas HR vacías; se restauran claves/configuración con propietario/permisos correctos y se valida la sesión. Nunca sobrescribir una base en uso, limpiar schemas, ejecutar Down ni restaurar globalmente PostgreSQL para un rollback aplicativo.
7. Verificar sitios/servicios anteriores, SSL público, SignalR con conexión real del navegador, recuperación/invitación y límites de carga. La prueba local no sustituye estas comprobaciones remotas. No reparar Eqmatch ni reiniciar el VPS. No crear tenant/empresa de prueba sin permiso específico.

### Pendiente para publicar

- **Autorización explícita de ejecución remota**, actualmente denegada por el alcance de esta etapa. La aprobación técnica HBA no equivale a permiso para aplicarlo ahora.
- Revalidar el estado del VPS y hash HBA al momento del cambio; confirmar backup recuperable del alcance y tratamiento de la anomalía Eqmatch preexistente.
- Nueva contraseña inicial introducida por el operador en el prompt seguro. No enviarla en chat ni usar una anterior.
- Usuario/credencial y autorización de remitente Aruba para `info@hrprotools.com`, selector DKIM o prueba firmada válida y autorización/destinatario del envío controlado.
- Destino/retención/cifrado de copia externa. No se requiere otra clave SSH: se mantiene la indicada en el encargo, sin copiarla.
- Commit/publicación del código y sitio cuando se solicite; no se hizo push de esta preparación. Certificates públicos, cuentas/datos y scripts quedan **preparados, no instalados en el VPS**.


## 13. Deployment de pruebas autorizado — 26/11/2026

Esta sección prevalece sobre los pendientes y propuestas históricos anteriores. El usuario autorizó commit/push de la rama de trabajo existente y deployment incremental en 95.217.134.178. Solo se crean recursos HR; no se reparan Eqmatch ni otras aplicaciones.

- No se instalarán servicios/timers de backup HR ni copias externas. Se conservan los scripts manuales de backup/restauración ya validados para uso futuro. Las copias puntuales de los archivos HR/HBA que se modifiquen sirven exclusivamente para rollback dirigido; se preservan los backups de otros proyectos.
- SMTP autorizado: info@hrprotools.com, smtps.aruba.it:587 STARTTLS, destinatario único orloch314@gmail.com. El comando administrativo `smtp` pide la contraseña con getpass, la transmite por stdin, la protege mediante SmtpSettingsService/Data Protection y registra antes del contacto un único intento persistente. Una repetición se rechaza incluso ante un fallo ambiguo; no se reenvía automáticamente. Nunca se guardan la credencial en claro, argumentos ni logs. La aceptación SMTP no prueba entrega en bandeja ni DKIM.
- Bootstrap de orloch314@gmail.com mediante prompt oculto independiente, sin reutilizar contraseñas. Cambio obligatorio en primer acceso conservado. El operador debe introducir ambas credenciales en el terminal seguro, no en chat.
- `Build-Production.ps1 -RequireClean` rechaza cambios pendientes en fuentes/versionados y cambios durante la construcción. Excluye los mockups ajenos no versionados, que no forman parte del bundle. version.json y SHA256SUMS identifican el commit exacto publicado. El bundle anterior prepared-20261126 es únicamente evidencia local y no se desplegará.
- Revalidación SSH de solo lectura a las 21:03 UTC: HR aún ausente, 5091 libre, HBA sin errores y hash 5a362999da209cb6cb6f121a7c900dd0286c1431d39dc911c54cece773a9c9d2. Nginx, ambos clusters PostgreSQL, EQ2, Eqmatchpy, Eqpair Beta, Tributa y ClamAV activos. Eqmatch se encontró inactivo/dead (Result=success), un estado previo externo a esta tarea que se preserva. Sitios anteriores: 200/302 según sus redirecciones actuales.
- Suite Release repetida con la protección del envío único: **111 superadas, 0 fallidas, 0 omitidas**, 1 min 31 s. No se envió correo desde QA. Resultados de publicación y verificaciones remotas se completarán con evidencia real al finalizar.


### Resultado remoto inicial y corrección solicitada durante la aceptación

- Publicado `c4bdc99594c18e0589651849681f364274aaa1b0` en `codex/phase5-mvp-completion`. Bundle `test-20261126-c4bdc99.tar.gz`, SHA-256 `ed30337b9cf769c8ed7ded58a8df459d853dc24695d1764ba75d57add3092f12`; verificación remota de archivo, miembros seguros, manifiesto y version.json exactos. Segunda ejecución Linux QA del propio bundle: correcta, incluida recuperación real y rollback HBA.
- Creado únicamente DB/rol/usuario/rutas HR. Cinco migraciones globales aplicadas; última `20261126200702_InitialAdministratorPasswordChange`. No se importaron datos locales ni se crearon tenants sintéticos remotos. Las altas posteriores realizadas por el operador se conservan.
- HBA conserva exactamente los bytes originales detrás del bloque HR aprobado; parser sin errores, acceso propio correcto y rechazo a otra DB comprobado. PostgreSQL no se reinició. Copia puntual de rollback en `/var/backups/hrprotools/preflight-20261126T212012Z`, sin timers ni servicios de backup HR.
- `hrprotools.service` habilitado y running, primer PID 663017, sin reinicios inesperados; escucha exclusivamente en 127.0.0.1:5091. Data Protection y sus directorios 0700, configuración root 0600. ClamAV existente: INSTREAM limpio OK, EICAR FOUND y PONG como usuario hrprotools, sin modificar el daemon.
- Bootstrap con entrada oculta completado por el operador; cambio obligatorio realizado posteriormente. DB confirma `MustChangePassword=false` para el admin inicial y la captura del operador muestra sesión Global autenticada. Ninguna contraseña fue conocida por Codex.
- SMTP Aruba cifrado mediante Data Protection; exactamente un intento inicial, `ACCEPTED_BY_SMTP`. Recepción en bandeja y DKIM pendientes de confirmación; no se realizó reenvío.
- HTTPS público correcto en raíz/app (200), www (308 a raíz), certificados Let's Encrypt YE2 hasta **24/02/2027**, SAN apropiados. La primera consulta inmediatamente posterior a la recarga observó transitoriamente el certificado previo; la comprobación posterior y la externa validan los nuevos, sin desactivar controles TLS ni tocar vhosts ajenos.
- Nginx master y ambos PostgreSQL mantienen los PID iniciales. EQ2, Eqmatchpy, Eqpair Beta, Tributa y ClamAV mantienen sus procesos; las siete URLs anteriores conservan 200/302. Eqmatch permanece inactivo como en el baseline, sin reparación.
- El operador solicita retirar también el código del grupo del formulario Global. Corrección preparada con asignación correlativa backend, bloqueo transaccional e índice único existente; no requiere migración ni modificación de códigos previos. Se publicará un segundo release de esa revisión, sin volver a ejecutar provisión/bootstrap/SMTP ni modificar Nginx.

- Corrección de grupo: suite completa Release **112/112 correcta**, 0 fallidas/omitidas, 1 min 46 s; sin secretos ni artefactos en los cambios.


## 14. Cierre del deployment de pruebas y corrección Global — 26/11/2026

- **Commit aplicativo publicado y activo:** `85487dda41b8d00a63abe6ff2912cf2b406301fd`, rama `codex/phase5-mvp-completion`. Retira la entrada de código de grupo y asigna GRP correlativo en backend. El commit documental posterior registra este resultado; no modifica la revisión del binario.
- **Artefacto exacto:** `test-20261126-group-auto.tar.gz`, SHA-256 `090871b8b34b581f0ad3f1eb861151a67e15ca0162b311ebc9184bd209a4f645`, source_commit de version.json igual al hash publicado, uncommitted_changes=false. Verificación remota SHA-256 de tar y todos sus miembros. Symlinks actuales apuntan al nuevo release de app y sitio; el anterior permanece para rollback. La activación tuvo rollback automático preparado si fallaba el health check.
- **Cambio preservador:** no se repitieron provisión, migraciones, bootstrap ni envío SMTP. Se compararon códigos/esquemas de los grupos anteriores y archivos de claves persistentes antes/después: conservados. No se modificaron datos introducidos por el operador.
- **Servicio:** hrprotools.service enabled, active/running, PID observado 664434, NRestarts=0. Loopback 127.0.0.1:5091; health y login responden. Solo HR se reinició para activar esta revisión.
- **Sitios/HTTPS:** raíz 200, www 308, app/login 200; nueve páginas Astro externas 200 con validación TLS. Certificados hasta 24/02/2027 y **ambos dry-run de renovación correctos**, limitados a los dos lineages HR. Certbot aplicó su espera aleatoria habitual; no hubo errores de renovación. Una comprobación adicional del flag para omitir esa espera terminó sin cambios; no interrumpió la simulación original, que acabó correctamente.
- **Blazor:** POST de negociación HTTPS 200, upgrade real a WSS y handshake BlazorPack aceptado desde el exterior, sin credenciales ni cambios de datos. El operador completó el acceso y cambio inicial y aportó captura autenticada Global. La validación de transporte no equivale a una aceptación funcional completa por roles; esa aceptación continúa con los datos/usuarios propios del operador, sin tenants sintéticos remotos.
- **Base/migraciones y seguridad:** cinco migraciones globales correctas, última InitialAdministratorPasswordChange; HBA dirigido conserva íntegra la configuración previa y acceso HR aislado. ClamAV y sus pruebas, configuración cifrada SMTP y permisos de claves según sección 13. No se copiaron bases QA ni secretos locales al VPS.
- **SMTP:** un solo intento de prueba del deployment, aceptado por Aruba. No hay confirmación de recepción ni evidencia de DKIM; no se afirma que funcionen ni se envía de nuevo. La configuración protegida persiste en PostgreSQL y las claves sobreviven al cambio de release.
- **Continuidad:** Nginx 2616305, PostgreSQL main 2616413 y eqmatchpy 2616421, EQ2 2616323, Eqmatchpy 2616357, Eqpair Beta 2616328, Tributa 490061 y ClamAV 457165: mismos PID/estado iniciales. Los siete sitios anteriores conservan 200/302; hashes de las siete configuraciones Nginx previas idénticos. Eqmatch sigue inactivo y no fue modificado/reparado.
- **Backups y límites:** sin timers/servicios de backup HR ni copias externas por decisión explícita del usuario; scripts manuales conservados y recuperación real comprobada localmente. Solo copias puntuales de configuración para rollback. Sin reinicio de VPS/PostgreSQL/Nginx, actualizaciones generales, cambios ajenos ni Docker remoto.
- **Pruebas:** 112 superadas, 0 fallidas/omitidas, 1 min 46 s; Linux self-contained build correcto y Astro 9 páginas/210 recursos correcto. Git conserva fuera los dos mockups ajenos sin seguimiento; binarios, credenciales, .env reales y datos temporales/QA no se incorporaron. Código y sitio publicados sin modificar historial; el cierre documental se publica separadamente.

Para comprobar la corrección, recargar `/admin/grupos` y abrir **Nuevo grupo**: únicamente nombre del grupo, razón social, RUC, administrador tenant y correo. El código automático se conserva como dato de consulta en la lista y no se solicita, ni siquiera como campo deshabilitado.
