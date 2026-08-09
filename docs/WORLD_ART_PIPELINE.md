# Sistema visual modular de mundos

El mapa de JoinDog separa deliberadamente cinco capas:

1. Fondo ilustrado de cada mundo.
2. Atmósfera dinámica (niebla, luces, partículas).
3. Camino generado por código.
4. Nodos, bloqueos, recompensas y marcador del perro.
5. Cabecera y paneles interactivos.

Esta separación permite sustituir el arte de un mundo sin alterar niveles,
progreso, navegación ni adaptación a distintas pantallas. Si falta una
ilustración, el mapa utiliza automáticamente el fondo procedural anterior como
respaldo.

## Convención de recursos

Los fondos se guardan bajo:

`Assets/_JoinDog/Resources/Worlds/<WorldName>/`

La asociación entre el identificador de campaña y su fondo se registra en
`WorldMapArtLibrary.cs`. Cada recurso es exclusivo de una zona; no debe incluir
camino, nodos, números, botones, personajes, logotipos ni textos.

## Primer mundo ilustrado

- Zona: `bosque_aventura` (niveles 11–20)
- Recurso: `Worlds/Forest/forest_world_background_v1`
- Entrada: `Worlds/Forest/forest_entrance_arch_v1`
- Uso: capa ambiental bajo el camino dinámico.

Prompt de producción usado con la herramienta integrada de generación de
imágenes:

> Use case: stylized-concept. Asset type: modular vertical game-world background
> for a mobile level-selection map. Create a lush enchanted woodland environment
> called Bosque Aventura for a vertical scrolling dog-themed puzzle-game campaign
> map. Use a polished premium 2D mobile-game style with rounded forms, soft 3D
> volume, vivid colors and controlled highlights. Frame both sides with emerald
> forest, mossy rocks, ferns, flowers, mushrooms, warm firefly glows, soft mist and
> sunbeams. Keep a broadly open, low-detail central corridor for a separate dynamic
> path and circular level nodes. Portrait composition with top-to-bottom
> continuity. Environment only: no characters, UI, buttons, panels, text, numbers,
> logos, icons, nodes, lines, roads, game board, watermark, checkerboard or borders.

La entrada se generó como un arco forestal aislado sobre un fondo magenta
uniforme y se convirtió localmente a PNG con canal alfa. Incluye un medallón de
huella, madera, musgo, faroles y vegetación, pero ningún texto o nivel incrustado.
El nombre de la zona y el primer nodo continúan siendo elementos dinámicos.

## Cómo añadir el siguiente mundo

1. Generar un fondo sin elementos interactivos incrustados.
2. Copiarlo a la carpeta del mundo dentro de `Resources/Worlds`.
3. Registrar una única ruta en `WorldMapArtLibrary`.
4. Mantener la atmósfera ligera y animada en `WorldMapScreenController`.
5. Probar el inicio, el centro y la transición de la zona en formato móvil.
6. Comprobar que, al retirar temporalmente el archivo, sigue apareciendo el
   respaldo procedural.

No se debe volver a crear un mapa completo como una única captura fija. Esta
regla evita el problema original de escalado y conserva la campaña ampliable.
