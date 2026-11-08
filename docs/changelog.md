# Registro de cambios de IMG Viewer

## Funciones incorporadas en las etapas 1 a 5

IMG Viewer es un visor de imágenes para Windows hecho con WPF y .NET 10. Permite recorrer una carpeta, hacer ajustes rápidos y preparar capturas para compartir.

### Visor y controles

- Apertura de JPG, JPEG, PNG y BMP al arrastrarlos a la ventana o desde **Abrir con** en el Explorador.
- Navegación por nombre con las flechas o la rueda. Presentación en bucle en pantalla completa, con reproducción y pausa y un intervalo de tres segundos.
- Doble clic en la foto para alternar la pantalla completa. **Ctrl + rueda** amplía o reduce la imagen y **Ctrl + arrastre izquierdo** la desplaza cuando está ampliada.
- Ventana sin barra de título, con borde gris fino, sombra nativa y botones flotantes. Arrastrar con el botón izquierdo sobre la foto o el fondo mueve la ventana.
- **T** alterna el fondo oscuro y el damero para revisar transparencias. El fondo del visor no forma parte de la imagen.
- El botón de información abre un pie con nombre, posición, dimensiones, tamaño de archivo y datos EXIF disponibles, como fecha de captura, cámara e ISO.

### Edición y guardado

- **R** gira 90° en sentido horario y **Shift + R** en sentido antihorario.
- Recorte libre y enderezado de −15° a +15° en una misma vista previa, con tiradores y cuadrícula de tercios.
- **Alt + arrastre izquierdo** dibuja una flecha. **Shift + arrastre izquierdo** dibuja un rectángulo. **Ctrl + Z** deshace la última marca pendiente.
- **Esc** descarta las ediciones pendientes. Si hay un trazo en curso, cancela primero ese trazo.
- El disquete ámbar indica que hay algo por guardar. **Ctrl + S** guarda en el archivo actual y **Ctrl + Shift + S** guarda una copia. **Ctrl + C** copia la imagen con las ediciones visibles.
- Al guardar una imagen editada, el archivo se vuelve a codificar y puede perder metadatos EXIF.

### Papelera

- **Supr** envía la foto actual a la Papelera y muestra la siguiente disponible. Si era la última, muestra la anterior.
- Durante cinco segundos aparece un botón rojo en la esquina inferior derecha. Parpadea suavemente cuando las animaciones de Windows están activas y cambia a una flecha de deshacer al pasar el mouse. Un clic restaura la foto.
- **Shift + Supr** restaura la última foto enviada desde el visor durante la sesión, aunque el botón ya haya desaparecido.

### Captura de pantalla

- **Impr Pant** inicia la captura desde otra aplicación mientras IMG Viewer está abierto. Si Windows abre Recortes con esa tecla, desactiva esa asignación antes de usarla en el visor.
- La selección se hace sobre una instantánea congelada. Al mover el cursor se resalta el marco visible de la ventana, al hacer clic se captura esa ventana y al arrastrar se selecciona una región manual.
- **Pantalla completa** captura el monitor seleccionado y **Esc** cancela. La captura se abre en el visor para marcarla, copiarla o guardarla. Si la captura todavía no tiene archivo, **Ctrl + S** pide un nombre.

## Cambios posteriores

### 2026-11-07 · Renombrar imágenes

- **F2** permite renombrar el archivo de la imagen abierta sin cambiar su extensión ni descartar las ediciones pendientes. Si el nombre está ocupado, el visor pide otro.

### 2026-11-07 · Capturas al portapapeles

- La casilla **Copiar las capturas al portapapeles** copia cada captura confirmada además de guardarla y abrirla en el visor. Viene desactivada y la preferencia se conserva al cerrar la app.

### 2026-11-07 · Redimensionado proporcional

- En modo Imagen, mantener **Ctrl** al arrastrar un borde o una esquina ajusta la ventana a la proporción de la foto actual. Al soltar **Ctrl**, el tamaño vuelve a ser libre.

### 2026-11-07 · Controles al hacer clic

- Un clic izquierdo sobre la foto o el fondo del visor muestra u oculta el pie de metadatos y la barra superior. El arrastre sigue moviendo la ventana y el doble clic alterna la pantalla completa.
- En la cuadrícula, los botones superiores quedan accesibles y un clic en una miniatura abre la foto.

### 2026-11-07 · Gestos del mouse

- Arrastrar con el botón izquierdo sobre la foto o el fondo del visor mueve la ventana. El doble clic sobre la foto sigue alternando la pantalla completa.
- El clic central alterna entre Imagen y Cuadrícula, igual que **G**. Girar la rueda cambia de foto en el visor y desplaza la cuadrícula.

### 2026-11-07 · Vista de cuadrícula

- **G** o el ícono de cuatro cuadros alterna entre el visor y una cuadrícula con miniaturas cuadradas. Las miniaturas llenan su celda y las fotos originales conservan su encuadre.
- La cuadrícula abre la carpeta de la foto actual. Si la foto todavía no tiene archivo o el visor está vacío, muestra la carpeta configurada para las capturas.
- Las fotos se ordenan por nombre, la rueda desplaza la cuadrícula y un clic abre la foto seleccionada. La foto actual se resalta.
- La ruta de la carpeta se muestra en un pie compacto. Si se trunca, puedes verla completa al pasar el mouse.
- El espacio libre del encabezado permite mover la ventana con arrastre izquierdo.
- En **Configuración → Cuadrícula**, elige entre una y ocho imágenes por fila. El valor inicial es tres y la preferencia se guarda.

### 2026-11-07 · Bandeja e instancia única

- **Ocultar en la bandeja al cerrar** viene activado. Al pulsar ×, el visor se oculta y **Impr Pant** sigue disponible. Si desactivas la opción, × cierra la app.
- El menú del ícono junto al reloj permite abrir el visor, capturar la pantalla o salir de la app.
- **Iniciar con Windows en la bandeja** es opcional y viene desactivado. Al activarlo, el visor arranca oculto al iniciar sesión.
- Abrir otra imagen desde el Explorador reutiliza la instancia activa, incluso si está oculta en la bandeja.

### 2026-11-07 · Guardado automático de capturas

- Al confirmar una captura, IMG Viewer la guarda como JPG en Descargas y la abre en el visor. Usa el formato `screen-261107-223518.jpg` y añade un sufijo numérico si el nombre ya existe.
- El engranaje permite cambiar la carpeta de capturas o restablecer Descargas. La elección se conserva al cerrar la app.
- Si el guardado automático falla, la captura se abre sin archivo para guardarla manualmente con **Ctrl + S**.


Añadir cada cambio visible para el usuario con su fecha, el comportamiento y el atajo, si lo tiene. Registrar por separado las correcciones y las limitaciones relevantes. Para el futuro README y la landing page, conservar ejemplos de uso y capturas que muestren la función en acción.
