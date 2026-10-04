# JoinDog: Frisbee Cometa y guía jugable

Proyecto: C:/Users/sefir/Desktop/JoinDogClean. Continúa la rama
codex/joindog-visual-experience y conserva los cambios anteriores.

- Nuevo especial Frisbee Cometa: cuatro frisbees en línea crean una ficha
  con marcas en X. Su activación elimina ambas diagonales, cruza los huecos
  de los tableros irregulares y puede activar otros especiales.
- Las combinaciones de cinco, seis y siete conservan su prioridad. La creación
  da el mismo bono de 450 puntos que los especiales de cuatro fichas.
- Rayos diagonales cortos y delimitados por el tablero; se omiten en movimiento
  reducido. No se incorporan nuevos archivos de arte ni nuevas categorías al azar.
- Guía de cuatro páginas accesible desde CÓMO JUGAR: jugadas, especiales,
  Cometa y objetivos/obstáculos/mascota. Navegación anterior, siguiente y cierre.
- Movimiento reducido detiene los saltos del compañero y evita o detiene las
  animaciones de creación y carga de especiales. Los indicadores quedan visibles.

Validación: 60 pruebas EditMode y 14 PlayMode superadas. XML en
Logs/comet-edit-tests.xml y Logs/comet-play-tests.xml. Incluyen creación real,
diagonales en bordes, cadena sin duplicados, prioridad de combinaciones,
navegación de la guía y cambio a movimiento reducido durante un salto.
Imagen renderizada por Unity para revisión: Builds/visual-qa/comet-piece.png.

Guía comprobada visualmente en 320×640, 390×844 y 1280×800; avance,
retroceso y cierre verificados. Texto ampliado tras la prueba del móvil pequeño.
Partida de costa revisada con frisbees presentes y tablero/objetivo legibles.
Consola del navegador sin errores durante la revisión. Capturas en Builds/visual-qa.
La guía aclara que la ayuda de la mascota se activa automáticamente.

Exportación final completada: Logs/comet-release-final.log, resultado Succeeded,
cero errores. Vista local: http://127.0.0.1:8766/. Sin publicación remota.
