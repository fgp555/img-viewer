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

### 2026-11-14 · Tamaño de imagen y ventana

- En modo Imagen, **Alt + 1**, **Alt + 2** y **Alt + 3** muestran la imagen al 50 %, 100 % y 150 % en píxeles de pantalla y ajustan la ventana a su proporción. Si es necesario, salen de pantalla completa.
- Se respeta el tamaño mínimo de la ventana para mantener los controles accesibles.


### 2026-11-14 · Opacidad configurable del fondo

- Configuración permite ajustar la opacidad del fondo semitransparente entre 1 % y 100 %, con 25 % por defecto. El ajuste se aplica al momento y se conserva al cerrar la app.


### 2026-11-14 · WebP y fondo al 50 %

- Apertura de WebP estático, miniaturas en Cuadrado y Masonry, y conversión individual desde **Guardar copia** a PNG, JPG, BMP o WebP sin pérdida. Se incorporan Imazen.WebP 11.0.0 y su runtime Windows x64 1.6.1.
- WebP y PNG conservan transparencia. JPG usa el codificador existente. La reproducción de WebP animado queda pendiente.
- El fondo inicial del visor tiene un 50 % de opacidad para permitir clic y arrastre sobre las zonas libres. **T** alterna entre semitransparente, oscuro y cuadrícula. Las exportaciones conservan sus propios píxeles y transparencia.


### 2026-11-14 · Fondo transparente inicial

- El visor inicia con fondo transparente. **T** alterna entre transparente, sólido oscuro y cuadrícula. El fondo solo afecta la visualización.
- La ventana usa transparencia de WPF y conserva el borde gris. La sombra nativa puede variar al usar una ventana transparente.


### 2026-11-14 · Abrir SVG

- Se incorpora SharpVectors.Wpf 1.8.6 para abrir SVG desde el Explorador o arrastrarlos al visor, recorrerlos y mostrarlos en Cuadrado y Masonry.
- El SVG se representa como imagen con transparencia para usar las herramientas actuales. Las ediciones se exportan como PNG, JPG o BMP y el original SVG se conserva.
- La representación de trabajo se limita a 8192 px por lado y 32 megapíxeles. Los recursos externos de SVG se omiten.


### 2026-11-09 · Salir con Shift + clic

- **Shift + clic en Cerrar** sale por completo de IMG Viewer, incluida la bandeja. Un clic normal sigue la preferencia de cierre.

### 2026-11-09 · Selección visible en la cuadrícula

- Al seleccionar una miniatura cerca del borde inferior, la cuadrícula se desplaza suavemente si el pie de metadatos reduce el espacio visible. Si la miniatura ya se ve completa, la cuadrícula se queda en su posición.

### 2026-11-09 · Transición de la cuadrícula al visor

- Al abrir una foto con doble clic, la imagen se amplía desde la posición visible de la miniatura. La transición dura 200 ms y respeta la configuración de animaciones de Windows.


### 2026-11-09 · Carga de la cuadrícula

- La cuadrícula abre en **Masonry** por defecto. La lectura de la carpeta y de las miniaturas se hace en segundo plano, con una caché limitada en memoria y cancelación al cambiar de carpeta o volver a Imagen.


### 2026-11-09 · Botón Minimizar

- El botón **Minimizar**, junto a **Cerrar**, envía la ventana a la barra de tareas.

### 2026-11-09 · Marco de captura

- Al abrir una captura nueva, el visor destaca sus bordes con un marco degradado y la etiqueta **Captura**. El marco hace un pulso suave si las animaciones de Windows están activadas. Al guardar o copiar, se usa la imagen sin el marco.

### 2026-11-09 · Esc en modo Imagen

- Tras atender un trazo, los cambios pendientes o la pantalla completa, **Esc** ejecuta la misma acción que **×**. La preferencia de cierre decide si el visor se oculta en la bandeja o si la app se cierra.


### 2026-11-08 · Inicio con información

- El visor arranca en modo Imagen con el pie de información abierto y la barra superior visible. El inicio con Windows sigue abriendo la app en la bandeja cuando esa opción está activada.

### 2026-11-08 · Siempre visible

- **Ctrl + F12** fija o desfija la ventana activa por encima de las ventanas normales. El atajo funciona también con IMG Viewer oculto en la bandeja.

### 2026-11-08 · Ajuste de imagen con C

- En modo Imagen, **C** alterna entre **Contain**, que muestra toda la foto, y **Cover**, que llena la ventana con zoom. **Ctrl + arrastre izquierdo** desplaza la foto en Cover para ver las zonas fuera de la vista. El ajuste es visual y **Ctrl + C** sigue copiando la imagen.

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


## 2026-11-14 — Áreas internas en capturas

- La captura detecta paneles y contenido accesible de las aplicaciones antes de mostrar la pantalla congelada, sin nuevas bibliotecas externas.
- El cursor muestra una cruz con líneas de lado a lado y el área detectada tiene un borde amarillo discontinuo.
- Clic captura el área resaltada; la rueda cambia entre áreas superpuestas y la ventana completa. Arrastrar conserva la selección manual y Esc cancela.
- La detección tiene un tiempo limitado y depende de la accesibilidad de cada aplicación; si no hay áreas disponibles, se mantiene la captura de ventana. Captura solo contenido visible.


## 2026-11-14 — Menús, paneles laterales y modo zen

