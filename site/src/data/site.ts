const candidate = import.meta.env.PUBLIC_APP_LOGIN_URL?.trim() || 'https://app.hrprotools.com/Account/Login';
export const contactEmail = 'info@hrprotools.com';
function validLogin(value: string | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return !url.username && !url.password && (url.protocol === 'https:' || (url.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(url.hostname))) ? url.href : null;
  } catch { return null; }
}
export const loginUrl = validLogin(candidate) ?? '/acceso/';
export const loginConfigured = validLogin(candidate) !== null;
export const navigation = [
  ['/', 'Inicio'], ['/funcionalidades/', 'Funcionalidades'], ['/planes-y-precios/', 'Planes y precios'],
  ['/rse/', 'RSE'], ['/preguntas-frecuentes/', 'Preguntas frecuentes'], ['/contacto/', 'Contacto'],
];
export const features = [
  { icon: 'calendar', title: 'Gestión de vacaciones', text: 'Períodos, saldos y solicitudes en un mismo lugar. Un calendario laboral para acompañar cada solicitud.' },
  { icon: 'clipboard', title: 'Permisos con y sin goce', text: 'Solicitudes por días o por horas, con un circuito de revisión y aprobación definido.' },
  { icon: 'heart', title: 'Ausencias', text: 'Declaración, revisión y seguimiento de ausencias. La clasificación final queda en manos de Recursos Humanos.' },
  { icon: 'flow', title: 'Circuitos de aprobación', text: 'Organizá niveles y responsables. Cada solicitud avanza por las etapas que le corresponden.' },
  { icon: 'building', title: 'Administración multiempresa', text: 'Empresas, departamentos, cargos y funcionarios de un mismo grupo, organizados desde un entorno común.' },
  { icon: 'chart', title: 'Alertas y reportes', text: 'Consultá pendientes y alertas operativas. Exportá reportes de permisos a Excel según tu alcance autorizado.' },
];
export const faqs = [
  ['¿Qué es HR ProTools?', 'Es una plataforma para organizar procesos de Recursos Humanos: funcionarios, vacaciones, permisos, ausencias y circuitos de aprobación.'],
  ['¿Permite administrar varias empresas de un mismo grupo?', 'Sí. La estructura contempla grupos empresariales con empresas, departamentos y cargos. Los accesos se limitan según el rol y el alcance configurado.'],
  ['¿Los funcionarios pueden solicitar sus vacaciones?', 'Sí. Desde su espacio personal pueden consultar sus períodos y saldos y presentar una solicitud para que siga el circuito de aprobación.'],
  ['¿Se pueden solicitar permisos por horas?', 'Sí. Se contemplan permisos por días o por horas, con y sin goce, considerando la jornada laboral configurada.'],
  ['¿Cómo funcionan las aprobaciones?', 'Las solicitudes siguen niveles y responsables configurados. La bandeja de cada autorizador muestra las solicitudes habilitadas para su intervención.'],
  ['¿Cómo se registran las ausencias?', 'El funcionario puede declarar una ausencia. Los usuarios operativos autorizados pueden registrarla dentro de su alcance y Recursos Humanos realiza la clasificación final.'],
  ['¿Qué reportes puedo consultar?', 'El dashboard presenta información según el rol. Recursos Humanos puede exportar un reporte de permisos a Excel con filtros por empresa, departamento, fechas y estado.'],
  ['¿Cómo se controla el acceso a la información?', 'El sistema separa los grupos empresariales y aplica roles y alcances. Un permiso para autorizar solicitudes no concede por sí solo acceso salarial o a documentos médicos.'],
  ['¿Cómo solicito una demostración?', 'La sección Contacto presenta los datos necesarios para coordinarla. El envío web está pendiente de habilitación y se informa expresamente antes de completar el formulario.'],
];
