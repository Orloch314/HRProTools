# Permisos y licencias del sector privado paraguayo

Consulta de fuentes: 23/11/2026. Versión operativa: `PY-2026-11-23-v1`. Esta fecha identifica la vigencia de la versión del software, no la promulgación de cada ley. La matriz es una especificación de implementación y no un dictamen jurídico.

## Fuentes oficiales

- [BACN, Ley 3384/2007, art. 1 y Código Laboral 62(j)](https://www.bacn.gov.py/leyes-paraguayas/3438/modifica-el-articulo-62-inciso-j-de-la-ley-n-21393-que-establece-el-codigo-del-trabajo).
- [BACN, Ley 5508/2015, arts. 11–14](https://www.bacn.gov.py/leyes-paraguayas/4428/promocion-proteccion-de-la-maternidad-y-apoyo-a-la-lactancia-materna).
- [BACN, Ley 7534/2025](https://www.bacn.gov.py/leyes-paraguayas/12838/ley-n-7534-2025-que-modifica-y-amplia-los-articulos-2-y-11-de-la-ley-n-5508-2015). [SILPY, publicación 23/09/2025](https://silpy.congreso.gov.py/web/ley/146073).
- [MTESS, guía de maternidad, paternidad y lactancia, Decreto 7550/2017](https://www.mtess.gov.py/wp-content/uploads/2026/03/MaternidadPaternidadLactancia.pdf).
- [SILPY, Ley 7383/2024, texto legislativo](https://silpy.congreso.gov.py/web/descarga/ley-145156?preview=). [Publicación 20/12/2024](https://silpy.congreso.gov.py/web/ley/145156). [MTESS: extensión médica y preaviso](https://mtess.gov.py/?p=30138).
- [BACN, Ley 6211/2018, modificación de Ley 3803/2009](https://www.bacn.gov.py/leyes-paraguayas/8937/ley-n-6211-modi). [IPS, información del beneficio preventivo](https://portal.ips.gov.py/sistemas/ipsportal/noticia.php?cod=4022).
- [BACN, Ley 7659/2026, art. 13 de Ley 6280/2019 modificado](https://www.bacn.gov.py/leyes-paraguayas/13174/ley-n-7659-2026-que-ampl-a-y-modifica-el-ac-pite-y-los-art-culos-1-2-4-6-9-11-12-y-13-de-la-ley-n-6280-2019-que-crea-el-programa-nacional-de-prevenci-n-detecci-n-precoz-y-tratamiento-del-c-ncer-de-pr-stata-y-colon). [SILPY, publicación 22/05/2026](https://silpy.congreso.gov.py/web/ley/146526).
- [BACN, Ley 7371/2025, art. 17](https://www.bacn.gov.py/leyes-paraguayas/12775/ley-n-7371-2025-que-garantiza-los-derechos-de-las-personas-con-discapacidad-modifica-varios-art-culos-de-la-ley-n-1183-1985-c-digo-civil-y-modifica-los-art-culos-262-y-357-de-la-ley-n-879-1981-c-digo-de-organizaci-n-judicial).
- [BACN, Código del Trabajo, arts. 62(h), 87–89](https://www.bacn.gov.py/leyes-paraguayas/2608/ley-n-213-establece-el-codi-).
- [IPS, régimen de maternidad](https://portal.ips.gov.py/sistemas/ipsportal/contenido.php?c=131). Se consulta para distinguir licencia y subsidio; no se implementan liquidaciones.

## Matriz jurídica y funcional resumida

Todas las filas usan la versión de software indicada arriba y requieren verificación HR del caso concreto. Las fechas de publicación confirmadas se indican expresamente; para las restantes debe completarse la comprobación de vigencia/reglamentación antes de producción. No se asigna una fecha normativa desconocida por defecto.

| Código / nombre | Ley y artículo | Duración / unidad / beneficiarios | Requisitos y documentación | Excepciones y preaviso | Remuneración | Regla implementada / revisión pendiente |
|---|---|---|---|---|---|---|
| MATRIMONIO — Matrimonio | 3384/2007, art. 1; CL 62(j) | 3 días/evento; trabajador que se casa | Fecha, constancia | Sin preaviso específico confirmado | Salario empleador | Tope agregado por evento. Se registran fechas elegidas, incluso tres no consecutivas; HR verifica cómputo. No se trasladan fines de semana/feriados automáticamente. Validar convención temporal. |
| LUTO_FAMILIAR — Fallecimiento | Misma fuente | 3 días/evento; cónyuge, hijos, padres, abuelos, hermanos | Defunción y parentesco | Comunicación posterior admitida | Salario empleador | Parentesco controlado; mismo límite y revisión del cómputo. No incluye primos/tíos automáticamente. |
| PATERNIDAD — Paternidad | 5508/2015, 13(b) | 2 semanas; padre de recién nacido | Nacimiento e inscripción | Posteriores al parto | Salario empleador | Un registro de 14 días; nunca suma los tres días antiguos. Confirmar documentación y fecha de inicio concreta. |
| MATERNIDAD — Maternidad | 5508/2015, 11; 7534/2025, art. 1 | 18 semanas; 24 especiales; múltiple: mes por hijo adicional | Certificado IPS/MSPBS; supuesto especial | Inicio prenatal hasta 2 semanas antes; transferencia por fallecimiento al padre/cuidador | Régimen de subsidio, sin calcularlo | Mayor extensión entre supuesto especial y múltiple. Remanente limitado al período original informado, sujeto a prueba documental. Publicación modificatoria 23/09/2025; validar transición y significado de mes en casos concretos. |
| ADOPCION — Adopción/acogimiento judicial | 5508/2015, 13(a) | 18 semanas menor de 6 meses; 12 mayor | Madre adoptante/acogedora; resolución judicial y nacimiento | Edad exacta de seis meses a revisión especializada | Régimen aplicable, sin subsidio calculado | No extiende automáticamente a cualquier cuidador. A los seis meses exactos permite declarar el supuesto propuesto, cuya verificación HR debe fundamentarse. |
| LACTANCIA — Lactancia | 5508/2015, 14; guía MTESS bajo D. 7550/2017 | Inicial 90 min/día; extensión 60; +60 por hijo desde segundo | Nacimiento, reintegro; certificado para extensión | Certificado renovable trimestralmente; hasta 24 meses de edad | Tiempo trabajado remunerado | Una solicitud recurrente con instantánea diaria de jornada. Guía MTESS: inicial hasta 7 meses de edad. La redacción legal de seis meses desde reintegro se conserva como supuesto especial de revisión jurídica HR; no se decide silenciosamente esa discrepancia. Un intervalo diario continuo; fraccionamiento intradiario requiere registro futuro específico. |
| CONTROL_PRENATAL — Control prenatal | 7383/2024, 4–6 | Hasta 4 h/control; gestante | Constancia posterior; aval médico para más tiempo | 2 días de aviso salvo urgencia; sin cupo anual inventado | Horas trabajadas | Permite ampliación certificada y urgencia; revisión obligatoria, no rechazo discrecional. Publicación 20/12/2024; contrastar reglamentación en producción. |
| PAP_MAMOGRAFIA — PAP/mamografía | 3803/2009, 1–2; 6211/2018 | 2 días laborales/año en conjunto; trabajadora | Constancia de examen | No dos días adicionales por cada examen | Remunerado | Reserva/consumo agregado por persona/año; no se excluye exclusivamente por Sexo. Verificar documentación y alcance excepcional. |
| PREVENCION_PROSTATA_TESTICULOS_PENE_COLON — Prevención | 6280/2019, 13 modificado por 7659/2026 | 2 días laborales/año, único cupo | Examen controlado y constancia, sin diagnóstico | Colon para funcionaria conserva 2 días | Remunerado | Plafond común sin multiplicar por órgano ni reducir por sexo. Publicación 22/05/2026; solicitudes retroactivas anteriores requieren revisión del régimen temporal, sin recalcular solicitudes históricas. |
| ACOMPANAMIENTO_DISCAPACIDAD — Acompañamiento | 7371/2025, 17 | Hasta 50% de jornada mensual; madre/padre/cuidador de niño/adolescente | Cuidado y atención acreditados; sin diagnóstico detallado | Necesidad concreta revisada HR | Con sueldo | Horas y límite derivados del calendario/jornada vigente del mes, no cuota fija. Validar tratamiento reglamentario de feriados y cambios de vínculo dentro del mes. |
| OBLIGACION_LEGAL — Obligación legal | CL 62(h) | 2 días/mes, 15/año; trabajador obligado | Citación/disposición y respaldo | Según obligación | Remunerado dentro del tope | Días u horas; cada fracción se divide por la jornada de su fecha y se guarda. Validar convencionalmente esa conversión para jornadas variables. No duplica consumo al aprobar. |
| BUSQUEDA_EMPLEO — Búsqueda de empleo | CL 87–89 | 2 h/día o 1 día/semana; notificado de despido | Preaviso escrito verificado HR y modalidad exclusiva | Solo dentro del período verificado | Sin reducción salarial | Registro HR de preaviso no crea solicitud; empleado la inicia. Modalidades no combinables. Acumulación continuada prevista en art. 89 no automatizada; requiere criterio jurídico y alcance adicional explícito. |
| REPOSO_MEDICO — Reposo médico | Régimen de ausencias médicas existente | Según constancia | Comunicación propia y documentación HR | Según caso | No se presupone 100% | Enlace al autoservicio de ausencias; backend rechaza duplicarlo como permiso. No calcula IPS. |
| PERMISO_SIN_GOCE — Ordinario sin goce | Circuito ordinario, no categoría legal especial | Horas/días; empleado | Motivo y circuito | Sujeto a resolución | Sin goce | Conserva circuito configurable y no calcula descuentos. |

Se mantiene `PERMISO_ORDINARIO_CON_GOCE` para no eliminar la funcionalidad genérica preexistente. No se lo presenta como un derecho legal nuevo ni como `PERMISO_CONTRACTUAL`.

## Arquitectura y controles

- Catálogo vigente global versionado en PostgreSQL; `LegalPermissionCatalog` conserva la base original y compatibilidad. Cada solicitud conserva código, JSON de la regla/condiciones/documentos/remuneración, datos del supuesto y Sexo administrativo al presentar. Futuras modificaciones de catálogo o persona no recalculan registros anteriores.
- Continúan `permission_request`, su detalle diario, pasos y actores. Para derechos protegidos se reutilizan solo los niveles HR del mismo circuito; para ordinarios se mantiene Jefe → HR. No hay motor paralelo. No se permite autoaprobar.
- Un derecho con requisitos verificados no puede rechazarse discrecionalmente. HR registra verificación o incumplimiento fundamentado antes de resolver. Adjuntos limpios o un plazo expresamente justificado permiten documentación posterior; el plazo queda en el historial, no se inventa automáticamente.
- Solo `Employee` autenticado y su vínculo activo presentan solicitudes. Administrador/Jefe/HR carecen de operación de presentación por terceros. Las ausencias operativas existentes permanecen separadas.
- Creación serializable con bloqueo de tablas; las solicitudes pendientes reservan una vez, aprobadas mantienen la misma contabilización, canceladas/rechazadas quedan excluidas. Cuotas anuales por persona evitan duplicarlas con distintos legajos del mismo grupo.
- Calendario y jornadas efectivos reutilizados. Instantáneas conservan minutos, fracción de día e intervalo diario; ningún permiso escribe en el ledger de vacaciones.
- Documentos en `bytea` tenant, validador existente PDF/JPEG/PNG, 5 MB, SHA-256 y ClamAV. El empleado carga sobre su solicitud; HR revisa y descarga solo archivos `Clean` dentro de sus empresas. Jefe y administrador no acceden por su rol. URLs sin tenant suministrado por cliente, descarga sin caché; no se envía tipo de examen/diagnóstico/comentario sensible por email.

## Auditoría funcional y presentación del 24/11/2026

Las fichas global/tenant abren en modal accesible con duración y límites destacados. Los parámetros numéricos de reglas especializadas se comparten con sus validadores; las categorías simples muestran los campos estructurados de su versión publicada. No se usa un texto fijo independiente como duración autorizable.

El saldo e historial se derivan de `permission_request` y sus fechas/estados mediante `PermissionQuotaQueries`, compartido por validación y consulta autorizada. Aprobadas son utilizadas/comprometidas; pendientes reservan; rechazadas y canceladas no ocupan cupo. Se conserva la separación de las dos categorías preventivas y la identidad de persona entre legajos. Se añade la acumulación diaria que faltaba para impedir dobles solicitudes horarias no superpuestas de lactancia y búsqueda de empleo. No se crean otro ledger ni migraciones, ni se reescriben datos históricos.

La inspección de A/QA27 no encontró solicitudes legales preexistentes; las pruebas PostgreSQL usan datos sintéticos. Escenarios, resultados de suite, verificaciones de navegador y limitaciones se registran en [ESTADO_PROYECTO.md](ESTADO_PROYECTO.md).

## Sexo e importación

`person.Sex` admite únicamente M (Masculino) y F (Femenino) como valores administrativos. Para personas históricas cuyo dato falta se permite temporalmente `NULL` como situación técnica de migración, sin crear una tercera categoría. No existe valor predeterminado. Las altas nuevas exigen selección explícita M/F; jamás se deduce del nombre. La ficha muestra el dato faltante como pendiente de completar y permite registrarlo solo a `TenantAdministrator`, con motivo, actor, fecha y valores anterior/nuevo en `person_sex_change`; el valor anterior puede ser `NULL`.

La migración correctiva `PersonSexNullableHistorical` elimina el default anterior y convierte exclusivamente `person.Sex = 'ND'` introducido por la migración previa a `NULL`. No modifica M/F, otros campos personales, solicitudes ni instantáneas históricas. La migración ya aplicada permanece como evidencia histórica; la corrección se agrega después. No se ofrece una reversión automática que reinvente un valor para los datos faltantes.

Verificación de la corrección: `20261123180532_PersonSexNullableHistorical` aplicada en A y QA27; un registro histórico convertido en A, ninguno en QA27. Columna nullable sin default y cero ND en ambos. Dieciséis controles de integridad de datos coinciden antes/después, exceptuando exclusivamente el campo corregido. Build Release sin errores/advertencias y 70 pruebas superadas, incluyendo migración ND → NULL, conservación M/F, rechazo de categorías inválidas, altas/Excel y completado administrativo autorizado.

Plantilla `Funcionarios`: columna M `Sexo`, validación M/F y errores por fila. Para personas nuevas exige columna/valor; personas existentes conservan su dato durante reingresos/importaciones. Se mantienen las dos hojas y la estructura de `Vacaciones`; transacción única del lote. No se envían invitaciones por importar.

## Rutas de prueba

- Administración: `/tenant/funcionarios` → Nuevo funcionario; Ficha → Datos generales → Guardar sexo con motivo.
- Plantilla: `/tenant/importacion` → Descargar plantilla → completar Sexo M/F.
- Empleado: `/empleado/permisos` → Tipo → datos pertinentes → Calcular (cupos) → Presentar → Detalle (documentación/revisión).
- HR: `/tenant/solicitudes` → Permisos → Resolver/Ver circuito → Verificación HR → resolver en circuito existente.
- Preaviso: `/tenant/permisos/preaviso`, exclusivo HR; luego empleado solicita búsqueda de empleo.
- Reposo: `/empleado/ausencias`.

## Validación antes de producción

Revisión jurídica especializada de las cuestiones señaladas en la matriz y vigencia exacta de cada norma/reglamento. No se declara validación legal integral. No hay liquidación salarial, subsidios, payroll, microservicios ni categorías excluidas. No se modifican iMatch ni el sitio Astro.
