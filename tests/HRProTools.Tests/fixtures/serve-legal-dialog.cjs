// Serves only rendered synthetic test markup and the application's actual CSS/JS. No database or workers.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const files = new Map([
    ['/', ['artifacts/legal-ui/index.html', 'text/html']],
    ['/frame', ['artifacts/legal-ui/index.html', 'text/html']],
    ['/app.css', ['src/HRProTools.Web/wwwroot/app.css', 'text/css']],
    ['/HRProTools.Web.styles.css', ['src/HRProTools.Web/obj/Release/net10.0/scopedcss/bundle/HRProTools.Web.styles.css', 'text/css']],
    ['/Components/Shared/LegalPermissionDialog.razor.js', ['src/HRProTools.Web/Components/Shared/LegalPermissionDialog.razor.js', 'text/javascript']]
]);
http.createServer((req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1');
    res.setHeader('Cache-Control', 'no-store');
    if (url.pathname === '/mobile') {
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end('<!doctype html><html lang="es"><title>Prueba móvil 375 px</title><body style="margin:0;background:#e8edf3"><p>Viewport móvil real: 375 × 740 píxeles</p><iframe title="Aplicación de prueba móvil" src="/frame" style="display:block;width:375px;height:740px;border:1px solid #08243c"></iframe></body></html>');
        return;
    }
    const file = files.get(url.pathname);
    if (!file) { res.writeHead(404); res.end(); return; }
    try {
        const content = fs.readFileSync(path.join(root, file[0]));
        res.writeHead(200, { 'Content-Type': `${file[1]}; charset=utf-8` }); res.end(content);
    } catch (error) { res.writeHead(500); res.end('Generate the synthetic HTML fixture with LegalDialogAndDurationRenderStructuredPublishedValues first.'); }
}).listen(5179, '127.0.0.1', () => console.log('Synthetic legal-dialog fixture: http://127.0.0.1:5179'));