- Nueva presentación con menús Archivo, Editar, Ver y Ayuda, una barra de acciones compacta y controles de ventana en la parte superior. La zona libre del encabezado permite mover la ventana.
- Archivo permite abrir imagen o carpeta, guardar, guardar copia, capturar, cerrar imagen, ocultar/cerrar la ventana y salir completamente. Editar reúne copiar, girar, recortar, redimensionar, convertir formato y enviar a la Papelera.
- Galería lateral izquierda con miniaturas virtualizadas de la carpeta actual, apertura con un clic y reutilización de la caché de miniaturas. La cuadrícula mantiene su selección múltiple y acciones masivas.
- Panel derecho para flechas y rectángulos, color y grosor. Cada marca conserva su estilo y siguen disponibles Alt/Shift + arrastre. Ctrl + arrastre conserva el desplazamiento de la imagen ampliada.
- Ambos paneles se pueden plegar y redimensionar. Se recuerdan sus preferencias y se ocultan temporalmente cuando falta espacio.
- El clic izquierdo deja de ocultar controles. El clic derecho abre un menú contextual con Modo zen primero, seguido de Copiar, Cerrar ventana, Convertir y más acciones.
- Modo zen oculta menús, botones, paneles e información. Se restaura desde el mismo menú contextual o con Tab desde el visor. No modifica el archivo ni la preferencia de los paneles.
- Ctrl + O abre imagen y Ctrl + Shift + O abre carpeta. Ayuda incluye los atajos, la página y la versión de la app. Convertir utiliza Guardar copia para elegir PNG, JPG, WebP o BMP.
- Los ajustes de tamaño del visor tienen en cuenta el espacio de los nuevos menús y paneles.

- Verificación: compilación Release x64 sin errores ni advertencias; revisión visual de menús y contexto, entrada/salida de zen, apertura de miniaturas y dibujo de flecha roja más rectángulo azul en imágenes de ejemplo. Esc cierra un menú abierto sin descartar marcas.


## 2026-11-14 — Tema neutro, búsqueda y barra de estado

- Paleta gris oscura para cabecera, menús y paneles, con borde exterior y separadores discretos. Los iconos de acciones no tienen borde ni fondo permanente; conservan respuesta al pasar el mouse y al recibir foco.
- Icono de IMG Viewer y botones de imagen anterior/siguiente antes del menú Archivo. El icono se incluye como recurso WPF para funcionar también en la publicación de un solo archivo.
- Botones de minimizar, maximizar/restaurar y cerrar con proporciones y trazos de estilo Windows. El icono de maximizar cambia al de restaurar y el cierre se resalta en rojo al pasar el mouse. El maximizado respeta el área útil del monitor y la barra de tareas.
- Buscador en la galería por nombre o ruta relativa, limitado a la carpeta actual y sus subcarpetas mientras hay texto. Al vaciarlo muestra solo las imágenes de la carpeta actual. Búsqueda en segundo plano, con espera breve al escribir y cancelación; omite subcarpetas inaccesibles y no sigue enlaces a otros directorios.
- Barra de estado de 28 px, estilo VS Code, con metadatos, estado y acciones de guardado/restauración. Tiene su propia fila fuera del lienzo y no tapa la imagen. La cuadrícula utiliza el mismo estilo de barra. Los textos largos se abrevian y se pueden consultar completos en el tooltip.
- Se mantiene el modo zen y se devuelve el foco al visor al pulsar sobre la imagen después de escribir una búsqueda.
- Verificación: Release x64 sin errores ni advertencias; búsqueda comprobada con coincidencia en subcarpeta y archivo coincidente fuera de alcance excluido; medición confirma que el borde inferior del lienzo coincide con el inicio del footer. Revisión visual del tema, cabecera, búsqueda, cuadrícula y maximizado con footer visible.


## 2026-11-14 — Iconos Fluent y acciones junto a marcación

- Iconos de la ventana principal unificados mediante Segoe Fluent Icons, con Segoe MDL2 Assets como alternativa del sistema. Se reemplazan los trazos personalizados y caracteres sueltos de acciones, navegación, presentación, selección de vista y controles de ventana; no se añaden paquetes externos.
- Copiar, guardar, restaurar la última imagen eliminada y enviar a la Papelera se agrupan en la barra superior, junto al lápiz de marcación. El footer queda exclusivamente para metadatos y estado.
- Guardar permanece visible, se desactiva cuando no corresponde y se muestra en ámbar si hay cambios pendientes. El botón de restauración conserva su aviso rojo de cinco segundos, parpadeo y cambio al icono Deshacer al pasar el mouse.
- Copiar y eliminar usan los mismos botones en el visor y la cuadrícula, respetando la selección y desactivándose durante acciones masivas.
- Verificación: Release x64 sin errores ni advertencias, revisión visual de los iconos y su ubicación; eliminación y recuperación de una imagen de ejemplo, giro y guardado desde el nuevo botón. El footer permanece separado del lienzo y sin botones.


## 2026-11-14 — Configuración, selección y recuperación

- Marco redondeado y colores comunes para la ventana principal y los diálogos propios; icono monocromo y mayor espacio superior. En modo zen se eliminan el borde y las esquinas redondeadas.
- Información siempre visible fuera de zen, sin botón Info. T y su botón alternan únicamente fondo sólido y cuadrícula; se elimina la opción de opacidad.
- Configuración con navegación lateral, búsqueda de secciones, tarjetas e interruptores: General, Capturas, Galería, Cursor y Acerca de.
- Galería con botón + para añadir y recordar carpetas, selector de carpeta y buscador con placeholder.
- Botones directos para fondo, captura, pantalla completa y zen. Todas las acciones y atajos ofrece botones para operaciones adicionales, escalas y herramientas.
- Casillas por miniatura y Seleccionar todas en cuadrícula, con estados vacío, marcado e intermedio de Segoe Fluent Icons. Ctrl + A selecciona todas; conserva la selección múltiple y el peso total.
- Historial de eliminaciones de la sesión: botón Deshacer permanente mientras haya archivos recuperables, con aviso rojo de cinco segundos. Ctrl + Z deshace primero una marca pendiente o recupera la última eliminación; puede repetirse para recuperar varios archivos, también los eliminados desde la cuadrícula.
- Skill multiproyecto ui-consistency-wpf actualizado con pautas reutilizables sobre iconos de selección, diálogos, configuración y modo sin distracciones.


