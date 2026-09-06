# Auditoría JoinDog — 6 septiembre 2026

Auditoría local del código, recursos, pruebas y build WebGL. No se ha publicado
nada. Estados: **verificado**, **parcial**, **pendiente** o **por probar**.

## Verificado en código y versión local

- Campaña continua hasta el nivel 100 y diez zonas de diez niveles.
- Fondos, ambientes, entradas y paleta diferenciada para las zonas del mapa.
- Portada Magia con logo, fondo, botones, mascota y contadores de progreso.
- Tarjeta de nivel Magia con nivel, título, objetivo, estrellas, tiempo, premio,
  récord, jugar y volver.
- Estrellas del mapa como medallones independientes.
- Caminos laterales en las entradas y rótulos ovalados de zona.
- Mascota seleccionada en la tarjeta Ayuda del Perrito.
- Retirada del cachorro pequeño bajo el tablero.
- Foto local de mascota con almacenamiento local del navegador.
- Hielo con borde menos dominante y grietas más visibles.
- Textos de combos, puntos y celebraciones con tipografía/estilo Magia.
- Patito como sexta ficha jugable desde el nivel 11.
- Álbum con nueve entradas: cinco figuras iniciales, patito, cuerda, frisbee y pingüino.
- Patito, cuerda, frisbee y pingüino como fichas jugables progresivas desde los niveles 11, 21, 31 y 41.
- Aviso de recuerdo en los niveles 31 y 41.
- Sonidos runtime básicos, control de volumen y opciones de accesibilidad.
- Recompensas de zona, cofres, objetivos secundarios y rachas presentes en el
  sistema de progreso.
- Build WebGL local completado con 0 errores.
- Favoritos persistentes para hasta doce niveles, con marca en el mapa y control
  en la tarjeta del nivel.
- El álbum ofrece acceso directo para jugar el nivel actual.
- Completar la colección concede una recompensa única de 250 galletas y 2
  huesos mágicos, persistida en el guardado.
- Las figuras muestran rareza COMÚN, ESPECIAL o ÉPICA sin modificar sus poderes.
- El panel Paseo Diario incluye un objetivo extra para conseguir tres estrellas
  nuevas durante la sesión diaria.
- Las tarjetas del álbum entran con una animación escalonada y respetan el modo
  de movimiento reducido.
- Las fusiones de dos especiales muestran el nombre del combo y una segunda
  línea que explica visualmente su efecto.
- La primera victoria tiene una celebración propia y los siguientes intentos
  distinguen la mejora del récord.
- El compañero reacciona con mensajes distintos para cascadas, especiales,
  fusiones y ayuda lista, usando la mascota seleccionada.
- Hay niveles de recolección doble que exigen reunir dos tipos de fichas y
  actualizan el objetivo con el progreso combinado.
- La bolsa de fichas ahora es temática por capítulo: las nuevas piezas no se
  eligen solo por orden numérico, sino por una combinación definida para cada
  bloque de mundo y se respeta también durante las caídas y casillas de salida.
- Al entrar por primera vez en una zona aparece una presentación breve de
  descubrimiento con el nombre y color del mundo; queda guardada para no
  interrumpir las visitas posteriores.
- Verificación runtime local del nivel 39: objetivo de entrega visible
  “PELOTAS · SALIDA 0/4”, fichas temáticas nuevas en el tablero, mascota
  seleccionada en la ayuda y consola sin errores.
- Verificación responsive local en viewport móvil aproximado: HUD, tablero,
  ayuda y potenciadores permanecen dentro del ancho visible; queda repetirlo
  en dispositivos físicos con distintas densidades.

## Parcial o necesita comprobación visual

- Las tarjetas cambian de color, arte, subtítulo, acento, regla, emblema y
  motivo decorativo por zona; la identidad de los diez capítulos queda resuelta
  sin añadir una descarga pesada de marcos bitmap.
- Las entradas de mapa están implementadas, pero hay que revisar visualmente los
  100 niveles para confirmar que ningún nodo o camino tapa un arco.
- La ayuda del compañero pulsa y cambia de estado, con reacciones específicas
  por mascota y por tipo de evento.
- El hielo está mejorado, pero hay que revisar todos los obstáculos y poderes
  en móvil, no solo una pantalla.
- La colección tiene etiquetas de recuerdo, animación de entrada, rareza y
  recompensas persistentes por completar grupos parciales; falta validar el
  reclamo final en móvil.
- Hay sonidos runtime y música ambiental procedural diferenciada por mundo, con
  volumen persistente separado; queda revisar la mezcla en móvil.
