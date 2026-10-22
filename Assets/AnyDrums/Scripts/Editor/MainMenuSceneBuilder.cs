using System;
using System.IO;
using AnyDrums.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static AnyDrums.EditorTools.MenuUIKit;
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

        // OVRCameraRigInteraction (el mismo rig que usa el estudio).
        public const string CameraRigPrefabGuid = "f54012181eacade4ea242c65f804720f";

        // El panel mide 1200 x 880 unidades; con esta escala ocupa 1.08 x 0.79 m (≈ 40° x 30° a 1.5 m).
        const float CanvasScale = 0.0009f;
        const float PanelWidth = 1200f;
        const float PanelHeight = 880f;
        const float PanelDistance = 1.5f;
        const float EyeHeight = 1.55f;

        [MenuItem("AnyDrums/Construir escena del menú (Escena 1)", priority = 0)]
        public static void Build()
        {
            if (!EnsureTmpEssentials()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        /// <summary>
        /// Uso desde la línea de comandos: Unity -projectPath . -executeMethod AnyDrums.EditorTools.MainMenuSceneBuilder.BuildFromCommandLine
        /// Genera los bancos de samples, el menú y (si falta) la escena Studio, guarda capturas en Logs/ y cierra el Editor.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                if (!EnsureTmpEssentials()) throw new InvalidOperationException("Faltan los TMP Essential Resources.");
                DrumSampleSynth.GenerateAll();
                BuildScene();
                CapturePreview("Logs/MainMenuPreview.png");
                StudioSceneBuilder.BuildOrUpdate(overwrite: false);
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
        public static void CapturePreview(string path) =>
            CaptureView(path, new Vector3(0f, EyeHeight + 0.05f, 0f), Quaternion.Euler(4f, 0f, 0f), 62f);

        public static void CaptureView(string path, Vector3 position, Quaternion rotation, float fieldOfView)
        {
            Canvas.ForceUpdateCanvases();
            var go = new GameObject("PreviewCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(position, rotation);
                cam.fieldOfView = fieldOfView;
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
            var fonts = LoadFonts();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            Camera headCamera = CreateRig();
            EnsureEventSystem();
            CreateEnvironment();
            CreateMenu(fonts, headCamera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsUtil.Apply();
            Debug.Log($"[AnyDrums] Escena del menú generada en {ScenePath}.");
        }

        // ---------- Escena ----------

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
            var accent = new AccentTargets();

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

            var canvas = CreateWorldCanvas("MenuCanvas", root.transform, new Vector2(PanelWidth, PanelHeight), CanvasScale,
                headCamera, out var menuAudio);
            var canvasRt = (RectTransform)canvas.transform;
            canvasRt.localPosition = new Vector3(0f, EyeHeight, 0f);

            accent.Graphics.Add(Img(Centered(UI("Halo", canvasRt), 2100f, 1500f), Art("glow"), WithAlpha(acoustic, 0.14f)));
            PanelBackground(canvasRt, 44f);
            var panel = canvasRt;

            // Encabezado
            var logo = Img(At(UI("Logo", panel), 64f, 52f, 76f, 76f), Art("round_fill"), acoustic, Radius(20f));
            accent.Graphics.Add(logo);
            Img(Centered(UI("Sticks", logo.rectTransform), 50f, 50f), Art("icon_sticks"), MenuPalette.OnAccent);
            var title = Text(At(UI("Title", panel), 164f, 44f, 760f, 62f), "AnyDrums VR", fonts.Bold, 50f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            Text(At(UI("Subtitle", panel), 165f, 102f, 900f, 32f), "Configura tu sesión de práctica antes de entrar al estudio",
                fonts.Regular, 23f, MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            Img(At(UI("Divider", panel), 64f, 160f, PanelWidth - 128f, 2f), null, White06);

            // 1. Tipo de batería (RF1)
            accent.Graphics.Add(SectionHeader(panel, fonts, 64f, 190f, "1", "ELIGE TU BATERÍA"));
            var kitSelector = KitSelectorRow(panel, fonts, menuAudio, 64f, 244f, PanelWidth - 128f, 220f, compact: false);

            // 2. Volumen general (RF4)
            accent.Graphics.Add(SectionHeader(panel, fonts, 64f, 500f, "2", "VOLUMEN GENERAL"));
            var volume = VolumeRow(panel, fonts, menuAudio, 64f, 552f, PanelWidth - 128f, accent);

            // Acciones
            var enter = PillButton(panel, menuAudio, "EnterButton", 64f, 680f, 744f, 108f, "Ingresar al estudio", fonts.Bold, 31f,
                MenuPalette.OnAccent, Art("icon_arrow"), 34f, iconFirst: false, primary: true, fonts: fonts,
                summary: "Batería acústica  ·  Volumen 75 %");
            accent.Buttons.Add(enter.GetComponent<MenuSelectableFeedback>());
            var exit = PillButton(panel, menuAudio, "ExitButton", 832f, 680f, 304f, 108f, "Salir", fonts.SemiBold, 29f,
                MenuPalette.TextPrimary, Art("icon_power"), 30f, iconFirst: true, primary: false);

            var hint = Text(At(UI("Hint", panel), 64f, 812f, PanelWidth - 128f, 40f),
                "Apunta con el mando y presiona el gatillo para seleccionar", fonts.Regular, 21f,
                MenuPalette.TextMuted, TextAlignmentOptions.Center);

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            Set(controller, "_kitSelector", kitSelector);
            Set(controller, "_volumeControl", volume);
            Set(controller, "_enterButton", enter);
            Set(controller, "_enterSummary", FindDeep(enter.transform, "Summary").GetComponent<TMP_Text>());
            Set(controller, "_exitButton", exit);
            Set(controller, "_hintLabel", hint);
            Set(controller, "_panelGroup", canvas.GetComponent<CanvasGroup>());

            AddAccentTheme(canvas.gameObject, accent, floorGlowRenderer, title);
            Set(placer, "_panel", canvasRt);

            title.text = $"AnyDrums <color=#{ColorUtility.ToHtmlStringRGB(acoustic)}>VR</color>";
        }
    }
}
