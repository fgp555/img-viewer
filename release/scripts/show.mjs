import {remoteRelease} from './deploy.mjs';
try {
  const manifest=await remoteRelease();
  if(!manifest) { console.log('Aun no hay publicaciones (latest.json responde 404).'); }
  else {
  console.log('Version publicada: '+manifest.version);
  console.log('Notas: '+manifest.releaseNotes);
  console.log('Descarga: '+manifest.downloadUrl);
  console.log('SHA-256: '+manifest.sha256);
  }
} catch(error) {
  console.error(error instanceof TypeError?'No se pudo consultar la version publicada. Comprueba la conexion.':error.message);
  process.exitCode=1;
}
