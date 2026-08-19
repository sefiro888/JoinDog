# JoinDog — documento de continuidad

Actualizado: 19 de agosto de 2026

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
- Móvil publicado: pendiente de la publicación de la expansión a 100 niveles.
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
- La compilación WebGL de JoinDog fue comprobada como correcta y publicada en `c448e95`.
- Las 30 mejoras de `JOIN_DOG_ROADMAP_MEJORAS.md` están aplicadas localmente en cinco fases.
- La nueva WebGL local fue validada con 21 pruebas de edición, 7 pruebas jugables y una auditoría de consola móvil sin errores.
- La build local de 100 niveles usa la versión de caché `dc00a466fed10f29b732d31e`, se compiló con cero errores y está pendiente de publicación.

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

- Probar en uno o dos móviles físicos las transiciones 70→71, 80→81 y 90→91,
  además del final 100.
- Ajustar la curva experta de los niveles 81–100 según esas primeras partidas.
- Después, limpiar gradualmente nombres históricos mediante migración controlada.

## Cómo retomar en una conversación nueva

Pegar este documento o decir:

> Continuamos JoinDog desde `JOIN_DOG_HANDOFF.md`. Usa exclusivamente `C:\Users\sefir\Desktop\JoinDogClean`, no uses DOGCRUSH, conserva la mecánica y los 100 niveles, y no borres referencias internas sin migrarlas y probar una build.
