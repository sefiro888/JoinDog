# JoinDog

Estado real del proyecto a 16 de agosto de 2026.

JoinDog es el proyecto limpio y vigente del juego movil vertical que nacio desde DOGCRUSH. El objetivo actual es un juego casual tipo match-3, con mapa de niveles, mundos visuales, potenciadores, vidas, objetivos progresivos y version WebGL instalable como PWA.

Este README existe para evitar una confusion importante: dentro del codigo todavia quedan nombres historicos como `DogCrush`, `ChainSelectionController` o `AddChainScore`. Algunos nombres vienen de etapas anteriores y no siempre describen la mecanica actual.

## Estado actual resumido

- Proyecto oficial actual: `C:\Users\sefir\Desktop\JoinDogClean`.
- Repositorio publico actual: `https://github.com/sefiro888/JoinDog`.
- Juego publicado: `https://sefiro888.github.io/JoinDog/`.
- Motor: Unity `6000.5.5f1`.
- Plataforma principal ahora mismo: WebGL movil / PWA instalable.
- Orientacion: movil vertical.
- Campana actual: 70 niveles en una única campaña JoinDog.
- Guardado: local, mediante `PlayerPrefs` y un JSON versionado dentro de `PlayerPrefs`.
- Estado de pruebas: el usuario prueba manualmente en movil; Codex puede compilar, revisar codigo y preparar builds, pero la validacion final de sensacion tactil la hace el usuario.

## Mecanica vigente

La mecanica que manda hoy es intercambio de fichas adyacentes tipo Candy Crush.

El jugador toca una ficha y la mueve hacia una ficha vecina en horizontal o vertical. Si el intercambio crea una combinacion valida, las fichas se quedan intercambiadas y se resuelve la combinacion. Si no crea combinacion, el juego muestra el movimiento y devuelve las fichas a su sitio.

No se juega arrastrando cadenas largas tipo Two Dots.

## Por que hay clases llamadas Chain

Quedan clases y metodos con nombres heredados:

- `ChainSelectionController`
- `ChainInputHandler`
- `ChainLineView`
- `AddChainScore`
- `minChainLength`

La razon es historica: antes se experimento con un sistema de encadenar fichas arrastrando. El proyecto cambio a intercambio de fichas, pero se reutilizo parte del controlador para no romper escenas, referencias de Unity y pruebas.

En el estado actual:

- `ChainSelectionController.adjacentSwapMode` esta activo y dirige el intercambio adyacente.
- `TrySwapAndFindMatches()` es la entrada real para validar una jugada.
- `PreviewSwap()` y `RestorePreviewSwap()` existen para mostrar el intercambio durante el arrastre.
- `AddChainScore()` queda como deuda tecnica y referencia historica; el sistema moderno usa `AddResolutionScore()` para puntuar resoluciones, especiales y cascadas.
- `minChainLength` queda en configuraciones antiguas, pero no debe usarse para balancear la mecanica principal.

Decision oficial: cualquier trabajo nuevo debe asumir match-3 por intercambio, no cadenas.

## Tiempo o movimientos

El juego usa cronometro. No hay contador de movimientos como condicion principal.

Fue una decision de la fase actual para mantener ritmo rapido en movil, permitir bonus de tiempo y hacer que el potenciador de comida tenga sentido. Se puede cambiar en el futuro a sistema de movimientos, pero no es la regla vigente.

Estado actual:

- Cada nivel tiene `durationSeconds`.
- El HUD muestra tiempo restante.
- Las estrellas dependen en parte del tiempo restante.
- El booster de comida suma tiempo.

Decision oficial: por ahora se mantiene cronometro.

## Plan vigente

El plan vigente es la campana de 50 niveles.

Archivo principal:

- `PLAN_CAMPANA_50_NIVELES.md`

Archivo historico:

- `PLAN_MENU_MAPA_30_NIVELES.md`

El plan de 30 niveles fue una fase anterior. Sirve como referencia historica de menu/mapa, pero ya no manda sobre el alcance actual.

Decision oficial: el juego avanza hacia 50 niveles.

## Potenciadores

Hay tres potenciadores principales:

- Pata: refresca completamente el tablero y garantiza que haya movimientos posibles.
- Hueso: limpia una fila o columna. Actualmente alterna segun el nivel: niveles pares limpian columna central, niveles impares limpian fila central.
- Comida: suma 10 segundos al cronometro.

Los potenciadores pueden venir de dos sitios:

- Cantidades iniciales dadas por el nivel.
- Inventario persistente del jugador, gestionado por `PlayerProgressService`.

## Vidas

El sistema de vidas existe y funciona localmente.

