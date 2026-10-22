using System.IO;
using System.Linq;
using AnyDrums.Menu;
using AnyDrums.Studio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AnyDrums.EditorTools.MenuUIKit;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Escena 2 (estudio de práctica). Une drumsAcustic y drumsElectric en una sola escena "Studio" con los dos
    /// kits, como describe el informe, para que cambiar de batería desde el panel de sesión no recargue la escena.
    /// También instala el panel de sesión, el cambio de kit y el reproductor de samples.
    /// </summary>
    public static class StudioSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Studio.unity";
        const string SystemsName = "[AnyDrums] Studio";
        const string FillLightName = "[AnyDrums] Luz de relleno";

        // Panel de sesión: 1000 x 700 unidades con esta escala ocupa 0.65 x 0.46 m (≈ 40° de ancho a 0.9 m).
        const float PanelScale = 0.00065f;
        const float PanelWidth = 1000f;
        const float PanelHeight = 700f;

        [MenuItem("AnyDrums/Crear escena Studio (une drumsAcustic + drumsElectric)", priority = 30)]
        static void CreateFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("AnyDrums",
                    "Ya existe Studio.unity. ¿Volver a crearla desde drumsAcustic y drumsElectric? Se perderán los cambios hechos en Studio.",
                    "Volver a crear", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildOrUpdate(overwrite: true);
        }

        [MenuItem("AnyDrums/Actualizar panel de sesión en Studio", priority = 31)]
        static void UpdateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildOrUpdate(overwrite: false);
        }

        /// <summary>Crea Studio si no existe (o si se pide sobrescribir) y reinstala sus sistemas.</summary>
        public static void BuildOrUpdate(bool overwrite)
        {
            if (!EnsureTmpEssentials()) return;
            Scene scene = !File.Exists(ScenePath) || overwrite
                ? MergeKitScenes()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallSystems(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSettingsUtil.Apply();
            Debug.Log($"[AnyDrums] Escena del estudio lista en {ScenePath}.");
            CapturePreviews(scene);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static string FindScene(string name) =>
            AssetDatabase.FindAssets($"{name} t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);

        // Toma drumsElectric como base (cuarto, rig, teletransporte y baquetas son iguales en las dos)
        // y le agrega el kit acústico de drumsAcustic sin perder sus ajustes.
        static Scene MergeKitScenes()
        {
            string electricPath = FindScene("drumsElectric");
            string acousticPath = FindScene("drumsAcustic");
            if (electricPath == null || acousticPath == null)
                throw new FileNotFoundException("Se necesitan las escenas drumsAcustic y drumsElectric para crear Studio.");

            var scene = EditorSceneManager.OpenScene(electricPath, OpenSceneMode.Single);
            var acousticScene = EditorSceneManager.OpenScene(acousticPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var acousticKit = new GameObject("Kit_Acoustic");
            foreach (var go in acousticScene.GetRootGameObjects().Where(g => g.name == "drumer_black_jack" || g.name == "Base"))
            {
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetParent(acousticKit.transform, true);
            }
            EditorSceneManager.CloseScene(acousticScene, true);

            var electronicKit = new GameObject("Kit_Electronic");
            foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "edrum"))
                go.transform.SetParent(electronicKit.transform, true);

            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        static void InstallSystems(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == SystemsName || g.name == FillLightName))
                Object.DestroyImmediate(old);

            var roots = scene.GetRootGameObjects();
            var acousticKit = roots.FirstOrDefault(g => g.name == "Kit_Acoustic");
            var electronicKit = roots.FirstOrDefault(g => g.name == "Kit_Electronic");
            var rig = roots.FirstOrDefault(g => g.GetComponentInChildren<OVRCameraRig>(true) != null);
            var headCamera = rig != null ? FindDeep(rig.transform, "CenterEyeAnchor")?.GetComponent<Camera>() : null;

            var systems = new GameObject(SystemsName);
            var switcher = systems.AddComponent<StudioKitSwitcher>();
            Set(switcher, "_acousticKit", acousticKit);
            Set(switcher, "_electronicKit", electronicKit);
            new GameObject("DrumKitAudio", typeof(DrumKitAudio)).transform.SetParent(systems.transform, false);
            EnsureEventSystem();
            BuildSessionPanel(systems.transform, LoadFonts(), headCamera);

            // El cuarto solo tiene luces de área (que necesitan iluminación horneada) y spots: sin hornear se ve
            // casi negro. Esta luz suave permite ver el cuarto y la batería; se puede borrar al hornear la iluminación.
            if (Lightmapping.lightingDataAsset == null)
            {
                var fill = new GameObject(FillLightName).AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(1f, 0.93f, 0.84f);
                fill.intensity = 0.55f;
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.Euler(55f, 140f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.36f, 0.35f, 0.34f);
                RenderSettings.ambientEquatorColor = new Color(0.26f, 0.24f, 0.22f);
                RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.10f);
            }
            FillMissingMaterials(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // InteriorTest.prefab usa dos materiales que no vinieron con los assets (la planta y las macetas) y sin
        // ellos se ven rosados. Se rellenan solo los espacios vacíos con materiales provisionales; al importar los
        // materiales originales basta con asignarlos de nuevo.
        static void FillMissingMaterials(Scene scene)
        {
            var plant = PlaceholderMaterial("M_Provisional_Planta", new Color(0.24f, 0.42f, 0.2f));
            var pot = PlaceholderMaterial("M_Provisional_Maceta", new Color(0.58f, 0.31f, 0.2f));
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                var materials = renderer.sharedMaterials;
                if (!materials.Any(m => m == null)) continue;
                var replacement = renderer.name.StartsWith("Tree") ? plant : pot;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == null) materials[i] = replacement;
                renderer.sharedMaterials = materials;
                if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                Debug.Log($"[AnyDrums] Material provisional asignado a {renderer.name}.");
            }
        }

        static Material PlaceholderMaterial(string name, Color color)
        {
            string path = $"Assets/AnyDrums/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(Shader.Find("Standard")) { color = color };
                material.SetFloat("_Glossiness", 0.2f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static void BuildSessionPanel(Transform parent, Fonts fonts, Camera headCamera)
        {
            var accent = new AccentTargets();
            Color acoustic = MenuPalette.AcousticAccent;

            var root = new GameObject("SessionPanel");
            root.transform.SetParent(parent, false);
            var controller = root.AddComponent<SessionPanelController>();

            var canvas = CreateWorldCanvas("SessionCanvas", root.transform, new Vector2(PanelWidth, PanelHeight), PanelScale,
                headCamera, out var audio);
            var panel = (RectTransform)canvas.transform;
            PanelBackground(panel, 36f);

            // Encabezado
            var logo = Img(At(UI("Logo", panel), 48f, 40f, 60f, 60f), Art("round_fill"), acoustic, Radius(16f));
            accent.Graphics.Add(logo);
            Img(Centered(UI("Sticks", logo.rectTransform), 40f, 40f), Art("icon_sticks"), MenuPalette.OnAccent);
            Text(At(UI("Title", panel), 128f, 36f, 700f, 44f), "Sesión de práctica", fonts.Bold, 34f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            Text(At(UI("Subtitle", panel), 129f, 80f, 700f, 28f), "Cambia la batería o el volumen sin salir del estudio",
                fonts.Regular, 19f, MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            var close = RoundIconButton(panel, audio, "CloseButton", PanelWidth - 48f - 60f, 40f, Art("icon_close"), 60f);
            Img(At(UI("Divider", panel), 48f, 124f, PanelWidth - 96f, 2f), null, White06);

            // 1. Tipo de batería (RF1)
            accent.Graphics.Add(SectionHeader(panel, fonts, 48f, 146f, "1", "BATERÍA"));
            KitSelectorRow(panel, fonts, audio, 48f, 196f, PanelWidth - 96f, 150f, compact: true);

            // 2. Volumen general (RF4)
            accent.Graphics.Add(SectionHeader(panel, fonts, 48f, 372f, "2", "VOLUMEN GENERAL"));
            VolumeRow(panel, fonts, audio, 48f, 420f, PanelWidth - 96f, accent);

            // Acciones
            float buttonWidth = (PanelWidth - 96f - 32f) / 2f;
            var back = PillButton(panel, audio, "BackToMenuButton", 48f, 528f, buttonWidth, 88f, "Volver al menú",
                fonts.SemiBold, 27f, MenuPalette.TextPrimary, Art("icon_back"), 28f, iconFirst: true, primary: false);
            var exit = PillButton(panel, audio, "ExitButton", 48f + buttonWidth + 32f, 528f, buttonWidth, 88f, "Salir",
                fonts.SemiBold, 27f, MenuPalette.TextPrimary, Art("icon_power"), 28f, iconFirst: true, primary: false);

            Text(At(UI("Hint", panel), 48f, 640f, PanelWidth - 96f, 34f),
                "Pulsa el botón de menú del mando izquierdo para abrir o cerrar este panel", fonts.Regular, 19f,
                MenuPalette.TextMuted, TextAlignmentOptions.Center);

            AddAccentTheme(canvas.gameObject, accent);
            Set(controller, "_panel", panel);
            Set(controller, "_group", canvas.GetComponent<CanvasGroup>());
            Set(controller, "_closeButton", close);
            Set(controller, "_backToMenuButton", back);
            Set(controller, "_exitButton", exit);
            Set(controller, "_audio", audio);
        }

        // Capturas desde el punto de teletransporte: kit acústico, kit electrónico y panel de sesión abierto.
        static void CapturePreviews(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var acousticKit = roots.FirstOrDefault(g => g.name == "Kit_Acoustic");
            var electronicKit = roots.FirstOrDefault(g => g.name == "Kit_Electronic");
            var hotspot = roots.FirstOrDefault(g => g.name == "TeleportHotspot");
            var systems = roots.FirstOrDefault(g => g.name == SystemsName);
            if (acousticKit == null || electronicKit == null || hotspot == null || systems == null) return;

            Vector3 eye = hotspot.transform.position + Vector3.up * 1.55f;
            Vector3 KitCenter(GameObject kit)
            {
                var renderers = kit.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return kit.transform.position;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                return bounds.center;
            }

            foreach (var (kit, name) in new[] { (acousticKit, "Studio_Acoustic"), (electronicKit, "Studio_Electronic") })
            {
                acousticKit.SetActive(kit == acousticKit);
                electronicKit.SetActive(kit == electronicKit);
                Vector3 look = KitCenter(kit) - eye;
                MainMenuSceneBuilder.CaptureView($"Logs/{name}.png", eye, Quaternion.LookRotation(look), 70f);

                if (kit != electronicKit) continue;

                // Panel abierto como lo ubicaría SessionPanelController: a la izquierda de la mirada.
                var panel = systems.GetComponentInChildren<SessionPanelController>(true).Panel;
                var group = panel.GetComponent<CanvasGroup>();
                panel.gameObject.SetActive(true);
                group.alpha = 1f;
                Vector3 forward = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
                Vector3 direction = Quaternion.Euler(0f, -40f, 0f) * forward;
                panel.SetPositionAndRotation(eye + direction * 0.9f + Vector3.down * 0.15f, Quaternion.LookRotation(direction));
                var view = Quaternion.LookRotation(Quaternion.Euler(0f, -22f, 0f) * forward) * Quaternion.Euler(8f, 0f, 0f);
                MainMenuSceneBuilder.CaptureView("Logs/Studio_SessionPanel.png", eye, view, 80f);
            }
        }
    }
}
