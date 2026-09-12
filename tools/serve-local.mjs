import http from 'node:http';
import path from 'node:path';
import { realpath, stat, readFile } from 'node:fs/promises';

// Explicit root; localhost only. No directory listing, writes or parent access.
const root = await realpath(path.resolve(process.argv[2] || '.'));
const port = Number(process.argv[3] || 8767);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid local port');
const types = {'.html':'text/html; charset=utf-8','.css':'text/css','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.png':'image/png','.jpg':'image/jpeg','.svg':'image/svg+xml','.wav':'audio/wav','.wasm':'application/wasm'};
http.createServer(async (request, response) => {
  try {
    if (request.method !== 'GET' && request.method !== 'HEAD') { response.writeHead(405).end(); return; }
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    let candidate = path.resolve(root, '.' + pathname);
    const inside = value => value === root || value.startsWith(root + path.sep);
    if (!inside(candidate)) throw new Error('Outside served root');
    if ((await stat(candidate)).isDirectory()) candidate = path.join(candidate, 'index.html');
    candidate = await realpath(candidate);
    if (!inside(candidate)) throw new Error('Outside served root');
    const bytes = await readFile(candidate);
    response.writeHead(200, {'Content-Type':types[path.extname(candidate)] || 'application/octet-stream','Content-Length':bytes.length,'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});
    response.end(request.method === 'HEAD' ? undefined : bytes);
  } catch { response.writeHead(404, {'Content-Type':'text/plain'}).end('Not found'); }
}).listen(port, '127.0.0.1', () => process.stdout.write(`Serving ${root} at http://127.0.0.1:${port}\n`));
