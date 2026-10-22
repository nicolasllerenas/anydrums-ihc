# AnyDrums VR

Simulador de batería en realidad virtual para Meta Quest 2 (curso de Interacción Humano-Computador).

- Unity **2022.3.62f3** (Built-in Render Pipeline)
- Meta XR SDK **201** (Core + Interaction SDK), igual que en los laboratorios del curso

## Escenas

La aplicación tiene las dos escenas del informe (están en ese orden en *Build Settings*):

| Escena | Ruta | Contenido |
|---|---|---|
| 1. Menú inicial | `Assets/AnyDrums/Scenes/MainMenu.unity` | Elegir batería acústica o electrónica (RF1) y el volumen general (RF4). |
| 2. Estudio de práctica | `Assets/Scenes/Studio.unity` | Cuarto, rig, teletransporte, baquetas y **los dos kits** (`Kit_Acoustic` y `Kit_Electronic`). Se muestra el kit elegido. |

`Studio` se creó uniendo `drumsAcustic` y `drumsElectric` (se conservan en `Assets/Scenes`, pero ya no están en
*Build Settings*). Desde ahora se trabaja en `Studio`.

## Entregable 2: RF1 y RF4 completos

**Menú inicial**
- Tarjetas de *Batería acústica* (ámbar) y *Batería electrónica* (cian). La acústica viene seleccionada por defecto.
  Al elegir una se carga su banco de samples, suena un golpe de caja de ese banco y todo el menú toma su color.
- Volumen general de 0 a 100 % (75 % por defecto) con slider y botones − / + de 5 %. Al soltar el slider suena el
  golpe de prueba con el volumen nuevo.
- "Ingresar al estudio" muestra debajo lo elegido (por ejemplo *Batería electrónica · Volumen 35 %*) y entra con
  fundido a negro de 0.5 s. "Salir" cierra la aplicación.

**Paso de la configuración a la escena 2**
- `DrumSessionSettings` (en `DontDestroyOnLoad`) guarda el kit y el volumen. El estudio muestra el kit elegido, usa su
  banco de samples y conserva el volumen. Al volver al menú, la configuración actual aparece marcada.

**Panel de sesión (escena 2)**
- Se abre y se cierra con el **botón de menú del mando izquierdo** (en el Editor sin visor, con la tecla **M**).
- Aparece a la izquierda del usuario para no tapar la batería, con las mismas opciones del menú (kit y volumen),
  "Volver al menú", "Salir" y una X para cerrar.
- Cambiar de kit desde el panel **no recarga la escena**: se reemplaza el modelo y el banco de samples y el usuario
  sigue en el mismo lugar.
- Los rayos de los mandos solo se ven con el panel abierto. `SessionPanelController.IsOpen` indica si está abierto,
  para que en el Entregable 3 los gatillos no funcionen como pedales mientras se usa el panel.

**Bancos de samples**
- `Assets/AnyDrums/Audio/Bank_Acoustic.asset` y `Bank_Electronic.asset`, con 10 piezas cada uno (bombo, caja,
  hi-hat cerrado/abierto/pedal, Tom 1, Tom 2, tom base, crash y ride).
- Los sonidos actuales son **sintetizados** (respaldo). Para usar los samples del informe basta con arrastrar los
  AudioClips a los campos del banco; el generador no reemplaza los clips que ustedes asignen.
- `DrumKitAudio.Instance.Play(DrumPiece.Snare, posición, volumen, pitch)` reproduce una pieza con el banco actual:
  es lo que usarán los golpes de las baquetas (Entregables 3 a 5).

## Prueba automática

**AnyDrums > Prueba automática del Entregable 2 (Play Mode)** recorre todo el flujo sin visor: menú → elegir kit
y volumen → estudio → panel de sesión (cambiar kit y volumen, abrir/cerrar) → volver al menú → entrar de nuevo.
Hace 39 verificaciones, revisa que no haya excepciones en la consola y deja el resultado en `Logs/SmokeTest.txt`
con capturas en `Logs/`.

## Menú AnyDrums del Editor

| Comando | Para qué |
|---|---|
| Construir escena del menú (Escena 1) | Regenera `MainMenu` con sus sprites, fuentes y materiales. |
| Generar bancos de samples | Regenera los WAV sintetizados y los bancos (no toca los clips asignados a mano). |
| Actualizar panel de sesión en Studio | Reinstala el panel de sesión, el cambio de kit y el audio en `Studio` sin tocar lo demás. |
| Crear escena Studio (une drumsAcustic + drumsElectric) | Vuelve a crear `Studio` desde cero (pide confirmación). |
| Revisar escena del estudio | Lista prefabs y materiales faltantes. |
| Prueba automática del Entregable 2 | Ver arriba. |

## Pendientes conocidos

- Faltan dos materiales de `InteriorTest` (la planta y las macetas). Tienen materiales provisionales
  (`M_Provisional_Planta` y `M_Provisional_Maceta`) hasta que se importen los originales.
- La iluminación del cuarto no está horneada (las luces de área solo funcionan horneadas). `Studio` tiene una
  `[AnyDrums] Luz de relleno` para que se vea; se puede borrar al hacer *Generate Lighting*.

## Estructura

```
Assets/AnyDrums/
├── Scenes/MainMenu.unity
├── Audio/                      Bancos de samples (WAV + Bank_*.asset)
├── Resources/AnyDrums/         DrumKitCatalog (kit → banco, nombres de escenas)
├── Scripts/Runtime/            DrumSessionSettings, SceneTransition, DrumSampleBank, DrumKitCatalog, DrumPiece
├── Scripts/Runtime/Menu/       MainMenuController, KitSelector, VolumeControl, KitOptionCard, AccentTheme,
│                               MenuSelectableFeedback, MenuAudio, ControllerHaptics, PlaceInFrontOfUser, …
├── Scripts/Runtime/Studio/     SessionPanelController, StudioKitSwitcher, DrumKitAudio
├── Scripts/Editor/             Generadores de escenas, sprites y samples; prueba automática
├── Art/Generated/              Sprites e íconos generados por MenuArtGenerator
└── Fonts/                      Montserrat (SIL Open Font License, ver OFL.txt)
Assets/Scenes/Studio.unity      Escena 2
```