## 2026-11-14 — Refinamiento de menús y búsqueda

- Menú contextual y desplegables con superficie gris neutra, esquinas redondeadas, separación y resaltado discreto.
- Cabecera con altura y fondo uniformes, sin márgenes de otro color, y flechas anterior/siguiente reducidas a 12 px.
- Selector de carpetas con ChevronDown de Segoe Fluent Icons, reemplazando el carácter de texto.
- Buscador de galería redondeado con icono Search Fluent, placeholder y borde de foco.


## 2026-11-14 — Scroll, formatos y fondo de capturas

- Barras de desplazamiento delgadas, con indicador gris oscuro y respuesta al hover/arrastre, compartidas por galería, ajustes y diálogos.
- Configuración > Galería incluye JPG, JPEG, PNG, BMP, SVG y WebP, con casillas y Seleccionar todos. Se recuerda la selección y se aplica a apertura, navegación y galería; los formatos de exportación se mantienen disponibles.
- Al capturar pantalla se activa directamente el fondo de cuadrícula y deja de mostrarse el marco coloreado. T o su botón permiten volver al fondo sólido.

- Deshacer eliminación deja de parpadear; el botón mantiene opacidad fija y conserva la recuperación de archivos.
- Deshacer usa el mismo fondo neutro y hover de los demás botones, sin fondo rojo.


## 2026-11-14 — Menú de bandeja oscuro

- El menú del icono de notificaciones utiliza un renderer oscuro propio para Windows Forms, con texto claro, espaciado, resaltado gris y separadores discretos.
- Se añaden Abrir carpeta… y Acerca de IMG Viewer, junto a Abrir, Capturar y Salir. Abrir carpeta utiliza el mismo flujo del menú Archivo.

- Zen y modo normal conservan por separado el tamaño, posición y estado maximizado durante la sesión. Redimensionar en zen ya no modifica las dimensiones del modo normal; todas las salidas de zen restauran su disposición.


## 2026-11-14 — Conversión masiva y barra compacta

- Se retiran los iconos de redimensionar de la barra principal y la selección de cuadrícula. Redimensionar permanece en Todas las acciones (tres puntos), Editar y el menú contextual cuando corresponde.
- Convertir formato admite varias imágenes seleccionadas en cuadrícula desde los tres puntos, Editar o el menú contextual. Permite PNG, JPG, WebP sin pérdida y BMP; guarda copias con nombres únicos junto a los originales y muestra el resultado. JPG compone las transparencias sobre blanco.


## 2026-11-14 — Sección Formatos y visor predeterminado

- Los tipos de archivo y Seleccionar todos se trasladan de Galería a la nueva sección Formatos, incluida en la búsqueda de ajustes.
- Configurar como visor predeterminado registra el ejecutable actual para el usuario y abre la página de IMG Viewer en Aplicaciones predeterminadas de Windows. La elección de asociaciones la confirma el usuario en Windows; no se modifica UserChoice.
- Se registran los formatos compatibles para Abrir con. Si se cambia de ubicación el ejecutable, el botón permite actualizar las rutas.

- Cubrir / ajustar la imagen cambia de C a Z. Ctrl + C conserva Copiar y Ctrl + Z conserva Deshacer.

- Con la imagen ampliada, mantener el botón central y arrastrar desplaza la imagen con cursor de mano y los mismos límites de Ctrl + arrastre. Sin ampliación, el clic central conserva el cambio de vista imagen/cuadrícula.

- El clic central alterna cubrir/ajustar igual que Z en modo imagen. Arrastrar con el central conserva el desplazamiento de la imagen ampliada y no alterna el ajuste al soltar. G sigue alternando imagen/cuadrícula.

- El zoom de cubrir/ajustar con clic central utiliza el punto pulsado como ancla, conservando su posición bajo el cursor hasta los límites de desplazamiento de la imagen. Z mantiene su comportamiento de teclado.

- La captura oculta el visor antes de congelar el escritorio, espera la actualización del compositor y captura las ventanas detrás. Al cancelar o fallar recupera la visibilidad y el estado anteriores; al completar muestra la captura.
- Se centra la altura real de los glifos de anterior/siguiente y los controles de ventana; los botones de título ocupan los 40 px del encabezado.

- Menú de bandeja refinado: ancho mínimo de 240 px, filas de 34 px, texto Segoe UI 9.5, marco y hover redondeados, separadores completos y espaciado adaptado al DPI. Se añade Configuración… entre Abrir carpeta y Acerca de.


## 2026-11-14 — Marcas Limpio y Contraste

- Flechas con punta triangular rellena, proporciones adaptadas al grosor y remate redondeado, sin unión visible entre cuerpo y punta.
- Rectángulos sin relleno con esquinas suavemente redondeadas.
- Selector Limpio / Contraste en marcación. Contraste añade un contorno blanco y negro para distinguir las marcas sobre fondos claros y oscuros. Cada marca conserva color, grosor y estilo propios al deshacer, copiar y exportar.
- Implementación con geometrías nativas WPF, sin dependencias adicionales.


## 2026-11-14 — Flechas estilizadas y curvas

- Selector de forma Clásica, Estilizada y Curva. Estilizada y Curva tienen cola fina, cuerpo progresivo y punta rellena.
- La última flecha curva permite ajustar su curvatura arrastrando un tirador central; el tirador se oculta en zen y no se copia ni exporta.
- Conservan color, grosor y acabado Limpio/Contraste. Implementación WPF sin dependencias nuevas.

