import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
let version = readFileSync(resolve(root, 'CallMonitor/CallMonitor.csproj'), 'utf8').match(/<Version>([^<]+)<\/Version>/)?.[1];
let notes = 'Actualizaciones desde R2 con descarga verificada, reemplazo del ejecutable y reinicio.';
for (let index = 0; index < args.length; index += 2) {
  if (!args[index + 1]) throw new Error('Falta el valor de ' + args[index]);
  if (args[index] === '--version') version = args[index + 1];
  else if (args[index] === '--notes') notes = args[index + 1];
  else throw new Error('Opción desconocida: ' + args[index]);
}
if (!/^\d+\.\d+\.\d+$/.test(version || '')) throw new Error('La versión debe tener formato 0.1.1.');
if (!notes || notes.length > 4000) throw new Error('Notas de versión inválidas.');
execFileSync('dotnet', ['publish', resolve(root, 'CallMonitor/CallMonitor.csproj'), '-c', 'Release', '-r', 'win-x64',
  '--self-contained', 'false', '-p:PublishSingleFile=true', '-p:AppEnvironment=Production', '-p:Version=' + version, '--nologo'], { cwd: root, stdio: 'inherit' });
const executable = resolve(root, 'CallMonitor/bin/Production/Release/net10.0-windows/win-x64/publish/CallMonitor.exe');
const hash = createHash('sha256');
for await (const chunk of createReadStream(executable)) hash.update(chunk);
const manifest = { version, downloadUrl: 'https://cdn.frankgp.com/call/CallMonitor.exe', sha256: hash.digest('hex').toUpperCase(), releaseNotes: notes };
const output = resolve(root, './docs/r2/CallMonitorLatest.json');
mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, JSON.stringify(manifest, null, 2) + '\n');
console.log('Ejecutable: ' + executable);
console.log('JSON listo para subir manualmente: ' + output);
console.log('Publica primero CallMonitor.exe y después CallMonitorLatest.json.');
