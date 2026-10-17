# AnyDrums VR

Simulador de batería en realidad virtual para Meta Quest 2 (curso de Interacción Humano-Computador).

- Unity **2022.3.62f3** (Built-in Render Pipeline)
- Meta XR SDK **201** (Core + Interaction SDK), igual que en los laboratorios del curso

## Escena 1: Menú inicial

La escena `Assets/AnyDrums/Scenes/MainMenu.unity` es lo primero que ve el usuario. Implementa la
"Escena 1: Menú inicial" del informe (RF1 y RF4):

| Elemento | Detalle |
|---|---|
| Panel | Canvas en espacio de mundo a 1.5 m del usuario, centrado a la altura de los ojos (se reubica al iniciar y al recentrar la vista). Sala oscura con un brillo suave detrás del panel. |
| Tipo de batería | Dos tarjetas: **Batería acústica** (ámbar) y **Batería electrónica** (cian). La acústica viene seleccionada por defecto. La tarjeta elegida se marca con borde, brillo e insignia de check, y todo el menú toma su color. |
| Volumen general | Slider de 0 a 100 % (75 % por defecto) con botones − / + de 5 % para ajustes precisos con el rayo. Controla `AudioListener.volume`. |
| Acciones | **Ingresar al estudio** (funde a negro 0.5 s y carga `drumsAcustic` o `drumsElectric` según el kit) y **Salir**. |
| Retroalimentación | El rayo resalta y agranda el control apuntado; al presionar suena un clic y vibra el mando que presionó. Al elegir un kit suena un golpe corto de caja de ese kit, y al soltar el slider se repite con el volumen nuevo. |
| Controles | Rayo de cada mando + gatillo para seleccionar y arrastrar el slider. El joystick no se usa en el menú (el teletransporte y el giro quedan desactivados). |

Lo elegido se guarda en `DrumSessionSettings` (objeto en `DontDestroyOnLoad`), así el estudio se carga
con el kit y el volumen correctos y, al volver al menú, la configuración actual aparece marcada.

> Entregable 1: los golpes de prueba del menú se generan por código (todavía no se cargan los bancos de
> *samples*). En el Entregable 2 basta con asignar los clips reales en el componente `MenuAudio`
> del objeto `MenuCanvas`.

### Cómo usarlo en el proyecto del equipo

1. Copiar la carpeta `Assets/AnyDrums` al proyecto (o hacer merge de esta rama).
2. Abrir el menú **AnyDrums > Construir escena del menú (Escena 1)**. Genera los sprites, las fuentes,
   la escena y la deja primera en *Build Settings*, seguida de `drumsAcustic` y `drumsElectric` si existen.
3. Si las escenas del estudio tienen otro nombre, cambiarlo en `MainMenuController`
   (campos *Acoustic Scene* y *Electronic Scene*).

### Estructura

```
Assets/AnyDrums/
├── Scenes/MainMenu.unity
├── Scripts/Runtime/            DrumKitType, DrumSessionSettings, SceneTransition
├── Scripts/Runtime/Menu/       MainMenuController, KitOptionCard, MenuSelectableFeedback,
│                               MenuAudio, ProceduralDrumSounds, ControllerHaptics,
│                               PlaceInFrontOfUser, DisableLocomotionInMenu, PointerUpNotifier
├── Scripts/Editor/             MainMenuSceneBuilder, MenuArtGenerator, MenuPalette
├── Art/Generated/              Sprites e íconos generados por MenuArtGenerator
└── Fonts/                      Montserrat (SIL Open Font License, ver OFL.txt)
```