- Flechas Estilizada y Curva con punta tipo arpón: extremos laterales retrasados y base cóncava con muescas junto al cuerpo.

- Flecha Estilizada predeterminada. Al abrir una captura se sale de zen, se muestra marcación con Flecha seleccionada y grosor 50%. El grosor se presenta como porcentaje.
- Un único icono Deshacer junto a Copiar reemplaza el botón de texto del panel y el control exclusivo de recuperación. Sin parpadeo, deshace marcas y luego eliminaciones, igual que Ctrl + Z; se desactiva cuando no hay acciones disponibles.

- Nombres de archivo centrados bajo las miniaturas de la galería.

- Al abrir o navegar a una imagen se oculta marcación y se activa Mover ventana. Las capturas conservan la apertura automática de marcación con Flecha seleccionada.

- Texto del menú de bandeja centrado verticalmente respecto a la altura completa de cada fila, conservando alineación lateral y espaciado DPI.


## 2026-11-14 — Ventana compacta

- Por debajo de 640 px, cabecera con controles de ventana visibles y menús agrupados en ☰. Barra de una fila con Galería, Vista, Copiar, Deshacer, Marcación y Más acciones.
- Acciones secundarias accesibles desde los tres puntos. Paneles laterales superpuestos sobre el contenido en ancho compacto, alternando galería/marcación sin reducir el lienzo.

- Corrección de esquinas claras: plantilla de ventana transparente sin decoración predeterminada y recorte del marco exterior con su mismo radio. Zen, maximizado y pantalla completa conservan esquinas rectas.


## 2026-11-14 — Guardado manual de capturas

- Las capturas quedan en memoria hasta Guardar o Ctrl + S, sin crear un archivo automáticamente por defecto. El diálogo propone Imágenes y WebP sin pérdida.
- Configuración > Capturas incorpora Guardar capturas automáticamente, desactivado por defecto, y elección de carpeta y formato (WebP, PNG, JPG, BMP).
- Nueva preferencia de carpeta con valor inicial Imágenes; la antigua carpeta del guardado automático no se reutiliza. Las capturas conservan su flujo de marcación y copia al portapapeles opcional.


## 2026-11-14 — Mano alzada, Texto y Blur

- Mano alzada en marcación y Alt + Shift + arrastre izquierdo, con color, grosor y contraste.
- Texto se activa con Ctrl + T; clic en la imagen abre un editor con tamaño y texto multilínea.
- Blur se activa con Ctrl + B; arrastrar delimita un desenfoque gaussiano aplicado como marca.
- Las tres operaciones se incluyen al copiar/exportar y se deshacen con el icono o Ctrl + Z, sin nuevas bibliotecas.


## 2026-11-14 — Historial temporal de capturas

- Galería separada en Carpeta y Capturas de esta sesión. Cada captura mantiene imagen y marcas en memoria al cambiar entre ellas.
- Miniaturas con × para descartar una captura y Limpiar todo para descartar el historial sin guardar ni borrar archivos existentes.
- Guardar/Ctrl + S conserva el guardado explícito de la captura elegida; el guardado automático opcional sigue respetando Configuración. El historial vive durante la sesión, incluida la bandeja, y se pierde al salir completamente.

- Grosor predeterminado de marcación ajustado a 30%, también al abrir capturas.

- V activa Mover ventana y desactiva la marcación o desplazamiento de imagen seleccionados, sin interferir al escribir en campos de texto.

- Mano alzada suavizada con filtro moderado de vibración y curvas cuadráticas continuas, grosor uniforme y extremos redondos. La vista previa y la exportación usan la misma geometría, conservando el recorrido sin convertirlo en una figura automática.

- Texto se edita directamente en el lienzo, sin modal. Clic crea ancho libre; arrastrar crea ancho fijo con ajuste de línea y altura automática. Ctrl + Enter o salir del campo confirma, Esc cancela; tamaño configurable en marcación.

- Footer con destino de captura y formato antes de guardar; identifica Imágenes como predeterminada y muestra ruta personalizada completa en tooltip. Tras guardar muestra la ruta real, con clic para abrir la carpeta y seleccionar el archivo. Capturas sin guardar se indican en ámbar.

- Corregido V al quedar el foco en selectores del panel: activa Mover ventana, actualiza su selección y restaura el cursor. Los campos de escritura conservan la entrada de texto.


## 2026-11-14 — Tamaño inicial adaptable

- Primera apertura centrada al 80% del área útil del monitor, respetando barra de tareas y escala.
- Al cerrar se recuerdan tamaño, posición relativa al monitor y estado maximizado; siguientes aperturas los recuperan. Si falta el monitor anterior se centra en el actual y se limita al área visible.
- Las dimensiones de zen no reemplazan las del modo normal; la pantalla completa tampoco se guarda como tamaño normal.

- Suavizado mejorado de mano alzada: remuestreo por distancia uniforme, filtro espacial moderado y curvas cúbicas con tangentes limitadas. Conserva extremos y recorrido libre; no reconoce ni convierte formas. Misma geometría en vista previa y exportación.

- Marcación solo se reinicia al abrir una imagen externa (Explorador, arrastrar archivo o Abrir imagen). Galería, cuadrícula, recuperación y navegación entre imágenes conservan el panel y la herramienta seleccionada. Las capturas mantienen su activación de Flecha Estilizada.

- Tamaño inicial adaptativo reducido del 80% al 60% del área útil del monitor. Se mantiene la restauración de la última disposición guardada.


## 2026-11-14 — Estabilizador de mano alzada

