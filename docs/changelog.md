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

### 2026-11-08 · Ajuste de imagen con C

- En modo Imagen, **C** alterna entre **Contain**, que muestra toda la foto, y **Cover**, que llena la ventana desde el centro. El ajuste es visual y **Ctrl + C** sigue copiando la imagen.

### 2026-11-08 · Barra superior al pasar el mouse

- En modo Imagen, el clic simple abre o cierra el pie de metadatos. La barra superior permanece visible mientras el pie esté abierto. Con el pie cerrado, la barra aparece al acercar el mouse a la esquina superior derecha y se oculta al salir.
- En Cuadrícula, la barra superior se mantiene visible.

### 2026-11-08 · Cursor por inactividad

- En **Configuración → Cursor**, activa **Ocultar cursor por inactividad** y elige entre 1 y 30 segundos. El valor inicial es 3 segundos y la opción viene desactivada.
- El cursor se oculta sobre la ventana activa de IMG Viewer, incluidos la cuadrícula, la pantalla completa y los diálogos. Vuelve con actividad del mouse o al salir de la aplicación.
- En **Alcance**, elige **Solo IMG Viewer** o **Todo Windows**. El alcance global funciona también en la bandeja y restaura el cursor al recibir actividad del mouse, al desactivar la opción o al salir.

### 2026-11-08 · Acerca de IMG Viewer

- Configuración muestra la versión del ejecutable, **2026.11.1**, y un enlace a la página de IMG Viewer.

### 2026-11-08 · Iconos para cambiar de vista

- Desde Imagen, el botón muestra una cuadrícula. Desde Cuadrícula, muestra una foto para volver al visor.
- El selector de diseño muestra columnas desiguales en vista cuadrada y cuatro cuadros en Masonry, según el diseño que abrirá.

### 2026-11-08 · Metadatos de selección en la cuadrícula

- Con una foto seleccionada, el pie muestra el nombre, las dimensiones, el tamaño en disco y los datos EXIF disponibles.
- Con varias fotos seleccionadas, el pie muestra la cantidad y el tamaño total de los archivos.
- Los botones de copiar, redimensionar y enviar a la Papelera aparecen en el encabezado al seleccionar fotos.

### 2026-11-08 · Cuadrado, Masonry y desplazamiento táctil

- En la cuadrícula, el nuevo ícono alterna entre miniaturas cuadradas y **Masonry**, que conserva las proporciones y coloca cada foto en la columna más corta.
- La barra de desplazamiento se oculta. Puedes seguir usando la rueda, deslizar con el dedo o arrastrar con el botón izquierdo en el área de miniaturas.
- Un clic selecciona y un doble clic abre la foto. El arrastre del encabezado mueve la ventana.

### 2026-11-07 · Acciones en la cuadrícula

- Un clic selecciona una imagen. **Ctrl + clic** añade o quita imágenes de la selección y **Shift + clic** selecciona un rango. El doble clic abre la imagen.
- Al seleccionar imágenes aparecen botones para copiarlas como archivos, redimensionarlas o enviarlas a la Papelera. **Ctrl + C** permite pegarlas en el Explorador y **Supr** las envía a la Papelera.
- El redimensionado aplica el mismo criterio a cada imagen manteniendo su proporción. Guarda las copias en la misma carpeta con las dimensiones en el nombre y evita sobrescribir archivos existentes.
- El envío de varias imágenes a la Papelera pide confirmación. **Shift + Supr** recupera la última imagen enviada durante la sesión.

### 2026-11-07 · Redimensionar la imagen

- El botón **Redimensionar imagen** abre un panel con **×0,5**, **×2**, **Máx. 1000 px** y campos de ancho y alto vinculados. El máximo reduce el lado más largo solo si supera 1000 px.
- El panel muestra las dimensiones resultantes antes de aplicar. La edición queda pendiente y se puede guardar en el original, guardar como copia o descartar con **Esc**.

### 2026-11-07 · Renombrar imágenes

- **F2** permite renombrar el archivo de la imagen abierta sin cambiar su extensión ni descartar las ediciones pendientes. Si el nombre está ocupado, el visor pide otro.

### 2026-11-07 · Capturas al portapapeles

- La casilla **Copiar las capturas al portapapeles** copia cada captura confirmada además de guardarla y abrirla en el visor. Viene desactivada y la preferencia se conserva al cerrar la app.

### 2026-11-07 · Redimensionado proporcional

- En modo Imagen, mantener **Ctrl** al arrastrar un borde o una esquina ajusta la ventana a la proporción de la foto actual. Al soltar **Ctrl**, el tamaño vuelve a ser libre.

### 2026-11-07 · Controles al hacer clic

- Un clic izquierdo sobre la foto o el fondo del visor muestra u oculta el pie de metadatos. El arrastre sigue moviendo la ventana y el doble clic alterna la pantalla completa.
- En la cuadrícula, los botones superiores quedan accesibles y un doble clic en una miniatura abre la foto.

### 2026-11-07 · Gestos del mouse

- Arrastrar con el botón izquierdo sobre la foto o el fondo del visor mueve la ventana. El doble clic sobre la foto sigue alternando la pantalla completa.
- El clic central alterna entre Imagen y Cuadrícula, igual que **G**. Girar la rueda cambia de foto en el visor y desplaza la cuadrícula.

### 2026-11-07 · Vista de cuadrícula

- **G** o el ícono de cuatro cuadros alterna entre el visor y una cuadrícula con miniaturas cuadradas. Las miniaturas llenan su celda y las fotos originales conservan su encuadre.
- La cuadrícula abre la carpeta de la foto actual. Si la foto todavía no tiene archivo o el visor está vacío, muestra la carpeta configurada para las capturas.
- Las fotos se ordenan por nombre, la rueda desplaza la cuadrícula y un doble clic abre la foto seleccionada. La foto actual se resalta.
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
