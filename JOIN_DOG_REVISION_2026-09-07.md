# JoinDog — estado actual, corrección pendiente y mejoras futuras

Fecha: 7 de septiembre de 2026. Revisión local de código, dimensiones de recursos, compilación y documentación. Esta revisión distingue implementación de validación visual; no equivale a haber jugado los 100 niveles completos en un móvil real.

## Addendum de auditoría — estado después de la última implementación

El informe original quedó parcialmente desactualizado después de añadir los perfiles territoriales y el panel de retos. El estado correcto es:

| Elemento | Estado real | Evidencia |
|---|---|---|
| Proporción nativa de las diez tarjetas | **Implementado y comprobado geométricamente** | `KeepPortraitArtwork` recibe el sprite real; `Tools/Test-TerritoryLayout.ps1` pasa 400 casos. |
| Distribución específica por territorio | **Implementada y comprobada geométricamente** | Diez perfiles en `LayoutTerritoryCard`; filas separadas para récord/favoritos/recompensas. |
| Revisión visual de las 100 tarjetas | **Pendiente** | La prueba automática no renderiza texto, contraste ni interacción táctil. |
| Tienda | **Implementada; validación visual completa pendiente** | Arte propio y `LayoutIllustratedStore`; faltan pruebas sistemáticas de saldo, compra y cierre. |
| Descubrimiento de zonas | **Implementado; validación visual completa pendiente** | Arte propio, botón Continuar y duración ampliada; faltan nombres largos y pruebas táctiles. |
| Panel de retos diarios | **Implementado localmente; no incluido en la versión publicada de este informe** | `daily_panel.png`, integración en `ShowDailyMissions` y recolocación de los dos premios. |
| Fuente y estilo de retos | **Parcialmente implementado** | Fuente MagicRounded, autoajuste y contorno; la redacción de los mensajes aún es básica. |
| Compilación WebGL más reciente | **Correcta** | `Logs/daily-panel-build.log` termina con código 0. |
| Pruebas de lógica | **47/47 en el resultado existente** | `TestResults/phase5-final.xml`, ejecutado el 6 de septiembre; no cubre la apariencia. |
| Publicación | **No publicada desde el último panel diario** | Hay cambios locales posteriores a la última subida; falta commit/push de esta iteración. |
| Arranque local con caché limpia | **Incidencia encontrada** | El 10 de septiembre el navegador local registró un error al actualizar `service-worker.js`; la pantalla quedó en 100% de carga. Debe corregirse antes de declarar validada la interfaz. |

### Confirmado como implementado

- Patito, cuerda, frisbee y pingüino están conectados como fichas jugables progresivas; no son únicamente coleccionables.
- Las transiciones de niveles 19, 39, 59, 79 y 99 no usan la misión de salida. Existe una prueba específica que lo verifica.
- Existen rondas por movimientos, rescate de cachorros, recolección doble, finales y ayudas del compañero.
- Favoritos, recuerdos de mundo, colección y recompensas están conectados en el código.

### No debe marcarse todavía como 100% terminado

- Ajuste visual final de cada combinación de territorio, nivel largo, cofre, recuerdo y favorito.
- Pruebas de la pantalla de retos diarios con premio bloqueado, premio listo y premio conseguido.
- Pruebas móviles reales de 320×568, 360×800 y 390×844 con toque y caché limpia.
- Verificación de persistencia después de recargar, actualizar y cambiar de mascota.
- Balance real de dificultad y frecuencia de las fichas nuevas mediante partidas.

El orden correcto ahora es: validar visualmente la nueva pantalla de retos, ejecutar la matriz móvil de tarjetas/tienda/descubrimiento, corregir regresiones y solo después publicar la iteración.

## 1. Veredicto sobre las tarjetas

**La corrección es parcial; el problema no está resuelto por completo.**

`WorldMapScreenController.cs` incluye `KeepPortraitArtwork`, que utiliza `AspectRatioFitter` con proporción fija 2:3. Esto evita que el contenedor adopte cualquier proporción de pantalla, pero no garantiza que respete la ilustración ni que sus textos coincidan con los huecos dibujados.

La afirmación anterior de que todas las ilustraciones comparten una cuadrícula compacta 2:3 no coincide con los archivos actuales:

| Territorio | Imagen real | Proporción ancho/alto |
|---|---|---|
| Pradera Feliz | 972 × 1619 | 0,600 |
| Bosque Aventura | 1003 × 1568 | 0,640 |
| Jardines Celestes | 1003 × 1568 | 0,640 |
| Festival Canino, Costa Dorada, Cumbres Nevadas, Valle Aurora, Cumbre Luminosa, Cañón de Rubíes y Santuario Dorado | 1122 × 1402 | 0,800 |