- Estabilizador de tipo cuerda para mano alzada, activado inicialmente a 12 px de pantalla; ajustable de 0 a 30 px en marcación (0 desactiva).
- El punto dibujado sigue al cursor al superar esa distancia, independientemente de la resolución y zoom. Conserva el suavizado mejorado y no añade un segmento brusco al soltar. No afecta a flechas, rectángulos, texto ni blur.

- Mano alzada añade Trazo Uniforme / Brush (Uniforme por defecto). Brush simula presión mediante velocidad, suaviza los cambios de ancho y afina inicio/final; conserva estabilizador, color, contraste y geometría al copiar/guardar.


## 2026-11-14 — Captura prioritaria

- Impr Pant prioriza congelar los píxeles antes de enumerar ventanas o consultar accesibilidad. Se elimina la espera fija de 160 ms al ocultar el visor; se sincroniza con el compositor.
- El selector aparece sin esperar la detección de áreas. La selección manual funciona desde el inicio y las ventanas/áreas internas se incorporan en segundo plano usando sus HWND originales.
- Resultados tardíos se ignoran si el selector ya se cerró y no interrumpen un arrastre en curso. Tiempos de captura y presentación disponibles en la salida Debug.

- Selector de captura con una sola píldora Pantalla completa e icono Fluent, centrada arriba y oculta al arrastrar. Esc anula la selección manual en curso; sin selección manual cancela toda la captura.

- Color predeterminado de marcación cambiado a coral #F87171, con muestra y etiqueta del panel coherentes.

- Marcación predeterminada ajustada a rojo suave #F24F57, intermedio entre rojo puro y coral, con opacidad completa.


### Atajo configurable de captura
- Ctrl + Impr Pant es el atajo global predeterminado. Configuración > Capturas incluye Cambiar atajo y Usar Impr Pant.
- El atajo se guarda entre sesiones; se valida disponibilidad y se conserva el anterior ante conflictos o errores de guardado. Menú, botón y ayuda muestran el atajo elegido.
- Impr Pant sola incluye indicaciones para desactivar su acceso a Recortes de Windows.


### Acceso a captura en la barra de acciones
- Capturar se ubica a la derecha, antes de Copiar, junto a Deshacer, Guardar, Eliminar y Marcación.
- En ventanas estrechas permanece visible antes de Copiar; su tooltip muestra el atajo configurado.


### Marco nativo de Windows 11
- La ventana principal utiliza un HWND opaco y esquinas/borde oscuros nativos de DWM, sin el doble recorte redondeado de WPF que dejaba esquinas blancas.
- Zen y pantalla completa solicitan esquinas rectas y ningún borde; al restaurar se recupera el marco normal. En sistemas anteriores a Windows 11 se conserva una ventana opaca de esquinas rectas.


### Cambio de masonry a cuadrados
- El tamaño cuadrado usa el ancho del área de galería aunque la lista esté oculta; se recalcula al alternar el diseño.
- El desplazamiento se restaura después del layout y se limita al contenido disponible para evitar una vista vacía.


### Escape en selección de captura
- El selector toma el foco de teclado al abrir y al empezar una selección para recibir Escape.
- Escape durante el arrastre anula el área y evita capturar al soltar; sin arrastre cancela toda la captura.


### Encabezado de galería
- Pestañas Carpeta/Capturas con subrayado activo, sin cajas.
- Selector de carpeta sin borde, menú oscuro redondeado y check Fluent en la carpeta activa; añadir carpeta a su derecha.
- Se elimina el título redundante Galería y se reduce el radio del buscador a 6 px.


### Minimizar después de copiar
- Nueva opción en General, desactivada por defecto y persistida entre sesiones.
- Copiar manualmente con botón o Ctrl+C minimiza a la barra de tareas solo después de copiar correctamente; funciona en imagen y cuadrícula. La copia automática de capturas conserva la ventana.


### Panel de marcación contextual
- Herramientas como filas con iconos Fluent y fondo suave en la selección, conservando controles radio y atajos.
- Propiedades visibles solo para la herramienta activa; selectores sin borde, sliders finos y muestras de color con check.
- Se retiran la vista previa estática de varias herramientas y los textos largos permanentes; ayuda disponible en tooltips.


### WebP para publicación web
- WebP usa compresión con pérdida y calidad 80% por defecto, configurable entre 1 y 100.
- Sin pérdida es una opción explícita, desactivada por defecto; al activarla se oculta calidad conservando su valor.
- Preferencia compartida y persistente para guardar, capturas automáticas y conversiones individuales o masivas. Controles en Configuración > Formatos, al elegir WebP en Capturas y en conversión masiva.
- Se elimina la etiqueta fija WebP sin pérdida de los selectores y del diálogo de guardado.


### Alineación de herramientas
- Ajuste óptico del texto de las seis herramientas de marcación para alinearlo con los iconos, conservando la altura de las filas.


### Tarjetas de galería
- Miniaturas cover con proporción original, esquinas de 8 px y nombre blanco superpuesto sobre degradado negro inferior.
- Nombre en una línea con elipsis y tooltip completo; selección exterior y foco de teclado visibles. Aplicado también al historial de capturas.
- Se conserva la carga asíncrona, cancelación al reciclar y caché de miniaturas.


### Formato visible en la galería
- Nombre de archivo sin extensión a la izquierda, con elipsis; formato en etiqueta independiente a la derecha para conservarlo visible con nombres largos.
- El tooltip conserva el nombre completo con extensión. Las capturas sin guardar no muestran un formato ficticio.


### Arrastrar desde galería
- Un clic abre; arrastrar más allá del umbral del sistema entrega un archivo real a carpetas y aplicaciones compatibles, ofreciendo solo copia.
- Vista flotante con miniatura y nombre durante el arrastre; el visor ignora sus propios drops.
- Las capturas generan una copia temporal con anotaciones y formato/calidad configurados, sin marcar el historial como guardado. Los temporales recientes se conservan para destinos que leen de forma diferida y se limpian tras siete días al exportar otra captura.


