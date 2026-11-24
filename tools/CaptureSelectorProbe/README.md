# Prueba del selector nativo

Prototipo aislado: no cambia el capturador de IMG Viewer y no añade dependencias NuGet. Usa una ventana Win32 mediante Windows Forms y GDI BitBlt, con doble buffer, escritorio oscurecido preparado una sola vez y regiones de repintado. El proyecto ya usa Windows Forms.

Desde la raíz del repositorio:

```sh
dotnet run --project tools/CaptureSelectorProbe/CaptureSelectorProbe.csproj -c Release
```

Congela el escritorio una sola vez. Primero abre el selector nativo y después el WPF sobre los mismos píxeles. Arrastra y suelta para seleccionar. En el nativo, Esc cancela el arrastre y un segundo Esc cierra; Enter selecciona el monitor. No guarda imágenes, no modifica el portapapeles y no sube información. Los resultados se descartan al terminar.

Solo el nativo:

```sh
dotnet run --project tools/CaptureSelectorProbe/CaptureSelectorProbe.csproj -c Release -- --native
```

Comparación automática de una trayectoria de 120 pasos sobre la misma captura congelada:

```sh
dotnet run --project tools/CaptureSelectorProbe/CaptureSelectorProbe.csproj -c Release -- --benchmark
```

Agregar `--synthetic` evita capturar el escritorio; `--wpf-first` invierte el orden del benchmark. Las mediciones son intervalos entre actualizaciones programadas, CPU del proceso y memoria residente al final. No miden latencia física mouse-pantalla ni prueban suavidad perceptual. Ambos motores reciben la misma trayectoria y no hacen detección automática de áreas: se compara únicamente el selector manual. El escritorio abarca todos los monitores y las coordenadas se expresan en píxeles físicos.

Comprobaciones básicas:

```sh
dotnet run --project tools/CaptureSelectorProbe/CaptureSelectorProbe.csproj -c Release -- --check
```

## Resultado local, 23 de noviembre de 2026

Una captura de 5760 × 2280 píxeles, 119 intervalos, nativo primero:

| Selector | Mediana | Percentil 95 | Máximo | CPU del proceso durante la fase |
| --- | ---: | ---: | ---: | ---: |
| Nativo con BitBlt | 31,79 ms | 66,35 ms | 83,25 ms | 3109 ms |
| WPF con DrawingVisual | 32,46 ms | 56,38 ms | 82,67 ms | 2078 ms |

La primera variante con GDI+ dio 51,75 ms de mediana; sustituir sus copias por BitBlt mejoró el prototipo. Aun así, esta prueba no demuestra una ventaja clara del nativo: los percentiles y CPU fueron peores que WPF. En esa primera comparación no se sustituyó el capturador habitual. Posteriormente, tras probar el arrastre manual y confirmar el usuario una mejora perceptual, se integró una variante con detección de áreas y pantalla completa en la aplicación. Esta herramienta permanece aislada como referencia A/B.

La memoria residente fue 324 MB al terminar la fase nativa y 823 MB tras WPF, pero ambas fases compartieron proceso y cachés y se ejecutaron en ese orden. Estos valores no permiten atribuir la diferencia íntegra a un motor. Para comparar memoria correctamente harían falta procesos aislados y repeticiones en ambos órdenes.

La automatización visual verificó la ventana y el sombreado, pero no produjo una selección válida en el primer arrastre sintético. Se corrigió el punto inicial/final del prototipo para usar las coordenadas del mensaje de botón. Falta confirmar manualmente la fluidez de esta variante final; no se declara solucionado el atasco.

Limitaciones: es solo selección manual y monitor completo con Enter. No incluye detector de ventanas/controles, miniatura ni integración con el historial. La aplicación principal conserva sus funciones.

## Dimensiones en tiempo real — solo prototipo

El selector nativo de esta herramienta muestra ancho × alto en píxeles durante el arrastre. La etiqueta se dibuja directamente con GDI en un espacio fijo, sin medir controles WPF, y se mantiene dentro del monitor. Se limpia su posición anterior para evitar rastros y se oculta al cancelar. Este experimento no modifica el selector integrado de la app.

Para probar únicamente el nativo:

    dotnet run --project tools/CaptureSelectorProbe/CaptureSelectorProbe.csproj -c Release -- --native
