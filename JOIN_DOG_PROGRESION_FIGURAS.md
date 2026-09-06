# JoinDog: descubrimiento, variedad y recompensas

6 de septiembre de 2026. Estado auditado localmente. Patito, cuerda, frisbee y
pingüino ya se incorporan como fichas jugables progresivas.

## Fase 1: estrenar juguetes

1. [IMPLEMENTADO] Patito amarillo como sexta ficha desde el nivel 11, junto a las cinco actuales.
2. [IMPLEMENTADO] Presentación del juguete en la ficha del nivel 11 y aviso inicial con reloj pausado.
3. [IMPLEMENTADO] Cuerda de juego desde el nivel 21.
4. [IMPLEMENTADO] Frisbee azul desde el nivel 31, al llegar a Costa Dorada.
5. [IMPLEMENTADO] Peluche de pingüino desde el nivel 41, al llegar a Cumbres Nevadas.
6. [IMPLEMENTADO] Selecciones temáticas de cinco a nueve fichas por bloque de
   mundo: una colección grande no obliga a introducir todos los tipos
   simultáneamente, y la selección se conserva durante las caídas.

## Fase 2: descubrir y coleccionar

7. [IMPLEMENTADO] Álbum de las nueve figuras actuales con cuadrícula 3x3.
8. [IMPLEMENTADO] Indicador de cuántos niveles faltan para la siguiente figura.
9. [IMPLEMENTADO] Pequeña escena de presentación al abrir una zona, con nombre,
   color del mundo y recuerdo de primera visita.
10. [IMPLEMENTADO] Recuerdos de cada mundo ganados al completar sus niveles.
11. [IMPLEMENTADO] Aura cosmética desbloqueable con estrellas, sin cambiar el
    poder de ninguna ficha.
12. [IMPLEMENTADO] Botón para volver al nivel actual después de explorar el mapa.

12b. [IMPLEMENTADO] Recompensas de colección por grupos de 3 y 6 figuras,
     además del premio final al completar las 9.

## Fase 3: decisiones en el tablero

13. [IMPLEMENTADO] Misiones de llevar un juguete hasta la salida inferior.
14. [IMPLEMENTADO] Rescatar cachorros liberando jaulas en niveles intermedios.
15. [IMPLEMENTADO] Recoger juguetes de dos tipos en una misma partida en
    niveles intermedios de cada capítulo.
16. [IMPLEMENTADO] Rondas especiales por movimientos para alternar con los niveles
    de tiempo (niveles 18, 38, 58, 78 y 98).
17. [IMPLEMENTADO] Finales de mundo con misión propia de limpieza de obstáculos,
    tipo de obstáculo y recompensa especial por capítulo.
18. [IMPLEMENTADO] Explicación visual breve de las combinaciones entre dos
    especiales.

## Fase 4: satisfacción y ganas de volver

19. [IMPLEMENTADO] Mostrar tres estrellas ilustradas en el HUD y comunicar el
    siguiente umbral (30%, 60% o 100%) durante la partida.
20. [IMPLEMENTADO] Celebrar por separado la primera victoria y la mejora del
    récord personal.
21. [IMPLEMENTADO] Mostrar el avance hacia la recompensa de zona directamente en
    el mapa.
22. [IMPLEMENTADO] Objetivo opcional de sesión: conseguir tres estrellas nuevas.
23. [IMPLEMENTADO] Animación del compañero reaccionando al rescate y a las
    cascadas.
24. [IMPLEMENTADO] Selección de niveles favoritos para repetirlos rápidamente.

## Validación pendiente en móvil

- Mantener los valores guardados de las cinco fichas anteriores.
- Las nueve fichas están presentes en generación, relleno y cambios de tipo.
- Las fronteras de desbloqueo son 10/11, 20/21, 30/31 y 40/41.
- La tasa real de victorias y el equilibrio con las bolsas temáticas necesitan
  partidas en móvil; no es una función pendiente de código.

## Arte del patito

Creado con la herramienta integrada de imágenes. Archivo:
`Assets/_DogCrush/Resources/Pieces/piece-duck-v1.png`.
Prompt: un único patito de goma amarillo con pico naranja, juguete canino,
ilustración cartoon 3D pulida, silueta legible a 48 píxeles, centrado en lienzo
cuadrado, fondo transparente, sin texto, marco ni otros objetos.
