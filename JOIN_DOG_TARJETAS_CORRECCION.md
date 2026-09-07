# Corrección de tarjetas — 7 septiembre 2026

## Cambios locales

- Proporción calculada desde el sprite real, dentro de un contenedor con márgenes.
- Diez perfiles de posiciones medidos sobre las diez ilustraciones existentes.
- Cabecera, título, estrellas, objetivo y filas ajustados por territorio.
- Récord y favoritos en columnas distintas; cofres/recuerdos en otra zona.
- Botones de jugar y volver integrados sobre el dibujo, sin segunda superficie genérica.
- Colores de texto oscuros sobre superficies claras, blanco en los paneles oscuros.
- Tienda con distribución independiente, saldo sobre su placa y productos dentro de sus marcos.
- Aviso de descubrimiento con posiciones propias y control Continuar; duración máxima ampliada a ocho segundos.

## Validación

`Tools/Test-TerritoryLayout.ps1`: 400 combinaciones de nivel/tamaño pasan las comprobaciones de proporción, cabida y separación de filas. Tamaños: 320×568, 360×800, 390×844 y 1280×720.

Esta prueba reutiliza el perfil común de los diez niveles de cada territorio. No simula partidas ni certifica el tamaño final de cada texto, el funcionamiento de todas las recompensas o la comodidad táctil. Esas comprobaciones no deben confundirse con las geométricas.

Compilación en `Logs/territory-layout-build.log`. No se ha publicado en GitHub.
