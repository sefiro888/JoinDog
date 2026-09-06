# Magia — entrega local, 6 septiembre 2026

No publicar ni hacer push a GitHub. El usuario revisa el aspecto y la fluidez.

## Cambios

- Inicio: fondo ilustrado, logo independiente, botones con relieve, progreso y estrellas grandes.
- Ficha del nivel: tarjeta perla, título Magia, estrellas ilustradas, objetivo y regla separados, tiempo/dificultad, récord y premio. Se conserva la apertura de cofres.
- Texto variable: Baloo 2 extra-gruesa, degradado perla/cian/lila, contorno y sombra para celebraciones y puntuaciones. Los carteles grandes se muestran brevemente sobre el tablero sin bloquear pulsaciones; instrucciones pequeñas mantienen letra legible.
- Mascota: la seleccionada aparece en la ayuda; se retira la mascota diminuta inferior. El retrato reacciona al ayudar.
- Foto propia: Mascotas → Elegir foto local. Recorte central cuadrado, 512 px, JPEG; almacenamiento local en ese navegador. No sale a servidores. La foto NO se convierte automáticamente en ilustración ni se le elimina el fondo. Borrar los datos del navegador elimina la foto.
- Hielo: borde más fino y menos intenso, grietas visibles. Reglas y golpes necesarios sin cambios.
- Colección: 9 entradas; cuerda desde nivel 21, frisbee desde 31 y pingüino desde 41 pasan a ser fichas jugables progresivas.

## Comprobaciones que realiza el usuario

1. Abrir inicio: logo, mascota, botones y ambos contadores completos.
2. Abrir un nivel normal, uno con hielo y uno con cofre: textos legibles y Jugar/Volver funcionando.
3. Cambiar mascota, entrar en partida, ver el retrato correcto. Elegir una foto centrada y repetir; cerrar/abrir el navegador para comprobar su guardado.
4. Combinar 4, 5 y provocar cascadas: letras nuevas legibles; valorar tamaño, duración y si molestan. Probar Movimiento reducido.
5. Abrir colección: nueve tarjetas completas, nuevos recuerdos y mensajes de desbloqueo.
6. Si falla algo: nivel, acción y captura o vídeo breve. No usar la URL antigua de GitHub para esta entrega.

## Recursos locales y procedencia

Herramienta integrada de imágenes (sin CLI). Archivos finales:

- `Assets/_JoinDog/Resources/Magic/logo.png`
- `Assets/_JoinDog/Resources/Magic/park.png`
- `Assets/_JoinDog/Resources/Magic/frisbee.png`
- `Assets/_JoinDog/Resources/Magic/penguin.png`

Fuente: https://github.com/google/fonts/tree/main/ofl/baloo2 (SIL OFL, licencia incluida en `Resources/Fonts/OFL.txt`). Instanciada en peso 800. Atlas generado por `MagicLocalBuild.Prepare`.

### Prompts finales

Logo: One production game logo asset, exact text JOIN on first line and DOG on second line. Chunky inflated rounded bubble lettering, pearlescent white/cyan highlights shading into pink lavender bottom, deep purple outline and bevel extrusion, beautiful magical polished 3D pet game brand. A paw pad motif inside O of DOG. Tight centered composition large filling canvas with 5% margin. TRANSPARENT background with alpha, no scene, no panel, no captions, no other objects. Readable and charming, not metallic. Square.

Fondo: Production background illustration for portrait mobile pet puzzle game home menu, no UI and NO TEXT and NO ANIMALS. Magical sunny dog park with lavender flowering branches framing upper corners, soft blue sky open in upper third for logo, park benches and dog paw carved sign at edges, soft grassy path and empty clearing in center for separate pet character, lilac flowers and small bushes around lower edges, lower center uncluttered for separate buttons. Polished colorful detailed 3D cartoon illustration, gentle sunshine, premium whimsical family game, not photographic. Portrait 2:3.

Frisbee: Single collectible blue and cyan rubber dog frisbee, thick rounded toy disc, little embossed paw center, three-quarter view, polished cheerful 3D cartoon game icon with subtle pearly lavender highlights, clear silhouette readable at 64px. Centered filling 80% of square image. TRANSPARENT alpha background. No text, no frame, no ground or surrounding objects.

Pingüino: Single cute small penguin plush dog toy, black navy white soft body, orange beak and feet, tiny lavender scarf, rounded chunky proportions, seated front three-quarter view. Polished cheerful 3D cartoon collectible game icon, large readable silhouette at 64px. Centered filling 80% of square image. TRANSPARENT alpha background. No text, no frame, no ground or surrounding objects.