- Maximo: 5 vidas.
- Al perder un nivel, baja una vida.
- Si llega a 0 y se reinicia partida, se restauran a 5.
- No hay recuperacion por tiempo real ni servidor.

Decision actual: vidas locales, sin monetizacion real todavia.

## Campana y niveles

La campana actual tiene 50 niveles divididos en cinco mundos:

- Niveles 1-10: Pradera Feliz.
- Niveles 11-20: Bosque Aventura.
- Niveles 21-30: Festival Canino.
- Niveles 31-40: Costa Dorada.
- Niveles 41-50: Cumbres Nevadas.

Los niveles pueden variar por:

- Tamano del tablero.
- Forma del tablero: completo, redondeado o diamante.
- Objetivo: puntos, recoger ficha, crear especiales, limpiar obstaculos o provocar cascadas.
- Obstaculo: ninguno, enredaderas, faroles, arena o hielo.
- Tiempo.
- Dificultad.
- Recompensa en galletas/premios.

Ahora mismo hay una bandera temporal de pruebas:

- `CampaignCatalog.UnlockAllLevelsForTesting = true`

Eso deja todos los niveles abiertos para poder probar rapido. Antes de una version publica seria conveniente ponerlo en `false`.

## Combinaciones especiales

El sistema de fichas especiales esta activo.

- Combinacion de 4: crea rayo horizontal o vertical.
- Combinacion de 5: crea estallido de color.
- Forma T/L: crea explosion de area.
- Combinacion de 6 o mas: crea supernova.
- Especial + especial: crea combinaciones mas potentes.
- Al completar el objetivo, los especiales restantes pueden seguir activandose como bonus final antes de cerrar el nivel.

El sistema ha mejorado, pero sigue siendo una zona importante de pulido visual, sonoro y de claridad.

## Obstaculos

Los obstaculos activos son:

- Enredaderas.
- Faroles.
- Arena.
- Hielo.

Actualmente estan generados o dibujados desde el sistema del juego, con apariencia visual propia por mundo. Todavia necesitan pulido artistico para que se entiendan mejor y se vean mas profesionales.

## Arte y licencias

El proyecto contiene actualmente 98 recursos graficos en formato imagen dentro de `Assets`.

Procedencia conocida por el desarrollo:

- Parte del arte fue generado con IA durante el proceso.
- Parte son recursos temporales o adaptaciones internas.
- Parte son referencias historicas del proyecto DOGCRUSH.

Punto importante: antes de publicar comercialmente hay que crear un archivo de licencias y procedencia. Hoy no existe una licencia cerrada y auditada para todos los PNG.

Decision recomendada:

- Marcar el arte actual como provisional hasta revisar origen.
- Mantener una carpeta clara para arte final.
- No incorporar imagenes nuevas sin anotar procedencia, herramienta, fecha y permiso de uso.

## Plataforma objetivo

La plataforma principal actual es WebGL movil con instalacion tipo PWA.

Ya existe:

- `docs/` como build WebGL publicada por GitHub Pages.
- `Assets/WebGLTemplates/DogCrushTemplate/manifest.webmanifest`.
- `Assets/WebGLTemplates/DogCrushTemplate/service-worker.js`.
- Iconos PWA en `Assets/WebGLTemplates/DogCrushTemplate/icons/`.

Objetivo practico:

- Que el usuario pueda abrir el enlace en movil.
- Que pueda instalarlo como acceso directo/app desde el navegador.
- Que la segunda carga sea mas rapida gracias a cache.

Google Play y App Store no son el objetivo inmediato. Se podrian abordar mas adelante, pero cambian requisitos, builds, privacidad, firma, politicas y posible monetizacion.

## Monetizacion

No hay monetizacion real implementada.

Existen sistemas que podrian servir para monetizacion futura:

- Vidas.
- Tienda.
- Galletas/premios.
- Potenciadores.

Pero ahora mismo son sistemas de juego locales, no compras reales ni anuncios.

Decision actual: proyecto jugable sin monetizacion conectada.

## Publico objetivo

El tono visual y mecanico apunta a publico casual, familiar y amigable.

No esta cerrado legalmente como juego para ninos. Si en el futuro se orienta explicitamente a menores, habra que revisar privacidad, anuncios, analiticas, compras y consentimiento.

Decision recomendada por ahora: tratarlo como juego casual familiar, no como producto infantil regulado.

## Servidor y personalizacion de mascota

Ahora mismo no hay servidor.

Todo el progreso vive localmente en el dispositivo mediante `PlayerPrefs`. Eso significa:

- No hay cuenta de usuario.
- No hay sincronizacion entre moviles.
- No hay ranking online.
- No hay guardado en la nube.
- No hay subida real de foto de mascota.

