# JOIN DOG — Plan profesional: menú, mapa y campaña escalable

## 1. Objetivo de este bloque

Construir una experiencia independiente y escalable con este recorrido:

`Arranque ligero -> Menú principal -> Mapa de niveles -> Partida -> Resultado -> Mapa`

La primera campaña tendrá 30 niveles. El sistema deberá permitir añadir después más niveles, nuevos mapas y nuevas zonas sin reconstruir la interfaz ni depender de una imagen fija que contenga los botones.

Este bloque no rediseñará la mecánica del tablero. La jugabilidad existente se conservará y se conectará al nuevo flujo cuando el mapa esté preparado.

## 2. Decisiones de arquitectura

### Escenas independientes

- `Boot`: escena mínima que carga los servicios y decide qué pantalla abrir.
- `MainMenu`: portada completamente independiente; no crea ni muestra el tablero.
- `WorldMap`: mapa navegable con camino, perro y nodos de nivel.
- `Gameplay`: escena actual de la partida, adaptada para recibir un identificador de nivel.

Un objeto persistente `AppServices` sobrevivirá a los cambios de escena y contendrá solamente servicios globales: progreso, navegación, audio, ajustes y carga de recursos. No contendrá tablero ni elementos visuales específicos de una escena.

### Datos independientes del aspecto visual

- `CampaignDefinition`: catálogo de la campaña y sus mundos.
- `WorldDefinition`: nombre, tema visual, orden, fondo y lista de niveles.
- `LevelDefinition`: reglas y objetivo del nivel, sin posiciones del mapa.
- `MapNodeDefinition`: posición visual, tipo de nodo y referencia al nivel.
- `PlayerProgressData`: niveles desbloqueados, estrellas, puntuaciones y última posición del perro.

Cada nivel tendrá un identificador estable, por ejemplo `park_001`, y no dependerá solamente de su posición en una lista. Así podremos insertar niveles sin destruir partidas guardadas.

### Mapa construido por capas

El fondo será decorativo. El camino, los círculos, candados, estrellas, números y el perro serán elementos independientes colocados por Unity.

Esto permite:

- añadir o mover niveles sin regenerar el fondo;
- cambiar el aspecto de un mundo sin cambiar los datos;
- reutilizar los mismos nodos en nuevos mapas;
- adaptar el mapa a distintas pantallas;
- cargar mapas futuros por bloques.

## 3. Estructura de carpetas propuesta

```text
Assets/_JoinDog/
  App/
    Boot/
    Navigation/
    Services/
  Campaign/
    Data/
    Progress/
    Runtime/
  MainMenu/
    Scenes/
    Scripts/
    Prefabs/
    Art/
  WorldMap/
    Scenes/
    Scripts/
    Prefabs/
    Art/
    Worlds/
  Gameplay/
    (referencias al sistema actual durante la migración)
  Shared/
    UI/
    Audio/
    Fonts/
    Transitions/
```

Los nombres internos antiguos `DogCrush` se migrarán únicamente cuando sea seguro. No se renombrarán masivamente, porque rompería referencias de Unity.

## 4. Plan por fases

### Fase 0 — Punto seguro y contrato de navegación

Trabajo:

- crear una rama o etiqueta estable antes del cambio;
- inventariar escenas y dependencias actuales;
- definir el flujo exacto entre pantallas;
- documentar qué datos viajan hacia y desde `Gameplay`;
- acordar identificadores estables para niveles y mundos.

Criterio de cierre:

- versión actual recuperable;
- diagrama de navegación aprobado;
- ningún cambio visual o de jugabilidad todavía.

### Fase 1 — Núcleo de aplicación y escenas separadas

Trabajo:

- crear `Boot`, `MainMenu` y `WorldMap`;
- crear `SceneNavigator` y `AppServices`;
- cargar el menú sin inicializar el tablero;
- implementar transición y pantalla de carga común;
- conservar `Gameplay` como escena separada.

Criterio de cierre:

- al abrir el juego solo aparece el menú;
- no hay tablero ejecutándose detrás;
- entrar y salir de las cuatro escenas no genera errores.

### Fase 2 — Campaña basada en datos y 30 niveles

Trabajo:

- sustituir el límite fijo de 10 niveles por un catálogo de campaña;
- crear 30 definiciones iniciales;
- separar reglas del nivel y posición en el mapa;
- validar que cada identificador es único;
- añadir una herramienta de editor para crear, ordenar y revisar niveles;
- mantener compatibilidad con el progreso existente.

Criterio de cierre:

- los 30 niveles aparecen en el catálogo;
- el juego puede abrir cualquier nivel desbloqueado;
- añadir el nivel 31 no exige modificar código.

### Fase 3 — Menú principal independiente

Trabajo:

- portada a pantalla completa;
- botones `Jugar`, `Mapa`, `Ajustes` y `Cómo jugar`;
- estado visual propio, sin transparencia sobre la partida;
- animación de entrada y respuesta táctil;
- acceso rápido al último nivel disponible.

Criterio de cierre:

- la portada parece una pantalla propia;
- `Jugar` lleva al mapa y no inicia directamente una partida;
- funciona en móviles altos, cortos y con zona segura.

### Fase 4 — Mapa funcional de 30 niveles

Trabajo:

- mapa vertical desplazable;
- camino dibujado entre nodos;
- 30 círculos creados desde datos;
- estados `bloqueado`, `disponible`, `actual` y `completado`;
- estrellas y mejor puntuación por nivel;
- perro situado sobre el último nivel alcanzado;
- centrado automático del mapa en el perro;
- tarjeta de confirmación antes de entrar en una partida.

Criterio de cierre:

- ninguna posición depende de una captura fija;
- se puede navegar por los 30 niveles;
- los niveles bloqueados no pueden abrirse;
- el nivel seleccionado se envía correctamente a `Gameplay`.

### Fase 5 — Progreso, movimiento del perro y retorno al mapa

Trabajo:

- guardado versionado en JSON con migración desde `PlayerPrefs`;
- desbloquear el siguiente nivel al ganar;
- guardar estrellas, récord y último nodo;
- volver al mapa tras victoria o derrota;
- animar al perro avanzando al siguiente círculo;
- recuperación segura si el guardado está incompleto o corrupto.

Criterio de cierre:

- cerrar y volver a abrir conserva el progreso;
- una victoria mueve al perro una sola vez;
- repetir un nivel no duplica desbloqueos;
- una actualización futura no borra la campaña.

### Fase 6 — Escalabilidad para nuevos mundos

Trabajo:

- dividir el mapa en mundos o capítulos de 20–30 niveles;
- permitir fondos y decoraciones propios por mundo;
- cargar únicamente el mundo visible;
- descargar o cargar bajo demanda recursos de mundos futuros;
- reutilizar nodos mediante pooling;
- añadir selección de mundo cuando exista más de uno.

Criterio de cierre:

- crear un segundo mundo no modifica el primero;
- cada mundo puede publicarse y probarse por separado;
- el arranque no carga arte de mundos todavía inaccesibles.

### Fase 7 — Inmersión y dirección artística

Trabajo:

- fondo ilustrado limpio para cada mundo;
- camino con profundidad, hitos y pequeñas escenas ambientales;
- animación del perro: espera, celebración y desplazamiento;
- nubes, hojas, pájaros u otros elementos ambientales ligeros;
- música específica de menú y mapa;
- transiciones suaves entre portada, mapa y partida;
- feedback sonoro y háptico en los nodos.

Criterio de cierre:

- menú, mapa y partida se perciben como experiencias diferentes pero coherentes;
- las animaciones no dificultan la navegación;
- mantiene una tasa estable en móvil.

### Fase 8 — Rendimiento, accesibilidad y publicación

Trabajo:

- carga diferida con Addressables;
- Sprite Atlases por pantalla o mundo;
- presupuesto máximo de tamaño por recurso;
- prueba en varias resoluciones móviles;
- tamaño táctil mínimo y contraste legible;
- navegación sin sonido y reducción opcional de movimiento;
- pruebas de WebGL, caché, reconexión y guardado;
- build pública y lista de comprobación manual.

Criterio de cierre:

- primera carga controlada;
- segunda visita usa caché;
- mapa fluido con 30 niveles;
- progreso verificable en móvil.

## 5. Elementos que faltaban por concretar