- El panel Ajustes incluye ahora un control independiente de música, con estado
  persistente y acceso táctil propio.
- La foto local está implementada, pero falta probar varios formatos y volver a
  abrir el navegador en un dispositivo real.
- La tarjeta de objetivo muestra ahora tres estrellas ilustradas grandes, cambia
  su estado al progresar y comunica el siguiente hito (30%, 60% o 100%); falta
  únicamente validarla en varios tamaños reales de móvil.

## Pendiente confirmado

### Variedad de tablero

- Selección temática de cinco a nueve fichas por bloque de mundo: implementada;
  queda validar el equilibrio real jugando varias partidas en móvil.
- Misiones de llevar un juguete hasta una salida: implementadas en niveles 19,
  39, 59, 79 y 99, con casilla de salida visible y contador por figura entregada.
- Rescate de cachorros alrededor de casillas objetivo: implementado en niveles
  14, 34, 54, 74 y 94 con jaulas visuales, contador de rescates y objetivo propio.
- Objetivos de dos tipos de figuras en una partida: implementado en niveles
  intermedios de cada capítulo.
- Niveles por movimientos además de niveles por tiempo: implementado en los niveles
  18, 38, 58, 78 y 98, con HUD específico, límite de movimientos y estrellas según
  movimientos conservados.
- Finales de mundo con misión propia: implementado; los niveles 10, 20, 30, 40,
  50, 60, 70, 80, 90 y 100 limpian obstáculos con tipo, cantidad y resistencia
  propios.
- Explicación visual de combinaciones entre dos poderes: implementada con título
  y descripción breve durante la resolución del combo.

### Progreso y colección

- Presentación animada al descubrir una zona: implementada; queda comprobarla
  visualmente en varios tamaños de móvil.
- Recuerdos ganados al completar cada mundo: implementados en el último nivel de
  cada capítulo, con reclamo persistente, 120 galletas y un Hueso Mágico.
- Aspectos cosméticos desbloqueables con estrellas: implementada el Aura Estelar
  al alcanzar 30 estrellas; se reclama en el álbum y aparece alrededor de la
  mascota del mapa sin alterar el poder de ninguna ficha.
- Recompensas por completar colecciones: implementadas para grupos de 3 y 6
  figuras, además del premio final de las nueve; falta validar el clic final en
  un dispositivo móvil.
- Porcentaje y avance de colección por mundo: implementado en cada banner del
  mapa con figuras descubiertas/figuras del capítulo.
- Objetivo opcional de sesión: implementado en Paseo Diario con progreso 0/3 y
  reinicio diario.
- Favoritos de niveles: implementado; falta probar la interacción manual en móvil.
- Botón de volver/jugar el nivel actual desde el álbum: implementado; falta probarlo
  en móvil.

### Pulido

- Tarjetas de nivel temáticas por zona: implementadas con fondo del capítulo,
  colores, banda de acento, emblema de entrada y sello de ritmo.
- Iconografía específica de cada mundo: implementada en la cabecera dinámica con
  el emblema ilustrado de la entrada de cada zona.
- Revisión completa de espacios vacíos en finales: implementada en el layout;
  falta confirmar los diez finales visualmente en móvil.
- Efectos y sonido diferenciados para cada tipo de combinación: implementados
  por nivel de combo, con partículas y capas de audio runtime.
- Reacciones únicas de mascotas para cascadas, rescates y ayuda lista:
  implementadas para Yorkshire, Pitbull y mascota local, con variantes de
  cascada, especial, fusión, rescate y ayuda.
- Prueba completa en varios tamaños de móvil.
- Prueba de rendimiento y carga WebGL en móvil modesto.
- Limpieza de avisos de metadatos antiguos de niveles.

## Estado de cierre

La parte de implementación prevista está completada y la build local actual
termina con cero errores. Ya no quedan fases funcionales de código de este
listado por aplicar. El cierre depende de comprobaciones físicas:

1. Validar en móvil las estrellas de objetivo, celebraciones y legibilidad.
2. Recorrer finales y transiciones de las diez zonas para confirmar que puertas,
   caminos y nodos no se pisan en el dispositivo real.
3. Probar desbloqueos de figuras, equilibrio de bolsas temáticas y objetivos
   especiales en los niveles 11, 21, 31, 41 y finales.
4. Probar álbum, recompensas, recuerdos, favoritos, aura, música, foto local y
   guardado tras recargar.
5. Medir rendimiento, carga y mezcla de audio en un móvil modesto.

Estas comprobaciones requieren interacción y hardware del usuario; no se pueden
certificar honestamente solo con la compilación o el navegador de desarrollo.