La proporción forzada es 0,667. Además, la imagen interior tiene `preserveAspect = false`: las ilustraciones todavía se deforman para llenar ese rectángulo. Hay un recurso adicional `jardines-celestes.png` que no coincide con el identificador cargado `jardines_celestes`; conviene aclarar cuál es el definitivo.

### Problemas confirmados en el código

- Todas las zonas usan las mismas coordenadas de texto y botones, aunque sus imágenes tienen diferentes proporciones. No existe aquí una configuración de posiciones por territorio.
- El récord ocupa el intervalo vertical 0,145–0,190; favoritos ocupa 0,145–0,188 dentro de esa misma fila. Sus rectángulos se solapan.
- En finales y cofres aparece otro botón en la mitad izquierda de esa fila, también encima del rectángulo del récord.
- El contenedor usa `FitInParent` directamente sobre la capa de pantalla: los márgenes declarados previamente no constituyen una garantía de espacio seguro.
- Tiempo utiliza un corazón y el premio en galletas una estrella: los iconos no explican correctamente esos datos.
- Los botones genéricos se superponen al arte completo. Preservar proporciones no basta para hacer coincidir botones y superficies ilustradas.

La posición exacta de cada palabra sobre cada imagen necesita revisión visual posterior. El código basta para rechazar la afirmación de que ya están garantizados los 100 niveles sin solapamientos.

## 2. Cómo arreglarlo bien por territorio

Recomiendo reutilizar las ilustraciones existentes y corregir su integración antes de generar más imágenes.

1. Crear un contenedor que respete márgenes y área segura. Dentro, ajustar la tarjeta a la proporción real del recurso, no a una constante universal.
2. Crear diez perfiles de distribución: uno por territorio. Cada perfil define los rectángulos normalizados de cabecera, nivel, título, estrellas, objetivo, tiempo, premio y acciones, además de los colores de contraste.
3. Separar completamente récord y acciones secundarias. Cofres y recuerdos necesitan un espacio reservado que no aparezca encima de otra información.
4. Mantener textos y cifras dinámicos; el dibujo aporta marcos y decoración. Donde el arte ya contiene un botón, colocar una zona interactiva alineada y su etiqueta, evitando un segundo botón encima.
5. Usar tipografía Magia para títulos y celebraciones; texto sencillo y suficientemente grande para instrucciones. Ajustar frases largas sin reducirlas hasta hacerlas ilegibles.
6. Sustituir iconos ambiguos por reloj, movimientos y galleta. Verificar que cada instrucción describe la regla real del nivel.
7. Aplicar un perfil independiente a tienda y descubrimiento. No reutilizar sus coordenadas como si fueran una tarjeta de nivel.

Alternativa: regenerar los diez diseños con idéntica geometría. Es más costosa y tampoco garantiza precisión; seguiría haciendo falta calibrar. Los diez perfiles sobre el arte actual son la opción más económica inicialmente. Regeneraría únicamente los recursos que no dejen espacio suficiente para contenido legible.

### Condiciones para darlo por terminado

- Revisar las 100 tarjetas, incluyendo objetivos largos, finales, cofres, favoritos y recompensas reclamadas/no reclamadas.
- Comprobar al menos 320×568, 360×800, 390×844 y una pantalla horizontal; contemplar la altura que resta la barra del navegador.
- Ningún texto, estrella o botón fuera de su superficie ni encima de otro control.
- Botones utilizables al tacto y objetivo comprensible sin interpretar iconos ambiguos.
- Validar además la tienda con saldo suficiente/insuficiente, compra y cierre; y el aviso de zona con nombres largos, animación y finalización.
- Distinguir comprobación automática de rectángulos de revisión visual: hacen falta ambas.

## 3. Estado actual: qué sabemos y qué no

| Área | Estado y límite de la evidencia |
|---|---|
| Compilación local | `Logs/final-aspect-build.log` termina correctamente, código 0. Compilar no certifica calidad visual. |
| Pruebas de lógica | Existe un resultado anterior de 47/47 aprobadas, del 6 de septiembre, en `TestResults/phase5-final.xml`. No se han vuelto a ejecutar en esta revisión ni validan el aspecto de las 100 tarjetas. |
| Arte territorial | Existen los diez recursos y el controlador los carga por territorio. La integración geométrica sigue pendiente. |
| Tienda | Hay arte propio y código de integración. Comparte el ajuste fijo 2:3; no se puede dar por aprobada visualmente con este mecanismo. |
| Descubrimiento de zonas | Existe implementación y recurso propio. Pendiente de validación visual completa, legibilidad y duración. |
| Favoritos, cofres y recuerdos | Hay controles funcionales conectados en la tarjeta; su colocación interfiere con el récord. |
| Nuevas figuras | El documento de progresión registra patito 11, cuerda 21, frisbee 31 y pingüino 41 como jugables, no solo coleccionables. Es estado documentado, no una nueva prueba de cada frontera en esta revisión. |
| Variedad de misiones | El catálogo contiene rondas por movimientos y jaulas. La documentación registra recolección doble y sustitución de misiones de salida. Permanecen referencias al enum `DeliverToy`; su mera existencia no demuestra que siga asignándose a niveles activos. |
| Colección y compañero | Documentados álbum de nueve figuras, recompensas, aura y mascota seleccionada. Falta una prueba integral reciente de desbloqueos, mascota subida y persistencia. |
| HUD/tablero móvil | No debe considerarse cerrado solo porque las tarjetas compilen. Hay que reproducir la superposición de objetivo/tablero señalada por el usuario y comprobar todas las dimensiones de tablero. |
| Actualización y guardado | La página local registra un service worker. Debe verificarse que se está viendo la compilación nueva y que actualizar no pierde progreso. |

