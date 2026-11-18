# Nube con tu Cloudflare R2

Nube es opcional y permanece desactivada hasta guardar una conexión. Cada usuario utiliza su propio bucket; IMG Viewer no aporta almacenamiento ni utiliza las credenciales de publicación de la app.

## Conectar

1. En Cloudflare R2, prepara un bucket para tus imágenes y crea credenciales de la API compatible con S3 con permiso **Object Read & Write**, limitado a ese bucket.
2. En IMG Viewer, abre **Editar → Configuración → Nube**.
3. Activa **Nube** y completa endpoint S3, bucket, Access Key ID y Secret Access Key. El endpoint tiene la forma `https://ACCOUNT_ID.r2.cloudflarestorage.com`, sin el nombre del bucket.
4. Elige una carpeta remota. El valor inicial es `img-viewer/images/`. Usa una carpeta distinta de tus ejecutables y manifiestos de publicación.
5. Pulsa **Probar conexión** y después **Guardar conexión**. La comprobación valida la lectura; subir y eliminar también necesitan permisos de escritura.

Las credenciales se guardan con DPAPI de Windows para el usuario actual, en `%LocalAppData%\IMGViewer\cloud-r2.dat`. No se guardan en texto plano ni en el repositorio. No compartas ese archivo como configuración portable: está protegido para tu usuario/equipo. Para desactivar la función, desmarca Activar Nube y guarda; las imágenes remotas se conservan.

Documentación de Cloudflare: [credenciales y conexión S3](https://developers.cloudflare.com/r2/get-started/s3/).

## Subir

Usa el icono **Subir a Nube**, Archivo → Subir a Nube o el menú contextual. En modo cuadrícula puedes subir la selección completa.

- Archivos sin cambios: se conserva el formato y contenido original.
- Capturas o imágenes editadas: se sube una copia PNG con las ediciones visibles y transparencia. El archivo local y su estado de guardado no se modifican.
- Cada subida genera un nombre único; no sobrescribe imágenes existentes.
- Hay progreso, cancelación y reintento de los elementos pendientes. El límite por imagen es 128 MB.
- Si activas Copiar enlace después de subir, se copia el enlace de la última imagen subida correctamente.

Las subidas son manuales: guardar localmente o tomar una captura no sube nada por sí solo. Una interrupción después de que R2 haya recibido una imagen puede dejar esa imagen subida aunque la app no reciba la confirmación; actualizar la pestaña permite comprobarlo antes de reintentar.

## Pestaña Nube

La galería muestra únicamente imágenes de la carpeta remota configurada y sus subcarpetas. Carga páginas de 50 objetos; **Cargar más** continúa el listado. Las miniaturas se descargan al entrar en pantalla, con hasta tres solicitudes simultáneas. Archivos de más de 16 MB muestran un icono en lugar de descargar una miniatura automáticamente.

- **Doble clic / Abrir:** abre una copia editable en el visor, conservada como borrador. Guardar crea una copia local; no cambia el objeto remoto.
- **Descargar:** conserva el archivo remoto original en el destino que elijas.
- **Copiar enlace:** copia un enlace para compartir.
- **Eliminar de Nube:** pide confirmación; elimina solo la imagen remota. No se puede deshacer y los archivos locales se conservan.
- **Actualizar:** vuelve a consultar R2.

## Enlaces privados o públicos

Deja **URL pública del bucket** vacía para usar enlaces privados firmados de **una hora**. No hace falta habilitar acceso público al bucket. Quien recibe el enlace puede abrir esa imagen mientras sea válido.

Si ya tienes un dominio público para ese bucket, introduce la URL raíz, por ejemplo `https://imagenes.example.com/`. Se copiarán enlaces permanentes a ese dominio. Esta opción no activa ni desactiva la privacidad en Cloudflare. No introduzcas una URL firmada ni credenciales en ese campo.

Los enlaces privados utilizan el endpoint S3, no el dominio público. Referencia: [URLs firmadas de R2](https://developers.cloudflare.com/r2/api/s3/presigned-urls/).

## Verificación

```powershell
dotnet run --project tests/IMGViewer.Cloud.Tests/IMGViewer.Cloud.Tests.csproj -c Release
```

Las pruebas usan credenciales ficticias, archivos temporales y un servidor HTTP simulado: firma SigV4 con una referencia independiente, enlaces, paginación, alcance de carpeta, subida, descarga, borrado, errores, cancelación y cifrado de credenciales. No acceden a R2 real.

Se revisaron visualmente Configuración y la galería con miniaturas sintéticas. Falta una comprobación con un bucket real configurado por el usuario; no se han utilizado las credenciales de `release` ni subido imágenes de prueba a su cuenta.
