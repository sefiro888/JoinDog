# JoinDog: impactos y cascadas más vivos

Continuación local en JoinDogClean. Se mantienen los 100 niveles, el mapa, los menús, las reglas, las puntuaciones y los especiales anteriores.

- Corregida la emisión de partículas: SetBurst(0, ...) se invocaba sin configurar burstCount, y Unity ignoraba la ráfaga. Ahora se configura una ráfaga real y se desactiva la emisión continua.
- Partículas de vida y recorrido breves para concentrar el impacto en la jugada.
- Ondas escalonadas en las casillas eliminadas. Cuatro fichas añaden un pulso dorado y destellos; cinco o más añaden una segunda onda; las grandes combinaciones tienen destellos multicolor. Las cascadas usan ondas turquesa de intensidad acotada.
- Pequeña compresión antes del estallido de cada ficha. La salida dura 0,26 segundos; movimiento reducido usa un desvanecimiento sin deformación.
- Efectos adicionales reutilizables, con un máximo de 40 sprites. No bloquean la gravedad ni añaden esperas. Las posiciones se copian antes de reciclar las fichas.
- Limpieza al reiniciar/desactivar y al activar movimiento reducido durante una animación. En movimiento reducido hay como máximo tres marcas estáticas breves por jugada.

## Comprobaciones

60 pruebas EditMode y 18 PlayMode superadas, más una prueba nueva de renderizado de las cuatro categorías visuales. Las cinco pruebas específicas de impactos se volvieron a ejecutar tras ajustar el tamaño: todas superadas. 79 pruebas distintas en total.

Resultados: Logs/impact-edit-tests.xml, Logs/impact-play-tests-final.xml y Logs/impact-focused-tests.xml.

Capturas reales del renderizado de Unity: Builds/visual-qa/match-impact-3.png, match-impact-4.png, match-impact-5.png y match-impact-7.png. Son escenas controladas para revisar los efectos; no capturas de una partida completa.

Compilación WebGL completada con cero errores (Logs/impact-release.log), versión dcf6ca17b8afcf32134b3f53. Disponible localmente en http://127.0.0.1:8766/?preview=impact-dcf6ca17.

Revisión visual en navegador a 390 × 844: combinación válida, partículas visibles, cascada, actualización de puntuación y misión, relleno completo del tablero y devolución de un intercambio inválido. Comprobado también el regreso al mapa ilustrado. No hubo errores de consola; permanece un aviso previo de compatibilidad del shader FSR de URP. La activación de un especial no se verificó en esta partida. No se publica ni se hace push durante esta fase.
