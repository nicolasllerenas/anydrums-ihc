using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyDrums.Menu;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Construye la Escena 1 (menú inicial) tal como se describe en el informe: sala oscura con una luz suave,
    /// panel World-Space a 1.5 m y a la altura de los ojos, dos tarjetas para elegir batería acústica o electrónica,
    /// slider de volumen (0–100 %, 75 % por defecto) y los botones "Ingresar al estudio" y "Salir".
    /// Se puede volver a ejecutar en cualquier momento: regenera la escena y sus recursos.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/AnyDrums/Scenes/MainMenu.unity";
        const string MaterialsFolder = "Assets/AnyDrums/Materials";
        const string FontsFolder = "Assets/AnyDrums/Fonts";

        // OVRCameraRigInteraction (el mismo rig que usan las escenas del estudio).
        const string CameraRigPrefabGuid = "f54012181eacade4ea242c65f804720f";
        // Plantilla que usa el asistente "Add Ray Interaction to Canvas" del Interaction SDK.
        const string RayCanvasTemplateGuid = "8369d93f7b6b99742bbea0649a41b7b1";

        static readonly string[] StudioScenes = { "drumsAcustic", "drumsElectric" };

        // El panel mide 1200 x 880 unidades; con esta escala ocupa 1.08 x 0.79 m (≈ 40° x 30° a 1.5 m).
        const float CanvasScale = 0.0009f;
        const float PanelWidth = 1200f;
        const float PanelHeight = 880f;
        const float PanelDistance = 1.5f;
        const float EyeHeight = 1.55f;
        const int UILayer = 5;

        const string FontCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡¿ÁÉÍÓÚÜÑáéíóúüñ·–—…";

        static readonly Color White06 = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color White08 = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color White12 = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color White16 = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color White26 = new Color(1f, 1f, 1f, 0.26f);

        class Fonts
        {
            public TMP_FontAsset Regular, SemiBold, Bold;
        }

        [MenuItem("AnyDrums/Construir escena del menú (Escena 1)", priority = 0)]
        public static void Build()
        {
            if (!EnsureTmpEssentials()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>
        /// Uso desde la línea de comandos: Unity -projectPath . -executeMethod AnyDrums.EditorTools.MainMenuSceneBuilder.BuildFromCommandLine
        /// Construye la escena, guarda una captura de la vista del usuario en Logs/MainMenuPreview.png y cierra el Editor.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                if (!EnsureTmpEssentials()) throw new InvalidOperationException("Faltan los TMP Essential Resources.");
                BuildScene();
                CapturePreview("Logs/MainMenuPreview.png");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        [MenuItem("AnyDrums/Guardar captura del menú", priority = 1)]
        static void CapturePreviewMenu() => CapturePreview("Logs/MainMenuPreview.png");

        /// <summary>Renderiza la escena desde la posición de la cabeza del usuario (sin visor) y la guarda como PNG.</summary>
        public static void CapturePreview(string path)
        {
            Canvas.ForceUpdateCanvases();
            var go = new GameObject("PreviewCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight + 0.05f, 0f), Quaternion.Euler(4f, 0f, 0f));
                cam.fieldOfView = 62f;
                cam.nearClipPlane = 0.05f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = MenuPalette.Background;
                var rt = new RenderTexture(1800, 1200, 24) { antiAliasing = 8 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(image);
                Debug.Log($"[AnyDrums] Captura guardada en {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            MenuArtGenerator.GenerateAll();
            var fonts = new Fonts
            {
                Regular = GetOrCreateFont("Montserrat-Regular"),
                SemiBold = GetOrCreateFont("Montserrat-SemiBold"),
                Bold = GetOrCreateFont("Montserrat-Bold"),
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            Camera headCamera = CreateRig();
            CreateEventSystem();
            CreateEnvironment();
            CreateMenu(fonts, headCamera);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
            Debug.Log($"[AnyDrums] Escena del menú generada en {ScenePath}.");
        }

        // ---------- Recursos ----------

        static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
            Debug.LogWarning("[AnyDrums] Se importaron los TMP Essential Resources. Ejecuta de nuevo AnyDrums > Construir escena del menú.");
            return false;
        }

        static TMP_FontAsset GetOrCreateFont(string fontName)
        {
            string assetPath = $"{FontsFolder}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/{fontName}.ttf");
            if (font == null)
            {
                Debug.LogWarning($"[AnyDrums] No se encontró {fontName}.ttf; se usa la fuente por defecto de TextMeshPro.");
                return TMP_Settings.defaultFontAsset;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(FontCharacters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[AnyDrums] {fontName} no tiene: {missing}");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(MenuArtGenerator.PathOf(name));

        static Material GetOrCreateMaterial(string name, string shaderName, Action<Material> setup)
        {
            Directory.CreateDirectory(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(shaderName);
            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static T LoadByGuid<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        // ---------- Escena ----------

        static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.23f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MenuPalette.Background;
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 9f;

            var lighting = new LightingSettings { name = "MainMenu Lighting", bakedGI = false, realtimeGI = false, autoGenerate = false };
            string lightingPath = Path.ChangeExtension(ScenePath, null) + " Lighting.lighting";
            AssetDatabase.DeleteAsset(lightingPath);
            AssetDatabase.CreateAsset(lighting, lightingPath);
            Lightmapping.lightingSettings = lighting;

            // Luz suave para que los mandos y las manos no se vean negros; el panel y el piso no la necesitan.
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static Camera CreateRig()
        {
            var prefab = LoadByGuid<GameObject>(CameraRigPrefabGuid);
            if (prefab == null)
            {
                Debug.LogError("[AnyDrums] No se encontró el prefab OVRCameraRigInteraction (Meta XR Interaction SDK).");
                var fallback = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                fallback.transform.position = new Vector3(0f, EyeHeight + 0.05f, 0f);
                fallback.clearFlags = CameraClearFlags.SolidColor;
                fallback.backgroundColor = MenuPalette.Background;
                return fallback;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = prefab.name;
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var camera = FindDeep(rig.transform, "CenterEyeAnchor").GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MenuPalette.Background;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            return camera;
        }

        static void CreateEventSystem()
        {
            // PointableCanvasModule convierte los rayos del Interaction SDK en eventos de uGUI.
            new GameObject("EventSystem", typeof(EventSystem), typeof(PointableCanvasModule));
        }

        static void CreateEnvironment()
        {
            var floorMaterial = GetOrCreateMaterial("M_MenuFloor", "Unlit/Texture", m =>
                m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuArtGenerator.PathOf("floor_grid")));

            var environment = new GameObject("Environment");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(environment.transform, false);
            floor.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = floorMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic);
        }

        // ---------- Menú ----------

        static void CreateMenu(Fonts fonts, Camera headCamera)
        {
            Color acoustic = MenuPalette.AcousticAccent;
            Color electronic = MenuPalette.ElectronicAccent;
            var accentGraphics = new List<Graphic>();

            var root = new GameObject("MainMenu");
            root.transform.position = new Vector3(0f, 0f, PanelDistance);
            var placer = root.AddComponent<PlaceInFrontOfUser>();
            root.AddComponent<DisableLocomotionInMenu>();

            // Reflejo de luz del panel sobre el piso; toma el color del kit elegido.
            var floorGlowMaterial = GetOrCreateMaterial("M_MenuFloorGlow", "Legacy Shaders/Particles/Additive", m =>
            {
                m.mainTexture = Art("glow").texture;
                m.SetColor("_TintColor", new Color(acoustic.r, acoustic.g, acoustic.b, 0.22f));
            });
            var floorGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorGlow.name = "FloorGlow";
            Object.DestroyImmediate(floorGlow.GetComponent<Collider>());
            floorGlow.transform.SetParent(root.transform, false);
            floorGlow.transform.localPosition = new Vector3(0f, 0.004f, -0.35f);
            floorGlow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            floorGlow.transform.localScale = new Vector3(2.6f, 1.7f, 1f);
            var floorGlowRenderer = floorGlow.GetComponent<MeshRenderer>();
            floorGlowRenderer.sharedMaterial = floorGlowMaterial;
            floorGlowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            floorGlowRenderer.receiveShadows = false;

            // Canvas en espacio de mundo.
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(AudioSource));
            canvasGo.layer = UILayer;
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.SetParent(root.transform, false);
            canvasRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            canvasRt.localPosition = new Vector3(0f, EyeHeight, 0f);
            canvasRt.localScale = Vector3.one * CanvasScale;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = headCamera;

            var audioSource = canvasGo.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.6f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;
            audioSource.dopplerLevel = 0f;
            var menuAudio = canvasGo.AddComponent<MenuAudio>();
            var controller = canvasGo.AddComponent<MainMenuController>();

            AddRayInteraction(canvas);

            var halo = Img(Centered(UI("Halo", canvasRt), 2100f, 1500f), Art("glow"), WithAlpha(acoustic, 0.14f));
            accentGraphics.Add(halo);

            var panel = Stretch(UI("Panel", canvasRt));
            Img(panel, Art("round_fill"), MenuPalette.Panel, Radius(44f));
            Img(Stretch(UI("Border", panel)), Art("outline_panel"), White08, 2f);

            // Encabezado
            var logo = Img(At(UI("Logo", panel), 64f, 52f, 76f, 76f), Art("round_fill"), acoustic, Radius(20f));
            accentGraphics.Add(logo);
            Img(Centered(UI("Sticks", logo.rectTransform), 50f, 50f), Art("icon_sticks"), MenuPalette.OnAccent);
            var title = Text(At(UI("Title", panel), 164f, 44f, 760f, 62f), "AnyDrums VR", fonts.Bold, 50f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            Text(At(UI("Subtitle", panel), 165f, 102f, 900f, 32f), "Configura tu sesión de práctica antes de entrar al estudio",
                fonts.Regular, 23f, MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            Img(At(UI("Divider", panel), 64f, 160f, PanelWidth - 128f, 2f), null, White06);

            // 1. Tipo de batería
            accentGraphics.Add(SectionHeader(panel, fonts, 190f, "1", "ELIGE TU BATERÍA"));
            var acousticCard = KitCard(panel, fonts, menuAudio, 64f, 244f, DrumKitType.Acoustic, acoustic,
                Art("icon_acoustic"), "Batería acústica",
                "Timbre cálido y natural, como un kit de estudio real.", "ROCK · JAZZ · POP");
            var electronicCard = KitCard(panel, fonts, menuAudio, 616f, 244f, DrumKitType.Electronic, electronic,
                Art("icon_electronic"), "Batería electrónica",
                "Sonidos de caja de ritmos y pads, precisos y modernos.", "ELECTRÓNICA · HIP-HOP · EDM");

            // 2. Volumen general
            accentGraphics.Add(SectionHeader(panel, fonts, 500f, "2", "VOLUMEN GENERAL"));
            var volumeIcon = Img(At(UI("VolumeIcon", panel), 64f, 563f, 50f, 50f), Art("icon_volume_high"), MenuPalette.TextSecondary);
            var volumeDown = RoundIconButton(panel, menuAudio, "VolumeDown", 134f, 556f, Art("icon_minus"));
            var slider = VolumeSlider(panel, menuAudio, 222f, 552f, 660f, 72f, acoustic, accentGraphics);
            var volumeUp = RoundIconButton(panel, menuAudio, "VolumeUp", 906f, 556f, Art("icon_plus"));
            var volumeValue = Text(At(UI("VolumeValue", panel), 990f, 554f, 146f, 68f), "75 %", fonts.SemiBold, 36f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineRight);

            // Acciones
            var enter = PillButton(panel, menuAudio, "EnterButton", 64f, 684f, 744f, 100f, "Ingresar al estudio", fonts.Bold, 31f,
                MenuPalette.OnAccent, Art("icon_arrow"), 34f, iconFirst: false, background: acoustic, outlined: false);
            var exit = PillButton(panel, menuAudio, "ExitButton", 832f, 684f, 304f, 100f, "Salir", fonts.SemiBold, 29f,
                MenuPalette.TextPrimary, Art("icon_power"), 30f, iconFirst: true, background: White06, outlined: true);
            var exitFeedback = exit.GetComponent<MenuSelectableFeedback>();
            Set(exitFeedback, "_normalColor", White06);
            Set(exitFeedback, "_hoverColor", White16);
            Set(exitFeedback, "_pressedColor", White26);

            var hint = Text(At(UI("Hint", panel), 64f, 812f, PanelWidth - 128f, 40f),
                "Apunta con el mando y presiona el gatillo para seleccionar", fonts.Regular, 21f,
                MenuPalette.TextMuted, TextAlignmentOptions.Center);

            Set(placer, "_panel", canvasRt);
            Set(controller, "_acousticCard", acousticCard);
            Set(controller, "_electronicCard", electronicCard);
            Set(controller, "_volumeSlider", slider);
            Set(controller, "_volumeValueLabel", volumeValue);
            Set(controller, "_volumeIcon", volumeIcon);
            Set(controller, "_volumeMuteSprite", Art("icon_volume_mute"));
            Set(controller, "_volumeLowSprite", Art("icon_volume_low"));
            Set(controller, "_volumeHighSprite", Art("icon_volume_high"));
            Set(controller, "_volumeDownButton", volumeDown);
            Set(controller, "_volumeUpButton", volumeUp);
            Set(controller, "_volumeRelease", slider.GetComponent<PointerUpNotifier>());
            Set(controller, "_enterButton", enter);
            Set(controller, "_enterButtonFeedback", enter.GetComponent<MenuSelectableFeedback>());
            Set(controller, "_exitButton", exit);
            Set(controller, "_acousticScene", StudioScenes[0]);
            Set(controller, "_electronicScene", StudioScenes[1]);
            Set(controller, "_accentGraphics", accentGraphics.Cast<Object>().ToArray());
            Set(controller, "_floorGlow", floorGlowRenderer);
            Set(controller, "_title", title);
            Set(controller, "_hintLabel", hint);
            Set(controller, "_panelGroup", canvasGo.GetComponent<CanvasGroup>());
            Set(controller, "_audio", menuAudio);

            // Estado inicial visible en el Editor: acústica seleccionada y volumen al 75 %.
            title.text = $"AnyDrums <color=#{ColorUtility.ToHtmlStringRGB(acoustic)}>VR</color>";
            acousticCard.SetSelected(true, instant: true);
            electronicCard.SetSelected(false, instant: true);
            enter.GetComponent<MenuSelectableFeedback>().SetBaseColor(acoustic);
        }

        static void AddRayInteraction(Canvas canvas)
        {
            var template = LoadByGuid<GameObject>(RayCanvasTemplateGuid);
            if (template == null)
            {
                Debug.LogError("[AnyDrums] No se encontró la plantilla de Ray Canvas del Interaction SDK.");
                return;
            }
            var interaction = (GameObject)Object.Instantiate(template, canvas.transform, false);
            interaction.name = "ISDK_RayCanvasInteraction";
            Stretch((RectTransform)interaction.transform);
            interaction.GetComponent<PointableCanvas>().InjectCanvas(canvas);
        }

        static Graphic SectionHeader(RectTransform panel, Fonts fonts, float y, string number, string label)
        {
            var badge = Img(At(UI($"Step{number}", panel), 64f, y, 34f, 34f), Art("circle_fill"), MenuPalette.AcousticAccent);
            Text(Stretch(UI("Number", badge.rectTransform)), number, fonts.Bold, 19f, MenuPalette.OnAccent, TextAlignmentOptions.Center);
            Text(At(UI($"Step{number}Label", panel), 112f, y, 700f, 34f), label, fonts.SemiBold, 20f,
                MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, spacing: 8f);
            return badge;
        }

        static KitOptionCard KitCard(RectTransform panel, Fonts fonts, MenuAudio audio, float x, float y, DrumKitType kit,
            Color accent, Sprite icon, string title, string description, string genres)
        {
            var card = At(UI(kit == DrumKitType.Acoustic ? "AcousticCard" : "ElectronicCard", panel), x, y, 520f, 220f);

            var glowRt = UI("Glow", card);
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-36f, -36f);
            glowRt.offsetMax = new Vector2(36f, 36f);
            var glow = Img(glowRt, Art("card_glow"), WithAlpha(accent, 0f), 1f);

            var background = Img(Stretch(UI("Background", card)), Art("round_fill"), new Color32(0x1A, 0x1D, 0x25, 0xFF), Radius(28f), raycast: true);
            var border = Img(Stretch(UI("Border", card)), Art("outline_card"), new Color(1f, 1f, 1f, 0.1f), 2f);
            var iconImage = Img(At(UI("Icon", card), 30f, 60f, 100f, 100f), icon, Color.white);

            var titleText = Text(At(UI("Title", card), 154f, 32f, 340f, 46f), title, fonts.SemiBold, 29f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 22f;
            titleText.fontSizeMax = 29f;
            var descriptionText = Text(At(UI("Description", card), 154f, 84f, 340f, 66f), description, fonts.Regular, 21f,
                MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft);
            descriptionText.enableWordWrapping = true;
            var genresText = Text(At(UI("Genres", card), 154f, 164f, 340f, 28f), genres, fonts.SemiBold, 16f,
                accent, TextAlignmentOptions.MidlineLeft, spacing: 6f);

            var badgeRt = At(UI("CheckBadge", card), 462f, 18f, 40f, 40f);
            Img(badgeRt, Art("circle_fill"), accent);
            var badgeGroup = badgeRt.gameObject.AddComponent<CanvasGroup>();
            badgeGroup.interactable = false;
            badgeGroup.blocksRaycasts = false;
            Img(Centered(UI("Check", badgeRt), 24f, 24f), Art("icon_check"), MenuPalette.OnAccent);

            var button = card.gameObject.AddComponent<Button>();
            ConfigureSelectable(button, background);

            var option = card.gameObject.AddComponent<KitOptionCard>();
            Set(option, "_kit", kit);
            Set(option, "_accent", accent);
            Set(option, "_button", button);
            Set(option, "_background", background);
            Set(option, "_border", border);
            Set(option, "_glow", glow);
            Set(option, "_icon", iconImage);
            Set(option, "_title", titleText);
            Set(option, "_genres", genresText);
            Set(option, "_checkBadge", badgeGroup);

            // El golpe de prueba del kit reemplaza al clic, por eso esta tarjeta no lo reproduce.
            var feedback = card.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_hoverScale", 1.025f);
            Set(feedback, "_clickSound", false);
            Set(feedback, "_hapticAmplitude", 0.5f);
            return option;
        }

        static Slider VolumeSlider(RectTransform panel, MenuAudio audio, float x, float y, float width, float height,
            Color accent, List<Graphic> accentGraphics)
        {
            const float handleSize = 44f;
            const float trackHeight = 16f;
            var root = At(UI("VolumeSlider", panel), x, y, width, height);
            // Área invisible más alta que la barra para que sea fácil apuntarle con el rayo (ley de Fitts).
            Img(root, null, new Color(1f, 1f, 1f, 0f), raycast: true);

            var track = UI("Track", root);
            SetHorizontalBand(track, handleSize / 2f, trackHeight);
            Img(track, Art("round_fill"), White12, Radius(trackHeight / 2f));

            var fillArea = UI("FillArea", root);
            SetHorizontalBand(fillArea, handleSize / 2f, trackHeight);
            var fill = UI("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.75f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            accentGraphics.Add(Img(fill, Art("round_fill"), accent, Radius(trackHeight / 2f)));

            var handleArea = UI("HandleArea", root);
            SetHorizontalBand(handleArea, handleSize / 2f, handleSize);
            var handle = UI("Handle", handleArea);
            handle.anchorMin = new Vector2(0.75f, 0f);
            handle.anchorMax = new Vector2(0.75f, 1f);
            handle.sizeDelta = new Vector2(handleSize, 0f);
            Img(Centered(UI("Shadow", handle), 100f, 100f), Art("glow"), new Color(0f, 0f, 0f, 0.6f));
            var knob = Img(Stretch(UI("Knob", handle)), Art("circle_fill"), Color.white);
            accentGraphics.Add(Img(Centered(UI("Dot", handle), 14f, 14f), Art("circle_fill"), accent));

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(75f);
            slider.transition = Selectable.Transition.ColorTint;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.35f);
            colors.pressedColor = accent;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            slider.colors = colors;

            root.gameObject.AddComponent<PointerUpNotifier>();
            var feedback = root.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_scaleTarget", handle);
            Set(feedback, "_hoverScale", 1.15f);
            Set(feedback, "_pressedScale", 1.22f);
            Set(feedback, "_clickSound", false);
            Set(feedback, "_hapticAmplitude", 0.3f);
            return slider;
        }

        static Button RoundIconButton(RectTransform panel, MenuAudio audio, string name, float x, float y, Sprite icon)
        {
            var root = At(UI(name, panel), x, y, 64f, 64f);
            var background = Img(root, Art("circle_fill"), White06, raycast: true);
            Img(Stretch(UI("Outline", root)), Art("circle_outline"), White16);
            Img(Centered(UI("Icon", root), 26f, 26f), icon, MenuPalette.TextPrimary);

            var button = root.gameObject.AddComponent<Button>();
            ConfigureSelectable(button, background);
            var feedback = root.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_hoverScale", 1.08f);
            Set(feedback, "_hapticAmplitude", 0.4f);
            Set(feedback, "_tintTarget", background);
            Set(feedback, "_normalColor", White06);
            Set(feedback, "_hoverColor", White16);
            Set(feedback, "_pressedColor", White26);
            return button;
        }

        static Button PillButton(RectTransform panel, MenuAudio audio, string name, float x, float y, float width, float height,
            string label, TMP_FontAsset font, float fontSize, Color labelColor, Sprite icon, float iconSize, bool iconFirst,
            Color background, bool outlined)
        {
            var root = At(UI(name, panel), x, y, width, height);
            var backgroundImage = Img(root, Art("round_fill"), background, Radius(height / 2f), raycast: true);
            if (outlined) Img(Stretch(UI("Outline", root)), Art("outline_pill"), new Color(1f, 1f, 1f, 0.18f), 2f);

            var content = Stretch(UI("Content", root));
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var labelRt = UI("Label", content);
            labelRt.sizeDelta = new Vector2(0f, height * 0.6f);
            var iconRt = UI("Icon", content);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            Img(iconRt, icon, labelColor);
            var iconLayout = iconRt.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
            if (iconFirst) iconRt.SetAsFirstSibling();
            Text(labelRt, label, font, fontSize, labelColor, TextAlignmentOptions.Midline);

            var button = root.gameObject.AddComponent<Button>();
            ConfigureSelectable(button, backgroundImage);
            var feedback = root.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_hoverScale", 1.03f);
            Set(feedback, "_hapticAmplitude", 0.5f);
            Set(feedback, "_tintTarget", backgroundImage);
            feedback.SetBaseColor(background);
            return button;
        }

        static void ConfigureSelectable(Selectable selectable, Graphic target)
        {
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = target;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        static void UpdateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            foreach (string sceneName in StudioScenes)
            {
                foreach (string guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) != sceneName || scenes.Any(s => s.path == path)) continue;
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            var missing = StudioScenes.Where(n => scenes.All(s => Path.GetFileNameWithoutExtension(s.path) != n)).ToArray();
            if (missing.Length > 0)
                Debug.LogWarning($"[AnyDrums] Faltan en Build Settings las escenas del estudio: {string.Join(", ", missing)}. " +
                                 "Agrégalas para que \"Ingresar al estudio\" pueda cargarlas.");
        }

        // ---------- Utilidades de UI ----------

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Posiciona con coordenadas desde la esquina superior izquierda del padre (pivote al centro).</summary>
        static RectTransform At(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x + width / 2f, -(y + height / 2f));
            return rt;
        }

        static RectTransform Centered(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void SetHorizontalBand(RectTransform rt, float horizontalInset, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(horizontalInset, -height / 2f);
            rt.offsetMax = new Vector2(-horizontalInset, height / 2f);
        }

        static float Radius(float radius) => MenuArtGenerator.BaseRadius / radius;

        static Image Img(RectTransform rt, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            }
            else if (sprite != null)
            {
                image.preserveAspect = true;
            }
            return image;
        }

        static TextMeshProUGUI Text(RectTransform rt, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment, float spacing = 0f)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.characterSpacing = spacing;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Asigna un campo serializado (incluidos los privados con [SerializeField]).</summary>
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new ArgumentException($"{target.GetType().Name} no tiene el campo {field}");
            switch (value)
            {
                case Object[] objects:
                    property.arraySize = objects.Length;
                    for (int i = 0; i < objects.Length; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                    break;
                case Object obj: property.objectReferenceValue = obj; break;
                case Enum e: property.enumValueIndex = Convert.ToInt32(e); break;
                case float f: property.floatValue = f; break;
                case int i: property.intValue = i; break;
                case bool b: property.boolValue = b; break;
                case Color c: property.colorValue = c; break;
                case string s: property.stringValue = s; break;
                default: throw new ArgumentException($"Tipo no soportado para {field}: {value?.GetType()}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