La idea de subir una foto de la mascota y convertirla al estilo JoinDog es posible, pero requiere backend y una API de imagen.

Para hacerlo bien haria falta:

- Autenticacion de usuario.
- Almacenamiento privado por usuario.
- Backend que reciba la foto.
- Llamada a API de generacion/edicion de imagen.
- Sistema para guardar el resultado sin mezclar mascotas entre usuarios.
- Politica de privacidad y borrado de imagenes.

Decision actual: no implementado.

## Estructura del proyecto

Carpetas principales:

- `Assets/_JoinDog`: menu, mapa, campana, progreso, personajes y estructura nueva.
- `Assets/_DogCrush`: gameplay heredado que sigue siendo usado por la partida.
- `Assets/Resources`: recursos cargados por Unity en runtime.
- `Assets/WebGLTemplates/DogCrushTemplate`: plantilla WebGL/PWA.
- `Packages`: dependencias Unity.
- `ProjectSettings`: configuracion del proyecto.
- `docs`: build WebGL para GitHub Pages.

Aunque existan nombres `DogCrush`, el repositorio actual es JoinDog. No depende del repositorio viejo para funcionar.

## Como probar

Ruta recomendada en Unity:

1. Abrir `C:\Users\sefir\Desktop\JoinDogClean`.
2. Usar Unity `6000.5.5f1`.
3. Abrir la escena `Assets/_JoinDog/Scenes/Boot.unity`.
4. Pulsar Play.

Ruta recomendada en movil:

1. Compilar WebGL a `docs/`.
2. Subir a GitHub.
3. Probar desde `https://sefiro888.github.io/JoinDog/`.

El ciclo de prueba acordado hasta ahora es:

- Codex implementa y compila cuando hace falta.
- El usuario revisa manualmente en movil.
- Se sube a GitHub cuando el usuario pide expresamente subir.

## Tolerancia a cambios

Como regla actual:

- Cambios de gameplay: se pueden hacer con ambicion, pero deben conservar una version jugable.
- Cambios visuales: preferible hacerlos por fases, porque el usuario decide mucho por sensacion en movil.
- Refactor profundo: posible, pero conviene hacerlo por bloques. Hay archivos grandes y nombres heredados; renombrarlo todo de golpe puede romper referencias de Unity.
- Repositorios: `JoinDogClean` es el proyecto oficial. Las copias experimentales deben mantenerse separadas.

Existe una copia local experimental para trabajar con otra IA:

- `C:\Users\sefir\Desktop\joindogexperimentalclaude`

Esa copia no tiene `.git` y no afecta al proyecto principal.

## Presupuesto y recursos externos

Presupuesto actual asumido: cero o muy bajo.

Rutas posibles:

- Arte IA propio: rapido, pero hay que documentar procedencia.
- Assets CC0 o comprados: mejor para licencias, requiere seleccion y coherencia.
- Audio CC0 o comprado: recomendado para mejorar sensacion sin complicar servidor.
- Backend/API para mascota personalizada: coste recurrente y complejidad media-alta.

## Respuestas directas a las dudas bloqueantes

1. Mecanica real: intercambio de fichas adyacentes tipo Candy Crush. El sistema de cadenas es herencia y deuda de nombres.
2. Tiempo: se usa cronometro por decision actual. No hay movimientos como condicion principal.
3. Plan vigente: 50 niveles. El plan de 30 niveles es historico.
4. Boosters: pata refresca tablero, hueso limpia fila/columna, comida suma 10 segundos.
5. Arte: mezcla de IA, temporales y referencias historicas. Falta auditoria de licencias antes de publicar comercialmente.
6. Plataforma: WebGL movil/PWA ahora. Stores mas adelante si se decide.
7. Monetizacion: no implementada. Vidas/tienda son sistemas locales.
8. Publico: casual familiar, no declarado legalmente como infantil.
9. Servidor: no hay. Todo es local. Mascotas personalizadas requieren backend.
10. Cambios: se puede tocar a fondo, pero por fases para no romper Unity.
11. Pruebas: Unity Play y WebGL movil. El usuario valida sensacion real en telefono.
12. Presupuesto: por ahora bajo/cero; si se compra arte/audio hay que documentarlo.

## Siguiente deuda tecnica recomendada

- Renombrar gradualmente `ChainSelectionController` a algo como `SwapInputController`.
- Separar el codigo de gameplay heredado de nombres `DogCrush` sin romper escenas.
- Crear `LICENSES.md` o `ART_SOURCES.md` para procedencia de recursos.
- Desactivar `UnlockAllLevelsForTesting` antes de version publica.
- Mejorar HUD, tienda, resultados y menus con un sistema visual final.
- Revisar rendimiento/carga WebGL cada vez que entren mundos o assets nuevos.
