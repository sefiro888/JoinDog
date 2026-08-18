# JoinDog — hoja de ruta de mejoras

Actualizado: 18 de agosto de 2026

Esta hoja de ruta conserva la mecánica de intercambio adyacente, la campaña de
70 niveles y la orientación WebGL móvil. Cada fase se entrega en un commit
separado y debe validarse antes de publicar.

## Fase 1 — Claridad móvil y confianza del jugador

- [x] Corregir la visualización inicial de las tres estrellas del objetivo.
- [x] Reforzar el aviso visual cuando quedan diez segundos o menos.
- [x] Cambiar el color del progreso del objetivo según su avance.
- [x] Diferenciar con claridad los potenciadores disponibles y agotados.
- [x] Ampliar las pruebas automáticas a los 70 niveles y siete zonas.
- [ ] Añadir una tarjeta breve de objetivo al comenzar cada nivel.

## Fase 2 — Niveles 51–70 y obstáculos

- [x] Dar una identidad jugable propia a Valle Aurora, además de su aspecto.
- [x] Dar una identidad jugable propia a Cumbre Luminosa.
- [x] Diseñar patrones de faroles específicos para los niveles 51–60.
- [x] Diseñar patrones de hielo específicos para los niveles 61–70.
- [x] Crear una transición especial antes de los niveles 60 y 70.
- [x] Añadir una celebración exclusiva al completar el nivel 70.

## Fase 3 — Progresión y recompensas

- [x] Mostrar recompensas futuras en el mapa antes de alcanzarlas.
- [x] Añadir una recompensa diaria sencilla sin servidor.
- [x] Crear y mostrar rachas diarias de juego.
- [x] Dar premios por reunir estrellas de una zona completa.
- [x] Añadir objetivos secundarios opcionales en niveles avanzados.
- [x] Crear una pantalla de colección de compañeros.

## Fase 4 — Variedad y estrategia

- [x] Añadir un obstáculo que se expanda si no se limpia.
- [x] Añadir casillas que cambien el tipo de ficha al caer.
- [x] Crear niveles con dos objetivos simultáneos.
- [x] Añadir desafíos sin potenciadores para una estrella extra.
- [x] Introducir tableros asimétricos adicionales.
- [x] Mejorar el sistema de pistas para priorizar el objetivo real.

## Fase 5 — Pulido, accesibilidad y rendimiento

- [ ] Añadir opción de movimiento reducido.
- [ ] Añadir modo de contraste reforzado para obstáculos.
- [ ] Mejorar el tamaño táctil mínimo de todos los botones.
- [ ] Reducir objetos animados fuera de la zona visible del mapa.
- [ ] Optimizar la carga inicial y el caché PWA.
- [ ] Añadir una comprobación automática de errores de consola WebGL.

## Criterio de entrega

Cada fase debe compilar sin errores, mantener accesibles los 70 niveles y dejar
el repositorio en un commit identificable. La publicación móvil se hace solo
después de una comprobación local correcta.
