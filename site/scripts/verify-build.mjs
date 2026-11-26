import { readFile, readdir, access } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve('dist');
async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = await Promise.all(entries.map(entry => entry.isDirectory() ? walk(join(dir, entry.name)) : [join(dir, entry.name)]));
  return files.flat();
}
const pages = (await walk(root)).filter(path => path.endsWith('.html'));
assert.equal(pages.length, 9, 'Se esperan seis páginas principales y tres páginas auxiliares');
let links = 0;
for (const path of pages) {
  const html = await readFile(path, 'utf8');
  assert.match(html, /lang="es-PY"/);
  assert.match(html, /href="mailto:info@hrprotools\.com"/);
  assert.match(html, /href="https:\/\/app\.hrprotools\.com\/Account\/Login"/);
  assert.equal((html.match(/<h1[ >]/g) ?? []).length, 1, `Un h1 por página: ${path}`);
  for (const [, target] of html.matchAll(/(?:href|src)="(\/[^"#?]*)/g)) {
    assert(!target.startsWith('//'), 'No se admiten recursos remotos implícitos');
    const local = resolve(root, `.${target}`);
    assert(local.startsWith(root), 'Ruta dentro de dist');
    await access(target.endsWith('/') ? join(local, 'index.html') : local);
    links++;
  }
}
const home = await readFile(join(root, 'index.html'), 'utf8');
for (const screen of ['dashboard', 'funcionarios', 'circuito']) assert(home.includes(`product-preview ${screen}`));
assert(home.includes('Representación ilustrativa'));
assert(home.indexOf('¿Ya sos cliente?') < home.indexOf('¿Querés conocer HR ProTools?'));
const contact = await readFile(join(root, 'contacto/index.html'), 'utf8');
assert.match(contact, /<fieldset disabled/);
assert.match(contact, /Envío web aún no habilitado/);
assert(!/<form[^>]*action=/.test(contact));
console.log(`OK: ${pages.length} páginas, ${links} enlaces/recursos locales, tres vistas ilustrativas y formulario sin envío.`);