### Scroll y renombrado en galería
- Scroll por píxeles con animación breve de rueda acumulativa y virtualización conservada; respeta animaciones del sistema.
- F2 edita el nombre sobre la tarjeta sin extensión: Enter confirma, Esc cancela y arriba/abajo guardan antes de editar la imagen vecina. Errores junto al campo.
- El renombrado conserva la colección y su posición, actualiza rutas del visor y evita reordenar durante la secuencia.


### Zoom de reducción
- Ctrl + rueda permite reducir hasta el 10% del tamaño ajustado y ampliar hasta 8 veces. Las acciones Ampliar/Reducir comparten los mismos límites.


### Porcentaje de zoom
- Footer con porcentaje de zoom real, actualizado al ampliar, reducir y redimensionar. 100% representa un píxel de imagen por píxel físico de pantalla, considerando DPI.


### Barra estable y delgada en galería
- Galería y Capturas reservan permanentemente el ancho del scrollbar para evitar ciclos de aparición/desaparición con tarjetas proporcionales.
- Indicador visible de 3 px, con zona de interacción de 10 px; se oculta por opacidad cuando no hay desplazamiento, sin cambiar el layout.
- Carril transparente e indicador gris semitransparente, con mayor opacidad en hover y arrastre.


### Escalas por modo de ventana
- Alt + 1/2/3 (50%, 100%, 150%) cambia solo el zoom en modo normal, conservando tamaño, posición y estado de ventana.
- En modo zen mantiene el ajuste de la ventana a la proporción y escala de la imagen.


### Restablecer zoom con clic central
- Un clic central con zoom aumentado o reducido vuelve a Contain y centra la imagen. Sin zoom personalizado mantiene la alternancia Contain/Cover anclada al cursor.
- Arrastrar con el botón central conserva el desplazamiento de la imagen ampliada y no restablece el zoom al soltar.


### Panel de acciones y atajos
- Atajos agrupados en tarjetas con encabezados e iconos Fluent; teclas en cajas y descripciones alineadas.
- Una columna en ventanas estrechas y dos en amplias, con scroll y ventana redimensionable.
- Filas ejecutables, estados habilitados según contexto y atajo de captura real; se incluyen texto, blur, mano alzada y restablecer zoom.


### Apertura prioritaria desde el Explorador
- Decodificación fuera del hilo de interfaz y presentación de la imagen antes de enumerar la carpeta o leer metadatos.
- Enumeración y metadatos posteriores en segundo plano; versiones de solicitud evitan aplicar resultados antiguos al abrir varias imágenes.
- En arranque visible, bandeja y configuración del cursor se inicializan después de mostrar la ventana. Inicio en bandeja conserva sus servicios inmediatos.
- Tiempos de decodificación, presentación y detalles registrados en salida Debug para comparar equipos y formatos.
- Validación local con imágenes sintéticas: retorno al hilo UI 6,4 ms e imagen lista ~56 ms; verificados resultados más recientes y SVG en worker. Estas cifras no representan el arranque completo desde el Explorador.


### Recorte directo en el visor
- Tras capturar se muestran ocho tiradores para recortar sin modal; las herramientas de marcación siguen disponibles hasta arrastrar un tirador.
- Icono Recortar activa/desactiva el modo en cualquier imagen; Enter o check aplica, Esc o cancelar descarta el marco. Ctrl+S aplica un recorte pendiente antes de guardar.
- Se conserva Recortar y enderezar en el menú para el editor avanzado. El recorte integra anotaciones y queda pendiente de guardar.


### Recorte inmediato en memoria
- Soltar un tirador aplica el recorte sin Enter ni confirmación y deja los tiradores activos para seguir ajustando.
- Ctrl+C copia la imagen ya recortada sin guardar; Ctrl+S sigue siendo la acción que escribe el archivo. Se retiran los botones de confirmar/cancelar.
- Escape durante el arrastre cancela el ajuste sin aplicarlo.


### Atajo para flecha curva
- Ctrl + Shift + arrastre izquierdo crea una flecha curva directamente y conserva el tirador de curvatura al terminar. La forma queda fijada al iniciar el gesto, aunque se suelten los modificadores antes del mouse.
- Alt + arrastrar mantiene la forma elegida en el selector, que no se modifica por el atajo temporal. Tooltip y panel de atajos actualizados.


### Icono de cambio de fondo
- Se reemplaza el cuadrado vacío por un icono dinámico de la misma familia: cuadrícula para pasar al damero y círculo sólido para volver al fondo sólido.
- Tooltip, nombre accesible y menú describen la siguiente acción e incluyen el atajo T; estado sincronizado tras capturar.


### Escape por modo
- En zen, Escape restaura el modo normal sin minimizar.
- En modo normal, dos pulsaciones de Escape en un máximo de 500 ms minimizan a la barra de tareas; se ignora la repetición por mantener la tecla pulsada.
- Cancelar una edición, un recorte o salir de pantalla completa no cuenta como la primera pulsación para minimizar. Otra tecla, clic o pérdida de foco reinicia la secuencia. Ayuda de atajos actualizada.


### Icono de cuadrícula de fondo
- Se usa GridViewSmall (F232) para diferenciar el fondo cuadriculado de la vista de galería.


