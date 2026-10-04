# JoinDog: experiencia visual, 2 de octubre de 2026

Proyecto: `C:/Users/sefir/Desktop/JoinDogClean`, remoto `sefiro888/JoinDog`.
Rama: `codex/joindog-visual-experience`. La campaña existente conserva sus 100 niveles,
sus diez mundos ilustrados, sus menús y la selección de mascotas.

## Cambios

- Cada partida usa la ilustración de su mundo; la pradera conserva su fondo original.
- Doce motas suaves en los bordes, fuera del tablero. Se reutilizan entre reinicios y
  desaparecen al activar movimiento reducido.
- Tarjetas de misión, compañero y ayudas con esquinas redondeadas y textos más grandes.
- Reloj sin parpadeo, con avisos puntuales. El contador distingue tiempo y movimientos.
- Las estrellas muestran el ritmo según los recursos restantes y el reto de habilidad,
  siguiendo el cálculo del resultado final.
- Las buenas jugadas envían una estrella hacia la misión. Máximo tres vuelos simultáneos,
  sin interceptar toques y desactivados con movimiento reducido.
- Las reacciones del compañero vuelven a la ayuda normal después de 2,4 segundos.
- Menos sacudidas de cámara: reservadas para jugadas grandes, cascadas y especiales.
- Cabecera del mapa con progreso en dos líneas y botones de volver/retos separados.
- La ficha del nivel usa el diseño que realmente se juega: objetivo, icono, duración
  y golpes de los obstáculos. El mapa consulta una interfaz compartida del recurso
  de nivel, sin duplicar las reglas ni crear referencias circulares entre ensamblados.
- El texto de ritmo y las estrellas tienen su propia fila, separada de la barra.
- Textos de continuar y descripción de las transiciones de zona más legibles.
- Cien nombres distintos, con la tanda de Cumbre Luminosa restaurada para que
  las últimas cuatro zonas tengan sus títulos correspondientes.
- Retos diarios con título y contadores en placas separadas y cierre visible.
- Colección con título de alto contraste, etiquetas más grandes y salida directa al
  menú, independiente de las recompensas. Texto de ayuda más grande.
- El compañero del menú también respeta movimiento reducido.
- HUD escalado por altura y tablero encajado en la misma columna de retrato en todas
  las orientaciones. El cálculo reserva también el marco y evita que invada la misión.
- Pantalla de carga con mascota, colores de JoinDog y animación que respeta las preferencias
  de movimiento del navegador.
- Corrección funcional: las misiones de colección siempre incluyen sus fichas necesarias
  en el grupo activo. El nivel 16 podía pedir pelotas sin generarlas. Se conserva la
  cantidad de tipos elegida para cada nivel manual.
- La cuerda aparece desde el comienzo de su mundo.
- La exportación conserva archivos comprimidos sin cambios para permitir compilaciones
  incrementales; Unity actualiza los archivos modificados.

## Validación

62 pruebas superadas: 53 EditMode y 9 PlayMode. Incluyen los 100 objetivos de colección,
la coherencia de las fichas de nivel, los diez fondos, movimiento reducido, estabilidad
del reloj, límite/limpieza de las animaciones de recompensa, salida de la colección
sin alterar monedas o iniciar una partida, y espacio del tablero en cuatro proporciones.
Resultados XML: `Logs/visual-edit-tests-responsive.xml` y
`Logs/visual-play-tests-responsive.xml`.
Revisión del navegador: menú, mascotas, ayuda, colección y cierre, ficha de nivel,
intercambio con cascada y progreso, ajustes, pausa, movimiento reducido, salida y
resultado de derrota, tienda y transiciones del mapa. Partidas comprobadas en
390×844, 320×640 y 1280×800 sin cortes de marco ni invasión de la misión.
Retos diarios corregidos y comprobados visualmente. Recorrido por los diez mundos,
con tarjetas finales comprobadas en los niveles 61 y 100. Consola del navegador
sin errores durante las comprobaciones. Capturas en `Builds/visual-qa`.
Exportación final completada: `Logs/visual-experience-release-final.log`,
resultado `Succeeded`, cero errores. Vista local: `http://127.0.0.1:8766/`.
La versión anterior se conserva en `Builds/visual-baseline/docs`.
No se ha publicado en GitHub Pages.
