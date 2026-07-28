# JoinDog

JoinDog es un juego móvil vertical de combinación de fichas inspirado en el mundo canino. Esta es la nueva base limpia del proyecto: contiene únicamente el proyecto Unity necesario para continuar el desarrollo, sin historiales, capturas ni builds antiguos del repositorio DOGCRUSH.

## Estado actual

- Intercambio de fichas adyacentes en horizontal y vertical.
- Previsualización del intercambio mientras se arrastra.
- Intercambio inválido que vuelve suavemente a su posición.
- Detección de combinaciones, eliminación, caída, reposición y cascadas.
- Niveles, objetivos, tiempo, puntuación, vidas y potenciadores integrados en la partida.
- Orientación vertical y controles táctiles pensados para móvil.

## Abrir el proyecto

1. Instala Unity `6000.5.5f1`.
2. Abre Unity Hub y selecciona esta carpeta como proyecto existente.
3. Abre la escena principal desde `Assets/Scenes` y pulsa Play.

## Estructura

- `Assets/`: escenas, scripts, prefabs, sprites y recursos del juego.
- `Packages/`: dependencias Unity fijadas para este proyecto.
- `ProjectSettings/`: configuración de Unity, plataforma y orientación.

Los nombres internos de algunos scripts conservan `DogCrush` temporalmente para evitar una refactorización de código innecesaria; no implican que este repositorio dependa de los repositorios históricos.

## Desarrollo

La rama `main` representa una base estable. Para cambios nuevos se recomienda trabajar en ramas `feature/...` y fusionarlas cuando hayan sido comprobadas en móvil. Antes de cada cambio importante, crea una etiqueta de respaldo, por ejemplo `backup-YYYYMMDD`.

## Repositorios históricos

El proyecto original DOGCRUSH se conserva separado como referencia y respaldo. JoinDog es el punto de partida oficial para el desarrollo desde ahora.

## Licencia y seguridad

Consulta [SECURITY.md](SECURITY.md) para comunicar incidencias. Los recursos gráficos deben conservar sus licencias y procedencia antes de incorporarse al proyecto.
