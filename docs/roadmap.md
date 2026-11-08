# IMG Viewer

Visor de imágenes de escritorio para Windows, desarrollado con WPF y .NET 10. El objetivo es ofrecer funciones sencillas y útiles para ver imágenes y preparar capturas de pantalla.

Las cinco etapas iniciales están completadas. Los cambios posteriores se registran en el [registro de cambios](changelog.md).

## Etapa 1 · Abrir y recorrer imágenes

- [x] Abrir imágenes JPG, PNG o BMP arrastrándolas a la ventana.
- [x] Abrir una imagen desde el Explorador de Windows mediante **Abrir con**.
- [x] Ajustar la imagen al espacio disponible.
- [x] Recorrer las imágenes de la misma carpeta por nombre con las flechas del teclado o la rueda del mouse.
- [x] Verificar la apertura y la navegación en la app con tres imágenes de una carpeta.

## Etapa 2 · Controles del visor

- [x] Mover la ventana con arrastre izquierdo sobre la foto o el fondo del visor y mostrar el cursor de mano.
- [x] Alternar la pantalla completa con doble clic sobre la imagen. Pulsar Esc también permite salir.
- [x] Ampliar y reducir la imagen con Ctrl + rueda.
- [x] Desplazar la imagen ampliada con Ctrl + arrastre con el botón izquierdo.
- [x] Alternar el fondo oscuro y el damero de transparencia con T.

## Etapa 3 · Presentación y Papelera

- [x] Reproducir y pausar una presentación de la carpeta en pantalla completa, en el mismo orden que las flechas.
- [x] Enviar la imagen actual a la Papelera con Supr y mostrar la siguiente disponible.
- [x] Recuperar con Shift + Supr la última imagen enviada a la Papelera desde el visor durante la sesión.

## Etapa 4 · Edición rápida e información

- [x] Girar 90° en sentido horario con R y antihorario con Shift + R.
- [x] Recortar con un marco libre y enderezar de −15° a +15° en una misma vista previa.
- [x] Dibujar flechas con Alt + arrastre y rectángulos con Shift + arrastre. Deshacer la última marca con Ctrl + Z.
- [x] Guardar sobre el original con Ctrl + S o guardar una copia con Ctrl + Shift + S.
- [x] Mostrar dimensiones, tamaño del archivo y datos EXIF disponibles en un pie que se abre y cierra con el botón de información.

## Etapa 5 · Captura de pantalla

- [x] Iniciar con Impr Pant una captura global y seleccionar una ventana visible, un área libre o un monitor completo.
- [x] Abrir la captura directamente en IMG Viewer para marcarla, copiarla o guardarla.
