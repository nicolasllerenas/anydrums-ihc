using System;
using System.Collections.Generic;
using System.IO;
using AnyDrums.Menu;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Piezas de interfaz compartidas por el menú inicial y el panel de sesión, para que ambos se vean y se
    /// comporten igual: tarjetas de kit, fila de volumen, botones, títulos de sección y el rayo del Interaction SDK.
    /// Las coordenadas se dan desde la esquina superior izquierda del padre, en unidades del Canvas.
    /// </summary>
    public static class MenuUIKit
    {
        public const int UILayer = 5;
        const string FontsFolder = "Assets/AnyDrums/Fonts";
        // Plantilla que usa el asistente "Add Ray Interaction to Canvas" del Interaction SDK.
        const string RayCanvasTemplateGuid = "8369d93f7b6b99742bbea0649a41b7b1";

        const string FontCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡¿ÁÉÍÓÚÜÑáéíóúüñ·–—…";

        // Tonos sobre el panel ya mezclados y opacos. Con blancos semitransparentes el resultado cambia mucho entre
        // espacio de color Gamma y Lineal (Lineal los aclara); así se ven igual en ambos.
        public static readonly Color White06 = OnPanel(0.06f);
        public static readonly Color White08 = OnPanel(0.08f);
        public static readonly Color White12 = OnPanel(0.12f);
        public static readonly Color White16 = OnPanel(0.16f);
        public static readonly Color White18 = OnPanel(0.18f);
        public static readonly Color White26 = OnPanel(0.26f);

        static Color OnPanel(float whiteAmount)
        {
            var c = Color.Lerp(MenuPalette.Panel, Color.white, whiteAmount);
            c.a = 1f;
            return c;
        }

        public class Fonts
        {
            public TMP_FontAsset Regular, SemiBold, Bold;
        }

        /// <summary>Elementos que deben tomar el color del kit elegido.</summary>
        public class AccentTargets
        {
            public readonly List<Graphic> Graphics = new List<Graphic>();
            public readonly List<MenuSelectableFeedback> Buttons = new List<MenuSelectableFeedback>();
            public readonly List<Slider> Sliders = new List<Slider>();
        }

        // ---------- Recursos ----------

        public static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
            Debug.LogWarning("[AnyDrums] Se importaron los TMP Essential Resources. Vuelve a ejecutar el comando.");
            return false;
        }

        public static Fonts LoadFonts() => new Fonts
        {
            Regular = GetOrCreateFont("Montserrat-Regular"),
            SemiBold = GetOrCreateFont("Montserrat-SemiBold"),
            Bold = GetOrCreateFont("Montserrat-Bold"),
        };

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

        public static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(MenuArtGenerator.PathOf(name));

        public static T LoadByGuid<T>(string guid) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        // ---------- Canvas y sistema de eventos ----------

        /// <summary>Canvas en espacio de mundo con raycaster, CanvasGroup, audio del panel y rayo del Interaction SDK.</summary>
        public static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 size, float scale, Camera eventCamera,
            out MenuAudio audio)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(AudioSource)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            // Se dibuja después de los efectos transparentes del cuarto (conos de luz, brillos) para que no lo tapen.
            canvas.sortingOrder = 10;

            var source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.6f;
            source.minDistance = 2f;
            source.maxDistance = 20f;
            source.dopplerLevel = 0f;
            audio = go.AddComponent<MenuAudio>();

            AddRayInteraction(canvas);
            return canvas;
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

        /// <summary>
        /// El rig OVRCameraRigInteraction (el mismo que usa el equipo) está marcado como obsoleto en el SDK 201 y
        /// llena la consola de advertencias en cada Play. Funciona igual, así que se silencian esas advertencias.
        /// </summary>
        public static void SuppressDeprecatedPrefabWarnings(GameObject rig)
        {
            foreach (var deprecated in rig.GetComponentsInChildren<DeprecatedPrefab>(true))
            {
                var so = new SerializedObject(deprecated);
                var property = so.FindProperty("_supressWarning");
                if (property == null || property.boolValue) continue;
                property.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>PointableCanvasModule convierte los rayos del Interaction SDK en eventos de uGUI.</summary>
        public static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(PointableCanvasModule));
                return;
            }
            if (eventSystem.GetComponent<PointableCanvasModule>() == null)
                eventSystem.gameObject.AddComponent<PointableCanvasModule>();
        }

        // ---------- Piezas compuestas ----------

        public static void PanelBackground(RectTransform canvas, float radius)
        {
            Img(Stretch(UI("Panel", canvas)), Art("round_fill"), MenuPalette.Panel, Radius(radius));
            Img(Stretch(UI("Border", canvas)), Art("outline_panel"), White08, 2f * 44f / radius);
        }

        public static Graphic SectionHeader(RectTransform parent, Fonts fonts, float x, float y, string number, string label)
        {
            var badge = Img(At(UI($"Step{number}", parent), x, y, 34f, 34f), Art("circle_fill"), MenuPalette.AcousticAccent);
            Text(Stretch(UI("Number", badge.rectTransform)), number, fonts.Bold, 19f, MenuPalette.OnAccent, TextAlignmentOptions.Center);
            Text(At(UI($"Step{number}Label", parent), x + 48f, y, 700f, 34f), label, fonts.SemiBold, 20f,
                MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, spacing: 8f);
            return badge;
        }

        /// <summary>Las dos tarjetas de kit lado a lado, con el componente <see cref="KitSelector"/>.</summary>
        public static KitSelector KitSelectorRow(RectTransform parent, Fonts fonts, MenuAudio audio, float x, float y,
            float width, float height, bool compact)
        {
            const float gap = 32f;
            float cardWidth = (width - gap) / 2f;
            var row = At(UI("KitSelector", parent), x, y, width, height);
            var acoustic = KitCard(row, fonts, audio, 0f, 0f, cardWidth, height, DrumKitType.Acoustic, compact);
            var electronic = KitCard(row, fonts, audio, cardWidth + gap, 0f, cardWidth, height, DrumKitType.Electronic, compact);
            var selector = row.gameObject.AddComponent<KitSelector>();
            Set(selector, "_acousticCard", acoustic);
            Set(selector, "_electronicCard", electronic);
            Set(selector, "_audio", audio);
            acoustic.SetSelected(true, instant: true);
            electronic.SetSelected(false, instant: true);
            return selector;
        }

        static KitOptionCard KitCard(RectTransform parent, Fonts fonts, MenuAudio audio, float x, float y, float width,
            float height, DrumKitType kit, bool compact)
        {
            bool acoustic = kit == DrumKitType.Acoustic;
            Color accent = KitPalette.AccentFor(kit);
            float radius = compact ? 24f : 28f;
            var card = At(UI(acoustic ? "AcousticCard" : "ElectronicCard", parent), x, y, width, height);

            var glowRt = UI("Glow", card);
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-36f, -36f);
            glowRt.offsetMax = new Vector2(36f, 36f);
            var glow = Img(glowRt, Art("card_glow"), WithAlpha(accent, 0f), 28f / radius);

            var background = Img(Stretch(UI("Background", card)), Art("round_fill"), new Color32(0x1A, 0x1D, 0x25, 0xFF),
                Radius(radius), raycast: true);
            var border = Img(Stretch(UI("Border", card)), Art("outline_card"), KitOptionCard.BorderIdle, 2f * 28f / radius);

            float iconSize = compact ? 84f : 100f;
            float textX = compact ? 124f : 154f;
            float textWidth = width - textX - (compact ? 56f : 26f);
            var iconImage = Img(At(UI("Icon", card), compact ? 22f : 30f, (height - iconSize) / 2f, iconSize, iconSize),
                Art(acoustic ? "icon_acoustic" : "icon_electronic"), Color.white);

            string title = KitPalette.DisplayName(kit);
            var titleText = Text(At(UI("Title", card), textX, compact ? 30f : 32f, textWidth, compact ? 40f : 46f), title,
                fonts.SemiBold, compact ? 26f : 29f, MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 20f;
            titleText.fontSizeMax = compact ? 26f : 29f;

            if (!compact)
            {
                string description = acoustic
                    ? "Timbre cálido y natural, como un kit de estudio real."
                    : "Sonidos de caja de ritmos y pads, precisos y modernos.";
                var descriptionText = Text(At(UI("Description", card), textX, 84f, textWidth, 66f), description, fonts.Regular,
                    21f, MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft);
                descriptionText.enableWordWrapping = true;
            }

            string genres = acoustic ? "ROCK · JAZZ · POP" : "ELECTRÓNICA · HIP-HOP · EDM";
            var genresText = Text(At(UI("Genres", card), textX, compact ? 84f : 164f, textWidth, 28f), genres, fonts.SemiBold,
                compact ? 15f : 16f, accent, TextAlignmentOptions.MidlineLeft, spacing: compact ? 4f : 6f);

            float badgeSize = compact ? 34f : 40f;
            var badgeRt = At(UI("CheckBadge", card), width - badgeSize - 16f, 16f, badgeSize, badgeSize);
            Img(badgeRt, Art("circle_fill"), accent);
            var badgeGroup = badgeRt.gameObject.AddComponent<CanvasGroup>();
            badgeGroup.interactable = false;
            badgeGroup.blocksRaycasts = false;
            Img(Centered(UI("Check", badgeRt), badgeSize * 0.6f, badgeSize * 0.6f), Art("icon_check"), MenuPalette.OnAccent);

            // Ecualizador: se mueve mientras suena el golpe de prueba del kit.
            var bars = new RectTransform[4];
            float barsRight = width - 22f;
            float barsBottom = height - (compact ? 20f : 24f);
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = UI($"Bar{i}", card);
                bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(6f, compact ? 22f : 28f);
                bar.anchoredPosition = new Vector2(barsRight - (bars.Length - 1 - i) * 11f - 3f, -barsBottom);
                Img(bar, Art("round_fill"), WithAlpha(accent, 0f), Radius(3f));
                bars[i] = bar;
            }

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
            Set(option, "_bars", bars);

            // El golpe de prueba del kit reemplaza al clic, por eso esta tarjeta no lo reproduce.
            var feedback = card.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_hoverScale", 1.025f);
            Set(feedback, "_clickSound", false);
            Set(feedback, "_hapticAmplitude", 0.5f);
            return option;
        }

        /// <summary>Ícono de volumen, botón −, slider, botón + y porcentaje, con el componente <see cref="VolumeControl"/>.</summary>
        public static VolumeControl VolumeRow(RectTransform parent, Fonts fonts, MenuAudio audio, float x, float y, float width,
            AccentTargets accent)
        {
            const float rowHeight = 72f;
            var row = At(UI("VolumeControl", parent), x, y, width, rowHeight);
            var icon = Img(At(UI("VolumeIcon", row), 0f, 11f, 50f, 50f), Art("icon_volume_high"), MenuPalette.TextSecondary);
            var down = RoundIconButton(row, audio, "VolumeDown", 70f, 4f, Art("icon_minus"));
            var slider = VolumeSlider(row, audio, 158f, 0f, width - 390f, rowHeight, accent);
            var up = RoundIconButton(row, audio, "VolumeUp", width - 216f, 4f, Art("icon_plus"));
            var value = Text(At(UI("VolumeValue", row), width - 146f, 2f, 146f, 68f), "75 %", fonts.SemiBold, 36f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineRight);

            var control = row.gameObject.AddComponent<VolumeControl>();
            Set(control, "_slider", slider);
            Set(control, "_valueLabel", value);
            Set(control, "_icon", icon);
            Set(control, "_muteSprite", Art("icon_volume_mute"));
            Set(control, "_lowSprite", Art("icon_volume_low"));
            Set(control, "_highSprite", Art("icon_volume_high"));
            Set(control, "_downButton", down);
            Set(control, "_upButton", up);
            Set(control, "_release", slider.GetComponent<PointerUpNotifier>());
            Set(control, "_audio", audio);
            return control;
        }

        static Slider VolumeSlider(RectTransform parent, MenuAudio audio, float x, float y, float width, float height,
            AccentTargets accent)
        {
            const float handleSize = 44f;
            const float trackHeight = 16f;
            Color color = MenuPalette.AcousticAccent;
            var root = At(UI("VolumeSlider", parent), x, y, width, height);
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
            accent.Graphics.Add(Img(fill, Art("round_fill"), color, Radius(trackHeight / 2f)));

            var handleArea = UI("HandleArea", root);
            SetHorizontalBand(handleArea, handleSize / 2f, handleSize);
            var handle = UI("Handle", handleArea);
            handle.anchorMin = new Vector2(0.75f, 0f);
            handle.anchorMax = new Vector2(0.75f, 1f);
            handle.sizeDelta = new Vector2(handleSize, 0f);
            Img(Centered(UI("Shadow", handle), 100f, 100f), Art("glow"), new Color(0f, 0f, 0f, 0.6f));
            var knob = Img(Stretch(UI("Knob", handle)), Art("circle_fill"), Color.white);
            accent.Graphics.Add(Img(Centered(UI("Dot", handle), 14f, 14f), Art("circle_fill"), color));

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
            colors.highlightedColor = Color.Lerp(Color.white, color, 0.35f);
            colors.pressedColor = color;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            slider.colors = colors;
            accent.Sliders.Add(slider);

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

        public static Button RoundIconButton(RectTransform parent, MenuAudio audio, string name, float x, float y, Sprite icon,
            float size = 64f)
        {
            var root = At(UI(name, parent), x, y, size, size);
            var background = Img(root, Art("circle_fill"), White06, raycast: true);
            Img(Stretch(UI("Outline", root)), Art("circle_outline"), White16);
            Img(Centered(UI("Icon", root), size * 0.4f, size * 0.4f), icon, MenuPalette.TextPrimary);

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

        /// <summary>Botón tipo píldora con ícono. Si <paramref name="summary"/> no es null, agrega una segunda línea de texto.</summary>
        public static Button PillButton(RectTransform parent, MenuAudio audio, string name, float x, float y, float width,
            float height, string label, TMP_FontAsset font, float fontSize, Color labelColor, Sprite icon, float iconSize,
            bool iconFirst, bool primary, Fonts fonts = null, string summary = null)
        {
            var root = At(UI(name, parent), x, y, width, height);
            Color background = primary ? MenuPalette.AcousticAccent : White06;
            var backgroundImage = Img(root, Art("round_fill"), background, Radius(height / 2f), raycast: true);
            if (!primary) Img(Stretch(UI("Outline", root)), Art("outline_pill"), White18, 2f * 50f / (height / 2f));

            var content = Stretch(UI("Content", root));
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform textBlock;
            if (summary != null && fonts != null)
            {
                textBlock = UI("Text", content);
                textBlock.sizeDelta = new Vector2(0f, height * 0.8f);
                var column = textBlock.gameObject.AddComponent<VerticalLayoutGroup>();
                column.childAlignment = TextAnchor.MiddleCenter;
                column.spacing = 0f;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = false;
                column.childForceExpandHeight = false;
                Text(UI("Label", textBlock), label, font, fontSize, labelColor, TextAlignmentOptions.Midline);
                Text(UI("Summary", textBlock), summary, fonts.SemiBold, fontSize * 0.64f, labelColor,
                    TextAlignmentOptions.Midline);
            }
            else
            {
                textBlock = UI("Label", content);
                textBlock.sizeDelta = new Vector2(0f, height * 0.6f);
                Text(textBlock, label, font, fontSize, labelColor, TextAlignmentOptions.Midline);
            }

            var iconRt = UI("Icon", content);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            Img(iconRt, icon, labelColor);
            var iconLayout = iconRt.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
            if (iconFirst) iconRt.SetAsFirstSibling();

            var button = root.gameObject.AddComponent<Button>();
            ConfigureSelectable(button, backgroundImage);
            var feedback = root.gameObject.AddComponent<MenuSelectableFeedback>();
            Set(feedback, "_audio", audio);
            Set(feedback, "_hoverScale", 1.03f);
            Set(feedback, "_hapticAmplitude", 0.5f);
            Set(feedback, "_tintTarget", backgroundImage);
            if (primary)
            {
                feedback.SetBaseColor(background);
            }
            else
            {
                Set(feedback, "_normalColor", White06);
                Set(feedback, "_hoverColor", White16);
                Set(feedback, "_pressedColor", White26);
            }
            return button;
        }

        public static AccentTheme AddAccentTheme(GameObject target, AccentTargets accent, Renderer floorGlow = null,
            TMP_Text title = null)
        {
            var theme = target.AddComponent<AccentTheme>();
            Set(theme, "_graphics", accent.Graphics.ConvertAll(g => (Object)g).ToArray());
            Set(theme, "_accentButtons", accent.Buttons.ConvertAll(b => (Object)b).ToArray());
            Set(theme, "_sliders", accent.Sliders.ConvertAll(s => (Object)s).ToArray());
            if (floorGlow != null) Set(theme, "_floorGlow", floorGlow);
            if (title != null) Set(theme, "_title", title);
            else Set(theme, "_titleFormat", "");
            return theme;
        }

        public static void ConfigureSelectable(Selectable selectable, Graphic target)
        {
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = target;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // ---------- Utilidades de UI ----------

        public static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Posiciona con coordenadas desde la esquina superior izquierda del padre (pivote al centro).</summary>
        public static RectTransform At(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x + width / 2f, -(y + height / 2f));
            return rt;
        }

        public static RectTransform Centered(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
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

        /// <summary>pixelsPerUnitMultiplier para que la forma redondeada base tenga el radio pedido.</summary>
        public static float Radius(float radius) => MenuArtGenerator.BaseRadius / radius;

        public static Image Img(RectTransform rt, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f, bool raycast = false)
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

        public static TextMeshProUGUI Text(RectTransform rt, string text, TMP_FontAsset font, float size, Color color,
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

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Transform FindDeep(Transform parent, string name)
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
        public static void Set(Object target, string field, object value)
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
