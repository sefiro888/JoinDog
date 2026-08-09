# JOIN DOG — Campaña ampliada a 50 niveles

## Estado del bloque

La campaña funciona mediante datos y queda preparada para crecer sin duplicar escenas ni convertir el mapa en una imagen fija. El recorrido es:

`Menú -> Mapa vertical -> Vista previa -> Partida -> Resultado -> Mapa`

El progreso existente se conserva. Los jugadores que ya habían avanzado continúan en su nivel y ahora pueden desbloquear hasta el nivel 50.

## Cinco mundos jugables

1. **Pradera Feliz (1–10):** introducción, tablero completo y aprendizaje de fichas especiales.
2. **Bosque Aventura (11–20):** tableros redondeados y enredaderas.
3. **Festival Canino (21–30):** tableros variables y faroles resistentes.
4. **Costa Dorada (31–40):** ambiente marino, tableros redondeados y arena que se limpia combinando encima o junto a ella.
5. **Cumbres Nevadas (41–50):** ambiente nocturno nevado, tableros de montaña y hielo de tres golpes sensible a especiales.

Cada mundo dispone de colores, ambiente animado, decoración, camino, HUD y tablero propios. Los finales 10, 20, 30, 40 y 50 tienen configuración especial y una celebración de resultado distinta.

## Variedad de objetivos

- alcanzar una puntuación;
- recoger una cantidad de una ficha concreta;
- fabricar fichas especiales;
- eliminar obstáculos;
- provocar cascadas automáticas.

Los objetivos, tiempo, dimensiones, forma del tablero, resistencia, premios y potenciadores se calculan por nivel. La dificultad y las metas crecen durante toda la campaña.

## Reglas técnicas para futuras ampliaciones

- `CampaignCatalog` es la fuente única de niveles y zonas;
- `CampaignCatalog.MaxLevel` sustituye límites escritos manualmente;
- `PlayerProgressService` limita y conserva el progreso usando ese catálogo;
- el mapa genera nodos, conexiones y decoraciones a partir de los datos;
- cada obstáculo tiene tipo, cantidad, resistencia, aspecto y regla propia;
- añadir otro mundo exige agregar sus datos, tema y regla, sin crear otra escena de partida;
- los recursos grandes de mundos futuros deberán cargarse bajo demanda para proteger el arranque WebGL.

## Validación del bloque

- compilar scripts sin errores;
- confirmar 50 niveles y 5 zonas en el recurso de campaña;
- ejecutar pruebas EditMode de catálogo, progreso y reglas base;
- generar WebGL local;
- comprobar manualmente en móvil los niveles 31, 32, 40, 41 y 50;
- afinar metas y tiempos con partidas reales antes de publicar.

## Próximo bloque recomendado

Tras validar este lote en móvil: tutorial interactivo de obstáculos nuevos, recompensas de final de mundo, misiones secundarias y carga independiente de futuros mundos 6+.