### Qué ocurre al pulsar `Jugar`

Recomendación: abrir el mapa centrado en el último nivel desbloqueado. No entrar directamente en la partida.

### Qué ocurre al terminar una partida

Recomendación: mostrar resultado, guardar, volver al mapa y, si hay victoria nueva, animar el avance del perro.

### Estrellas y repetición

Cada nivel conserva el mejor resultado de 1 a 3 estrellas. Un nivel completado se puede repetir sin perder estrellas ni progreso.

### Bloqueos y vidas

El mapa debe mostrar el estado de vidas antes de entrar. Si no hay vidas, el nodo no inicia una partida y ofrece la pantalla correspondiente.

### Compatibilidad del guardado

El sistema necesita un número de versión y migraciones. Este punto evita perder partidas cuando cambie la estructura de los niveles.

### Navegación hacia atrás

Todas las pantallas deben tener un destino claro: partida a resultado, resultado a mapa, mapa a menú y ventanas emergentes a su pantalla de origen.

### Mapas futuros

No conviene construir un camino infinito único. Se recomienda trabajar por mundos o capítulos. La campaña inicial puede ser `Parque Central`, niveles 1–30.

### Contenido de los 30 niveles

Crear 30 nodos no equivale a diseñar 30 niveles interesantes. Este bloque prepara y conecta los niveles; el equilibrio, obstáculos y objetivos se cerrarán después en un bloque específico de jugabilidad.

## 6. Herramientas recomendadas

### Necesaria: Unity Addressables

Se instala desde `Window > Package Manager > Unity Registry > Addressables`.

Uso previsto: cargar por separado escenas, fondos, decoraciones y mundos futuros. Conviene incorporarlo en la Fase 6, no al principio, para no añadir complejidad antes de validar el flujo.

### Necesaria: Unity Test Framework

Normalmente ya está disponible desde Package Manager.

Uso previsto: verificar desbloqueos, migración del guardado, selección de nivel y retorno al mapa.

### Recomendable: Figma

Opcional. Sirve para aprobar la composición del menú y el mapa antes de producir arte final. No es necesario para programar el sistema.

### Recomendable: optimizador de PNG/WebP

Puede utilizarse TinyPNG, Squoosh o una herramienta local equivalente para reducir fondos y decoraciones antes de importarlos.

### No necesaria inicialmente: DOTween

Puede ayudar con animaciones, pero añade una dependencia externa. Para esta primera campaña se usarán transiciones propias y Animator de Unity. Solo se reconsiderará si el volumen de animaciones lo justifica.

### No recomendada para la build publicada: Git LFS

Git LFS puede servir para originales pesados, pero no debe aplicarse a los archivos que GitHub Pages necesita servir directamente. Primero mantendremos los recursos optimizados y separaremos originales de la build.

## 7. Reglas para evitar problemas futuros

- Ningún texto ni botón de nivel estará dibujado dentro del fondo.
- Los niveles tendrán identificadores estables.
- El mapa no conocerá reglas internas del tablero.
- `Gameplay` no decidirá qué pantalla se abre después.
- El progreso tendrá versión y migraciones.
- Los recursos se cargarán por pantalla o mundo.
- Cada fase terminará con una prueba manual y un punto recuperable en Git.
- No se añadirán nuevas mecánicas de tablero durante este bloque.

## 8. Orden de ejecución acordado

1. Fase 0 y Fase 1: estructura segura y escenas.
2. Fase 2: catálogo de 30 niveles.
3. Fase 3: menú independiente.
4. Fase 4: mapa funcional.
5. Prueba manual completa.
6. Fase 5: progreso y movimiento del perro.
7. Fases 6–8: escalabilidad, arte, rendimiento y publicación.

## 9. Definición de terminado de este bloque

Este bloque estará cerrado cuando:

- el juego arranque en un menú independiente;
- `Jugar` abra un mapa independiente;
- existan 30 niveles gestionados por datos;
- el perro indique el progreso;
- mapa y partida se comuniquen sin dependencias directas;
- el progreso sobreviva al cierre y a futuras migraciones;
- añadir nuevos mundos no obligue a rehacer los anteriores;
- la versión WebGL funcione correctamente en móvil.