**No sería correcto decir «todo terminado al 100%».** Hay funcionalidades ya implementadas que necesitan validación, y hay defectos concretos de presentación aún presentes. Tampoco sería correcto volver a listar todas esas funcionalidades como si no existieran.

## 4. Mejoras futuras, ordenadas por utilidad

### Prioridad 0 — cerrar lo comprometido

- Terminar los diez perfiles de tarjeta, tienda y celebración de zona.
- Corregir el HUD adaptable: reservar espacio real para objetivo, tablero, compañero y potenciadores; evitar depender de desplazamientos fijos.
- Centralizar estilos, márgenes, tamaños mínimos e iconos para no corregir cada pantalla de manera distinta.
- Preparar una matriz reproducible de los 100 niveles y estados especiales. Guardar capturas de referencia para detectar regresiones.
- Revisar carga/caché y mostrar una identificación de versión comprobable.

Resultado esperado: una versión visualmente coherente y verificable antes de añadir más contenido.

### Prioridad 1 — claridad y equilibrio

- Microtutorial al estrenar cada obstáculo: una demostración breve, una instrucción y oportunidad de probar. No añadir pantallas largas.
- Mostrar objetivos con las figuras reales y contadores separados cuando sean dobles.
- Diferenciar claramente hielo, enredaderas, jaulas y poderes sin ocultar la ficha interior.
- Revisar dificultad mediante partidas: movimientos útiles, tiempo disponible, probabilidad de combinaciones y dependencia de potenciadores. Ajustar especialmente cambios de bolsa de figuras y finales.
- Explicar al terminar qué faltó y qué se consiguió; evitar mensajes genéricos cuando el objetivo no era solo puntuación.

No propongo volver a crear las misiones ya existentes: propongo hacerlas comprensibles y equilibradas.

### Prioridad 2 — satisfacción audiovisual

- Jerarquía de celebraciones: combinación de 3 discreta; 4 y 5 más vistosas; cascadas con intensidad progresiva y límites de partículas.
- Frases Magia legibles con contorno y duración suficiente, sin tapar fichas mientras se decide la siguiente jugada.
- Sonidos de especiales diferenciados, con volumen consistente y controles independientes.
- Reacciones del compañero vinculadas a una ayuda concreta y explicación clara de cómo se carga.
- Transición de mundo que anticipe el nuevo paisaje y juguete, con posibilidad de continuar cuando el jugador lo haya leído.

### Prioridad 3 — colección y motivos para regresar

- Pulir el álbum existente: siguiente desbloqueo visible, premio concreto y progreso por mundo.
- Ampliar primero con cosméticos para la mascota y recuerdos de territorio; no añadir tipos al tablero sin estudiar el equilibrio.
- Mejorar los retos opcionales existentes con objetivos variados y sin penalizar ausencias.
- Hacer que repetir favoritos permita perseguir una meta comprensible: estrellas pendientes o récord personal.

### Prioridad 4 — fiabilidad, rendimiento y accesibilidad

- Probar guardado tras cerrar navegador, recargar, actualizar y cambiar mascota. Considerar exportación/importación de progreso antes de plantear cuentas o sincronización.
- Medir carga inicial, memoria y fluidez en un móvil modesto; optimizar texturas y reutilizar efectos según resultados.
- Opción de movimiento reducido, contraste suficiente y diferencias de forma además de color.
- Revisar áreas táctiles, orientación, pausas y recuperación al volver de otra aplicación.
- Mantener un único listado de estado con «implementado», «probado» y «pendiente» separados. Evita repetir trabajo y gastar tokens en auditorías contradictorias.

## 5. Recomendación práctica

El siguiente trabajo debería ser **integración y validación**, no una nueva ronda de funciones. Primero calibrar Pradera, Cumbres y Cañón, que representan geometrías y contrastes diferentes; después completar los otros siete perfiles y verificar los 100 niveles. A continuación cerrar tienda, descubrimiento y HUD. Solo entonces entrar en equilibrio y futuras ampliaciones.

Esta revisión no modifica el código del juego ni publica cambios. El informe identifica lo comprobado y deja explícito lo que todavía requiere prueba.
