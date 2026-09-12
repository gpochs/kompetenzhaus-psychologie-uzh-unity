// Inspect a completed local Unity Web build. Read-only; no uploads or API calls.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

const root = fs.realpathSync(path.resolve(process.argv[2] || 'UnityProject/Build/WebGL'));
const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const issues = [];
if (/\{\{\{/.test(html)) issues.push('Unexpanded Unity template placeholders.');
if (!/createUnityInstance/.test(html)) issues.push('Unity loader missing.');
const shellFiles = ['shell.js', 'shell.css', 'bridge-contract.mjs', 'framework-view.mjs', 'shell-locale.mjs'];
const contentFiles = ['content/kompetenzhaus-content.json', 'content/module-learning-design.json', 'companion/published.json', 'companion/context-catalog.json'];
const required = [...shellFiles, ...contentFiles, 'TemplateData/cover.png'];
for (const file of required) if (!fs.existsSync(path.join(root, file))) issues.push('Missing ' + file);
for (const file of [...shellFiles, ...contentFiles]) {
  const source = path.join(sourceRoot, shellFiles.includes(file) ? 'web-template' : '', file);
  const built = path.join(root, file);
  if (fs.existsSync(built) && !fs.readFileSync(source).equals(fs.readFileSync(built))) issues.push('Build differs from current source: ' + file);
}
const template = fs.readFileSync(path.join(sourceRoot, 'web-template/index.html'), 'utf8').replaceAll('\r\n', '\n');
const escape = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const expandedTemplate = new RegExp('^' + template.split(/\{\{\{[^}]+\}\}\}/).map(escape).join('[^\\n]*') + '$');
if (!expandedTemplate.test(html.replaceAll('\r\n', '\n'))) issues.push('HTML differs from the current Unity template.');
const files = [];
function visit(folder) {
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const absolute = path.join(folder, entry.name);
    if (entry.isSymbolicLink()) { issues.push('Unexpected symbolic link.'); continue; }
    if (entry.isDirectory()) { visit(absolute); continue; }
    const relative = path.relative(root, absolute).replaceAll('\\', '/');
    const bytes = fs.readFileSync(absolute);
    if (/\.(docx?|pdf|blend|fbx|cs|pdb|ps1|env)$/i.test(relative)) issues.push('Unexpected source/private file: ' + relative);
    if (bytes.length >= 100 * 1024 * 1024) issues.push('File exceeds ordinary GitHub file limit: ' + relative);
    files.push({ path: relative, bytes: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex') });
    if (/\.wasm(?:\.unityweb|\.gz)?$/.test(relative)) {
      const decoded = bytes[0] === 0x1f && bytes[1] === 0x8b ? zlib.gunzipSync(bytes) : bytes;
      if (!decoded.subarray(0, 4).equals(Buffer.from([0, 97, 115, 109]))) issues.push('Invalid WebAssembly header: ' + relative);
    }
  }
}
visit(root);
if (!files.some(file => /\.wasm(?:\.unityweb|\.gz)?$/.test(file.path))) issues.push('WebAssembly player missing.');
if (!files.some(file => /\.data(?:\.unityweb|\.gz)?$/.test(file.path))) issues.push('Unity scene data missing.');
const totalBytes = files.reduce((sum, file) => sum + file.bytes, 0);
if (totalBytes > 1024 ** 3) issues.push('Build exceeds GitHub Pages site limit.');
console.log(JSON.stringify({ schema: 'kompetenzhaus.web-build-inspection.v1', passed: !issues.length, totalBytes, fileCount: files.length, issues, files,
  scope: 'File integrity and static packaging only. Browser, graphics, sound and learning interactions require a separate runtime check.' }, null, 2));
process.exitCode = issues.length ? 1 : 0;