### Actualizaciones desde R2
- Adaptado el actualizador de Call WPF: consulta, descarga verificada, progreso, reemplazo con respaldo y reinicio explícito.
- Actualización de la edición Light single-file publicada; controles en Acerca de y aviso de versión en la barra.
- Se conservan los archivos personales my-*; documentación y script de publicación corregidos para IMG Viewer, con manifiestos generados por edición. No se suben archivos a R2 automáticamente.
- Manifiesto reutilizable latest.json para la única edición Light distribuida.
- Rutas confirmadas: https://cdn.frankgp.com/img-viewer/latest.json y https://cdn.frankgp.com/img-viewer/IMGViewer.exe.
- Publicación simplificada a Light únicamente: IMGViewer.exe y latest.json, sin sufijos de edición. Se excluye Full del actualizador para no cambiar su dependencia de runtime.


### Comandos npm de publicación
- npm run build genera Light y latest.json; npm run deploy valida y sube el par a R2 leyendo .env sin exponer credenciales.
- Dry-run y pruebas con red simulada; subida del EXE antes del manifiesto y sin dependencias npm externas.


### Build local y deploy automatico
- Build solo compila para pruebas locales. Deploy consulta latest.json remoto, incrementa patch, empaqueta, genera hash/notas y publica EXE antes de JSON.
- Notas predeterminadas: Mejoras y correcciones. Se retira la denominacion de edicion de los textos, scripts y documentacion.


### Modulo release reutilizable
- Publicacion centralizada en release/: configuracion, build, deploy, pruebas, documentacion y output ignorado por Git. Se conservan notas y archivos previos.
- Rutas nuevas: /img-viewer/downloads/IMGViewer.exe y /img-viewer/downloads/latest.json. Bootstrap opcional del manifiesto anterior solo si la ruta nueva responde 404.
- Configuracion central por proyecto sin rutas de producto en la logica de empaquetado/subida. Instalaciones antiguas necesitan version puente o instalacion manual para adoptar el nuevo feed.


### Consulta de version publicada
- npm run show muestra version remota, notas, descarga y SHA-256 sin credenciales ni modificaciones. Indica si se uso el manifiesto previo durante la migracion.


### Build para probar la distribucion
- npm run build reutiliza el mismo empaquetado de deploy y genera el EXE single-file en release/output, con la version local del csproj. No incrementa version ni publica.


### Version 1.0.1
- Numeracion inicial cambiada de 2026.11.1 a 1.0.1. Deploy incorpora migracion explicita desde la version remota anterior y continua incrementando patch tras ella.
- Se conserva la comparacion de versiones del actualizador; instalaciones 2026.x requieren reinstalacion manual.


### Primer deploy sin manifiesto
- HTTP 404 permite publicar la version local del csproj sin incremento. Otros errores siguen deteniendo el deploy. La comprobacion previa al upload detecta publicaciones concurrentes y desaparicion del manifiesto.
- npm run show informa cuando aun no existe una publicacion.


### Limpieza del modulo release
- Eliminadas URL fallback, bootstrap y migraciones de version, junto con sus pruebas y ejemplos heredados. Solo se consulta el feed configurado.
- Documentacion reutilizable reescrita; notas personales y generador de icono preservados fuera del modulo, en docs/archive/release-notes.


### Scripts del modulo release
- Scripts agrupados en release/scripts; configuracion, README y output permanecen en la raiz del modulo. Comandos npm y resolucion de rutas actualizados.


### Credenciales del modulo release
- .env movido a release/.env sin modificar sus valores. Deploy lo lee desde la raiz del modulo; .env.example sin secretos y exclusiones de Git incluidas para reutilizar la carpeta.


### Inicializador WPF reutilizable
- npm run wpf:init detecta csproj WPF/AssemblyName, actualiza scripts npm y configuracion sin eliminar comandos ajenos. --project resuelve multiples proyectos.
- PROJECT y STORAGE_PUBLIC_URL en release/.env generan nombre EXE, URLs downloads y prefijo R2; se preservan credenciales y URL existente.


### Miniatura temporal tras captura
- Miniatura sin activar la ventana principal, fondo sólido, historial conservado y acciones copiar, guardar, guardar/copiar ruta, abrir carpeta y cerrar. Clic en imagen abre el visor con marcación y recorte.
- Configuración permite miniatura (predeterminado) o abrir visor y duración 3–60 segundos (8 por defecto), pausada con hover. Se reutiliza el archivo guardado para evitar duplicados.
- Capturas abiertas en el visor usan fondo sólido.


### Miniatura sin texto y posicion multimonitor
- Botones arriba de la miniatura, sin texto de estado visible.
- El selector transmite el centro fisico de la region capturada; widget ubicado en ese monitor, con DPI del destino, limites de area de trabajo y coordenadas negativas. Reposicion tras render y cambios de DPI.


### Miniatura de captura: acciones separadas

- Barra superior independiente con cuatro acciones: copiar, guardar y copiar ruta, guardar y cerrar. Iconos Fluent centrados sin recortes. Abrir carpeta pasa al menú contextual.
- Vista previa proporcional de hasta 320 × 240, sin relleno alrededor de las capturas horizontales. Las capturas estrechas mantienen un ancho mínimo para los botones. Se elimina la altura mínima nativa que añadía espacio vacío.
- Verificación: compilación Release sin errores y revisión visual con vistas sintéticas horizontal (320 × 220) y vertical (160 × 280). No se verificaron transiciones entre monitores con escalas diferentes.


### Flecha de arpón refinada

- Cola con corte plano y anchura visible, sin terminar en aguja. Laterales de la cabeza más cortos, menos inclinados y con extremos redondeados. Se aplica tanto a la flecha estilizada recta como a la curva y a su exportación.
- Grosor predeterminado actualizado al 40 % en el panel de marcación y al abrir capturas.


### Ubicación de la miniatura

