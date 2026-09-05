# JoinDog — documento de continuidad

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

## Última mejora aplicada

- Patito amarillo como sexta ficha desde el nivel 11, sin retirar las cinco
  anteriores. Integrado en generación, relleno y cambios de tipo.
- Aviso al entrar en el nivel 11, con el temporizador pausado durante la introducción.
- Seis pruebas de recursos y desbloqueo correctas; WebGL con cero errores
  (`Logs/duck-webgl.log`). Equilibrio con seis tipos pendiente de partidas en móvil.
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

## Siguiente trabajo recomendado

### Celebraciones de combinaciones — revisión compartida

- Tres fichas: huellas breves sin cartel habitual; cuatro: «¡GENIAL!»; cinco: «¡INCREÍBLE!»; seis o más: «¡ESPECTACULAR!».
- Cascadas: «¡COMBO ×N!» con prioridad sobre el mensaje de tamaño. Se usa OriginalMatchCount, no la cantidad extra eliminada por un especial.
- Cartel con degradado, contorno y rebote contenido en la franja del compañero; restaura su texto al terminar y no recibe pulsaciones.
- Estrellas alrededor de combinaciones grandes, multicolor desde seis; máximo 18 sprites adicionales activos y duración de 0,55 s.
- Puntuaciones y nombres de especiales con contorno, pequeño rebote y sin bloqueo táctil.
- Movimiento reducido desactiva los nuevos destellos, desplazamiento y rebote; también evita sacudidas de cámara.
- La barra ahora dice «AYUDA DEL PERRITO» e incluye una explicación de cómo cargarla.
- Validación compartida: el usuario revisa estética, fluidez y legibilidad en móvil para ahorrar rondas de inspección automática.
- Seis pruebas EditMode correctas (`TestResults/celebration.xml`) y build WebGL sin errores (`Logs/celebration-webgl.log`). Versión `b04156a7fdedf0d290fef425`; revisión visual/jugable pendiente del usuario.

#### Comprobación del usuario

1. Unir 3, 4 y 5 fichas; confirmar diferencias y que las fichas especiales se siguen creando.
2. Provocar una cascada; comprobar el contador ×2, ×3… y que no parpadeen varios carteles superpuestos.
3. Activar una especial y, si surge, combinar dos: comprobar que se distinguen de su creación.
4. Verificar que el mensaje del compañero reaparece tras la celebración y que su carga funciona.
5. Activar movimiento reducido en ajustes: comprobar ausencia de rebotes, nuevas estrellas y sacudidas.
6. Jugar varias partidas seguidas: comprobar fluidez y controles. Enviar vídeo corto o captura, nivel y acción si algo falla.

### HUD de aventura — 5 de septiembre de 2026

- Se sustituyen las bandas oscuras con marcos anidados por tarjetas crema y turquesa.
- Cabecera compacta: volver, ajustes, nivel, tiempo destacado y vidas.
- Objetivo separado sobre el tablero, con icono de la figura y nombres en español.
- Barra de carga del compañero y tres potenciadores grandes con sus cantidades.
- Se reutilizan controles, eventos y recursos existentes; no cambian reglas ni guardados.
- El tablero reserva altura para los nuevos paneles en vertical.
- Primera build WebGL sin errores y pantalla de partida comprobada en navegador vertical (420×900).
- Verificados visualmente el panel inferior completo, el consumo de +10 s y la apertura de ajustes.
- Pendiente de valoración estética del usuario en su móvil.

### Álbum de figuras — siguiente fase

- Nuevo acceso «ÁLBUM DE FIGURAS» desde el menú; seis tarjetas con arte existente.
- Las cinco figuras originales están disponibles desde el inicio. El patito se descubre al alcanzar el nivel 11 con progreso ganado.
- Vista atenuada del patito pendiente e indicador de niveles restantes. No se anuncian juguetes que todavía no existen.
- `EarnedUnlockedLevel` separa este progreso del desbloqueo global de pruebas del mapa; no modifica el guardado.
- Cinco pruebas EditMode correctas: fronteras 1/10/11/100, recursos distintos y mensajes del siguiente hito.
- WebGL compilada con cero errores (`Logs/figure-album-webgl.log`), versión `383bfaa917d56ed2570c9b59`.
- Verificados en navegador 420×900: botón del menú, seis tarjetas completas y cierre del álbum.

- Probar en uno o dos móviles físicos las transiciones 70→71, 80→81 y 90→91,
  además del final 100.
- Ajustar la curva experta de los niveles 81–100 según esas primeras partidas.
- Después, limpiar gradualmente nombres históricos mediante migración controlada.

## Cómo retomar en una conversación nueva

Pegar este documento o decir:

> Continuamos JoinDog desde `JOIN_DOG_HANDOFF.md`. Usa exclusivamente `C:\Users\sefir\Desktop\JoinDogClean`, no uses DOGCRUSH, conserva la mecánica y los 100 niveles, y no borres referencias internas sin migrarlas y probar una build.
