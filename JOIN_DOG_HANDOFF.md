# JoinDog — documento de continuidad

## Orden vigente — 6 septiembre 2026

Solo trabajo local: NO publicar ni hacer push a GitHub. Rediseño Magia en `JOIN_DOG_MAGIC_LOCAL.md`.
El usuario realiza la revisión visual para ahorrar rondas. Código y arte nuevo están en JoinDogClean; no usar el proyecto antiguo.

Actualizado: 5 de septiembre de 2026

## Fuente única del proyecto

Desde ahora el proyecto vigente es exclusivamente:

`C:\Users\sefir\Desktop\JoinDogClean`

Repositorio oficial:

`https://github.com/sefiro888/JoinDog`

Rama de pruebas/publicación actual:

`codex/prueba-movil-companero`

El proyecto antiguo `DOGCRUSH` y su repositorio `sefiro888/dogcrush` quedan archivados. No deben usarse para nuevas ediciones, builds ni enlaces de prueba.

## Enlaces actuales

- Local: `http://127.0.0.1:8766/`
- Móvil publicado: `https://sefiro888.github.io/JoinDog/` (usar el enlace versionado de la última entrega para actualizar).
- Repositorio: `https://github.com/sefiro888/JoinDog/tree/codex/prueba-movil-companero`

## Estado confirmado

- Unity: `6000.5.5f1`.
- Plataforma: WebGL móvil vertical/PWA.
- Menú independiente, mapa de campaña y entrada a partida funcionando.
- Mecánica vigente: intercambio de fichas adyacentes en horizontal/vertical.
- El movimiento se previsualiza durante el arrastre; si no crea combinación, las fichas vuelven a su sitio.
- Especiales, cascadas, objetivos, obstáculos, energía del perro, compañero y potenciadores están integrados.
- Campaña integrada: niveles 1–100.
- Zonas de campaña: Parque Central, Bosque Aventura, Festival Canino, Costa Dorada, Cumbres Nevadas, Valle Aurora, Cumbre Luminosa, Jardines Celestes, Cañón de Rubíes y Santuario Dorado.
- La compilación WebGL de JoinDog fue comprobada como correcta y publicada en `9fd93cf`.
- Las 30 mejoras de `JOIN_DOG_ROADMAP_MEJORAS.md` están aplicadas localmente en cinco fases.
- La nueva WebGL local fue validada con 21 pruebas de edición, 7 pruebas jugables y una auditoría de consola móvil sin errores.
- La build de 100 niveles con puertas ampliadas, plazas de transición y rutas
  ocultas dentro del umbral usa la versión de caché `e6ed6496fed10f291f9d10d0`
  y se compiló con cero errores.

## Estado de cierre de la implementación actual

- La progresión ya cubre nueve fichas jugables: patito desde el nivel 11,
  cuerda desde el 21, frisbee desde el 31 y pingüino desde el 41. Los grupos
  temáticos se respetan durante las caídas y las salidas.
- La campaña está preparada hasta el nivel 100, con diez mundos de diez
  niveles, finales de mundo, rescates, entregas, objetivos dobles y rondas por
  movimientos.
- Las tarjetas de nivel usan el arte, emblema, paleta, regla y motivo visual
  del mundo correspondiente; ya no existe una tarjeta genérica compartida.
- Están integrados álbum, recompensas de colección, recuerdos de mundo,
  favoritos, aura estelar, música por mundo, control de música y mascota
  seleccionada en la ayuda del tablero.
- La build WebGL local más reciente se verificó con cero errores en
  `Logs/music-settings-build.log` y se sirve desde `JoinDogClean\docs`.
- 24 propuestas por fases en `JOIN_DOG_PROGRESION_FIGURAS.md`.

- Las estrellas de la pantalla de victoria usan la misma ilustración que las
  estrellas del mapa, evitando cuadrados o símbolos de fuente según el móvil.
- Las estrellas conseguidas aparecen con una entrada progresiva y el resto
  quedan visibles como recompensas pendientes.

## Regla de seguridad principal

No borrar ni renombrar todavía `Assets/_DogCrush`. Aunque el nombre sea histórico, Unity aún tiene escenas, scripts, prefabs, namespaces y claves de guardado que dependen de esas rutas. Primero habría que migrar referencias y probar una build; solo después se podría retirar por fases.

Que existan nombres internos `DogCrush` no significa que el proyecto use el repositorio antiguo. JoinDogClean es independiente y contiene todo lo necesario para funcionar.

## Proceso de trabajo a partir de ahora

1. Trabajar únicamente dentro de `JoinDogClean`.
2. Antes de una modificación grande, crear una rama de prueba o un commit identificable.
3. No compilar nunca WebGL hacia `C:\Users\sefir\Desktop\DOGCRUSH\docs`.
4. Las builds de JoinDog se generan en `JoinDogClean\docs`.
5. El usuario realiza la comprobación final en móvil.
6. Si una mejora falla, volver al commit anterior de JoinDog; no mezclar archivos del proyecto antiguo.
7. Publicar en GitHub solo después de que la comprobación local sea correcta.

## Cierre pendiente de esta fase

La implementación funcional está hecha. Solo queda la validación física que no
puede certificarse desde el navegador de desarrollo:

1. Unir 3, 4 y 5 fichas, provocar cascadas y combinar especiales; confirmar
   legibilidad, prioridad de carteles y reacciones del compañero.
2. Probar los desbloqueos de patito, cuerda, frisbee y pingüino en los niveles
   11, 21, 31 y 41, incluyendo varias partidas para valorar equilibrio.
3. Probar finales 70, 80, 90 y 100, además de 70→71, 80→81 y 90→91, para
   confirmar puertas, espacio, rutas y ausencia de solapes.
4. Probar álbum, reclamos, favoritos, recuerdos, aura, música, foto local y
   guardado tras recargar en uno o dos móviles.
5. Revisar rendimiento, audio y mezcla en un móvil modesto.

La última build local se sirve desde `http://127.0.0.1:8766/`; no se publica
nada durante esta fase.

## Cómo retomar en una conversación nueva

Pegar este documento o decir:

> Continuamos JoinDog desde `JOIN_DOG_HANDOFF.md`. Usa exclusivamente `C:\Users\sefir\Desktop\JoinDogClean`, no uses DOGCRUSH, conserva la mecánica y los 100 niveles, y no borres referencias internas sin migrarlas y probar una build.