- Configuración → Capturas permite elegir Monitor principal (predeterminado) o Monitor de la captura. La preferencia se conserva entre sesiones.
- La miniatura se coloca en la esquina inferior derecha del área útil del monitor elegido, respetando la barra de tareas; la selección de captura continúa disponible en todos los monitores.


### Flecha clásica predeterminada

- La flecha clásica pasa a ser la forma inicial, también al capturar. Cabeza proporcionada al cuerpo, cola redondeada y sombra suave conservada al copiar y guardar. Se mantiene el grosor inicial del 40 %. Las formas estilizada y curva siguen disponibles.


### Sombra y recorte por gesto

- La flecha clásica permite ajustar la intensidad de sombra entre 0 y 100 % (40 % inicial); cada nueva flecha conserva su valor al copiar o guardar.
- Ctrl + Alt + arrastrar sobre la imagen selecciona el área que se conservará al soltar, con recorte inmediato en memoria y sin guardar automáticamente. Esc cancela el gesto y un clic sin arrastrar no recorta.


### Ajustes iniciales de marcación

- Intensidad de sombra situada debajo de Grosor en las propiedades de la flecha. Nuevos valores iniciales: sombra 20 % y grosor 60 %, incluido el grosor al capturar pantalla.


### Recorte con confirmación visual y deshacer

- Ctrl + Alt + arrastrar mantiene la selección 0,3 segundos con un pulso del borde antes de recortar. Esc cancela durante esa pausa.
- Ctrl + Z y el icono Deshacer restauran recortes, incluida la imagen y las marcas previas. Hasta 10 recortes consecutivos por imagen; las marcas añadidas después se deshacen primero. No se modifica el archivo original hasta guardar.


### Borradores recuperables de capturas

- Las capturas pendientes se conservan automáticamente en %LocalAppData%\IMGViewer\Drafts y se recuperan al iniciar, también después de reiniciar Windows. La escritura se realiza en segundo plano con sustitución atómica de cada borrador.
- Se conservan la imagen, sus anotaciones editables, texto, parches de desenfoque, rotación y estado de edición. El pie distingue Borrador · sin guardar del destino de exportación.
- Guardar una captura sin cambios pendientes elimina su copia interna. Cerrar la miniatura conserva el borrador; cerrar una captura desde Galería lo elimina. Limpiar todo solicita confirmación y conserva los archivos exportados. Los borradores dañados se conservan y no impiden recuperar los demás.
- Verificación: ida y vuelta de un borrador temporal con comparación de píxeles y transparencia; conservación de rotación, geometría, control de curva, sombra y desenfoque. Se comprobó la sustitución de un borrador existente sin archivos temporales residuales. No se reinició Windows durante la prueba.


### Ubicación de Configuración

- Configuración pasa de Ver al final de Editar, con un separador. El menú compacto refleja el mismo orden; se conserva el acceso desde la bandeja.


### Guardado directo de capturas

- Ctrl + S y Guardar exportan capturas nuevas o borradores directamente a la carpeta configurada, con nombre screen-yyMMdd-HHmmss y sufijo si ya existe. Guardar copia conserva el selector de destino.
- JPG pasa a ser el formato predeterminado de las capturas; los demás formatos siguen disponibles en Configuración.


### Orden de opciones de Capturas

- Configuración → Capturas muestra Atajo de captura, Guardado y portapapeles y Después de capturar, en ese orden. Se conserva el comportamiento de todas las opciones.


### Escape en capturas

- Esc mantiene la cancelación/restauración de cambios. Cuando una captura ya no tiene cambios pendientes, activa Mover ventana igual que V y desactiva Flecha, sin minimizar ni cerrar. Se mantiene la prioridad de selección, edición de texto y menús.


### Nube con R2 propio

- Nueva sección Editar → Configuración → Nube, desactivada inicialmente. Cada usuario configura su endpoint S3, bucket, credenciales y carpeta remota, con prueba de conexión. Credenciales protegidas mediante DPAPI para su usuario de Windows, separadas de `release`.
- Al activarla aparece la pestaña Nube junto a Carpeta y Capturas. Listado paginado, miniaturas virtualizadas y acciones de abrir, descargar, copiar enlace y eliminar con confirmación, conservando los archivos locales.
- Subida manual desde la barra, Archivo o menú contextual, también para capturas sin guardar y selección múltiple del grid. Los originales sin cambios conservan su formato; las ediciones se suben como PNG. Incluye progreso, cancelación, nombres únicos y reintento de pendientes.
- Enlaces privados firmados de una hora o enlaces permanentes si el usuario configura la URL pública de su bucket. No se modifica la privacidad del bucket.
- Implementación con .NET y APIs de Windows, sin paquetes externos nuevos. Pasan 35 comprobaciones con HTTP simulado; revisión visual de Configuración y la pestaña con datos ficticios. No se probó contra un bucket real. Guía: `docs/cloud-r2.md`.


### Vista previa de pantalla completa al capturar

- Al pasar el cursor o enfocar con el teclado el botón Pantalla completa, se resalta todo el monitor que se capturará y se ocultan las guías. El botón muestra Capturar este monitor y sus dimensiones en píxeles.
- Al salir del botón se recupera la selección anterior; la detección asíncrona no la sustituye mientras está activa la vista previa. El clic captura el mismo monitor resaltado.
- Verificado con un escritorio sintético de 1280 × 720: marco completo y dimensiones visibles, restauración de selección, protección ante detección tardía y coincidencia con el resultado capturado. Revisión visual de ambos estados. No se verificaron varias escalas DPI entre monitores.


### Ayudas en la conexión Nube

- Placeholders en endpoint S3, bucket, Access Key ID, Secret Access Key, carpeta remota y URL pública. Los ejemplos desaparecen al introducir un valor, sin sustituir las etiquetas ni modificar los datos de conexión.
