# HR ProTools — sitio institucional

Sitio independiente, estático, en español, construido con Astro, TypeScript y CSS. Todo el proyecto permanece en `C:\HRProTools\site`; la aplicación C#/Blazor y PostgreSQL no se modifican.

## Ejecutar

Requiere Node compatible con Astro 7 (el desarrollo se verificó con Node 24.15.0) y npm.

```sh
cd C:\HRProTools\site
npm ci
npm run dev
```

`npm run check` verifica Astro/TypeScript. `npm run build` genera `site/dist`. `npm run verify` comprueba las nueve páginas generadas, enlaces y recursos locales, vistas ilustrativas y formulario deshabilitado. `npm run preview` permite revisar la compilación estática.

## Páginas

- `/`: Inicio, funcionalidades, tres vistas del producto, multiempresa y bloque final con login a la izquierda y demo a la derecha.
- `/funcionalidades/`
- `/planes-y-precios/`: propuestas, sin tarifas ni límites inventados.
- `/rse/`: principios, sin certificaciones ni resultados atribuidos.
- `/preguntas-frecuentes/`: acordeones nativos accesibles.
- `/contacto/`: formulario comercial preparado y deshabilitado.

También existen `/acceso/`, `/privacidad/` y `/terminos/` para evitar enlaces vacíos o destinos inexistentes. Los textos legales son avisos de estado, no políticas aprobadas.

## Configuración del login

Copiar `.env.example` a `.env` dentro de `site` y asignar `PUBLIC_APP_LOGIN_URL` a la URL existente de login. Se admiten HTTPS y HTTP exclusivamente para localhost/127.0.0.1. No poner credenciales en esta variable pública. Astro incorpora el valor al compilar; reconstruir después de cambiarlo. El valor por defecto es `https://app.hrprotools.com/Account/Login`. Si se configura un valor inválido, el enlace lleva a `/acceso/`. No se implementa autenticación.

## Contacto y privacidad

No hay backend ni servicio de entrega. El formulario no envía, no almacena datos y no muestra mensajes de éxito. Los campos y el botón están deshabilitados y existe un aviso visible. El contacto comercial autorizado es `info@hrprotools.com`, publicado también como enlace mailto. No se inventaron teléfono ni identidad corporativa.

Para habilitarlo en una entrega posterior se deben aprobar responsable y política de privacidad, canal comercial y mecanismo real de entrega con validación y protección contra abuso. Las cantidades de funcionarios del selector son opciones de contacto, no límites de planes.

## Recursos y referencias

`public/images/logo.png` es una copia intacta de `mockup/logo.png`. No se rediseñó el logo. La composición sigue `mockup/site.png` y `mockup/site2.png`: navy, amarillo, blanco y gris claro, header corporativo y tarjetas redondeadas.

`ProductPreview.astro` construye tres representaciones HTML/CSS: dashboard, funcionarios y circuito de solicitudes. Se basan en las funciones y componentes existentes consultados en modo lectura. No son capturas reales ni controles de la aplicación; cada vista se identifica como ilustrativa. Nombres, empresas y métricas son ficticios. No contiene datos salariales, médicos, correos ni datos de clientes. Su formato HTML evita imágenes de texto poco legibles y se adapta a la pantalla.

No se usan fuentes remotas, analítica, cookies de seguimiento, CMS, React, bases de datos ni nuevas APIs. El único JavaScript del sitio controla el menú móvil; los acordeones usan `details/summary`.

## Alcance de entrega

El usuario autorizó el 26/11/2026 el commit/push y deployment del sitio junto con la aplicación. Los originales de `mockup` permanecen intactos. La compilación estática se sirve con Nginx, sin proceso Node permanente.

## Verificación de esta entrega — 22/11/2026

- `npm install`: 272 paquetes instalados, auditoría sin vulnerabilidades.
- `npm run check`: 25 archivos, 0 errores, 0 advertencias, 0 sugerencias.
- `npm run build`: salida estática correcta, nueve páginas (seis principales y tres auxiliares).
- `npm run verify`: 220 enlaces/recursos locales válidos y comprobación de las tres vistas, el CTA y el formulario sin envío.
- Compilación separada en `qa/login-build` con una URL ilustrativa: el login configurado se incorpora correctamente. La build principal conserva el destino informativo `/acceso/` hasta definir la URL real.
- Vista de navegador a 1280 px y 390 px: navegación por las seis páginas principales, logo cargado y sin desbordamiento horizontal. Control adicional de preguntas frecuentes a 320 px.
- Menú móvil operativo; acordeón abierto con clic y cerrado con Enter; botón de envío deshabilitado.
- Comprobación de disposición: ingreso a la izquierda y demostración a la derecha, en la misma fila en escritorio.
- Comparación visual con los mockups: se conservan la composición del hero, navy/amarillo, tarjetas, navegación y jerarquía. Se utiliza el logo oficial, aunque el símbolo representado en los mockups sea distinto. Las vistas de producto son HTML/CSS, sin fotografías de stock.
- SHA-256 del logo copiado coincide con el original: `F402C587C8423731509B7D65AA5F498122919A2259E9F15B14EC268452F621E0`.
- Capturas locales de comprobación: `qa/home-desktop.jpg` y `qa/home-mobile.jpg` (ignoradas por Git).
- `git diff --name-only` sin resultados: ningún archivo previamente versionado fue modificado. Los únicos archivos nuevos propios de esta entrega están en `site/`; `mockup/site.png` y `mockup/site2.png` ya eran archivos no versionados al inicio y permanecen intactos.

Pendientes de definición comercial: URL pública de login, canal real para demostraciones, identidad y documentos legales aprobados, precios y condiciones de los planes. La interfaz informa esas limitaciones sin simular envíos ni publicar tarifas ficticias.
