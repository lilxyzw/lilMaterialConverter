#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

// 1.0.1
// - Fixed conversion of transparent material and MatCap
// - Stencil support
// - Improved conversion accuracy between rim light, outline and AOMap

// 1.0.2
// - Improved conversion of HighColor
// - Support for Built-in Light Direction
// - Support for UTS3

// 1.0.3
// - Improved conversion of AOMap (lilToon 1.2.11)
// - Add language (Japanese)

// 1.0.4
// - Support for lilToon 1.3.0 or later
// - Remove unused properties

namespace lilToon
{
    public static class lilMaterialConverter
    {
        private static readonly string[] TEXT_OK = new[] {"OK", "OK"};
        private static readonly string[] TEXT_CANCEL = new[] {"Cancel", "キャンセル"};
        private static readonly string[] TEXT_YES = new[] {"Yes", "はい"};
        private static readonly string[] TEXT_NO = new[] {"No", "いいえ"};
        private static readonly string[] TEXT_MESSAGE_BEFORE_CONVERT = new[] {
            "Are you sure you want to convert the material?\r\n(It is recommended to make a backup before conversion)",
            "マテリアルの変換を実行しますか？\r\n（変換前にマテリアルのバックアップを取っておくことをオススメします）"
        };
        private static readonly string[] TEXT_MESSAGE_CONVERT_AO_MASK = new[] {
            "Do you want to convert AO Mask?",
            "AOマスクを変換しますか？"
        };
        private static readonly string[] TEXT_MESSAGE_ENABLE_SHADER_SETTING = new[] {
            "Do you want to enable the missing features in shader settings?",
            "不足しているシェーダー設定を有効化して見た目を再現しますか？"
        };
        private static readonly string[] TEXT_MESSAGE_UNSUPPORTED_SHADER = new[] {
            "Skipped conversion of materials using unsupported shaders.",
            "非対応のシェーダーを使用しているマテリアルの変換をスキップしました。"
        };
        private static readonly string[] TEXT_MESSAGE_COMPLETE = new[] {
            "Complete!",
            "完了しました"
        };
        private const string TEXT_DIALOG_TITLE = "Convert material to lilToon";

        private static int lang = 0;

        [MenuItem("Assets/lilToon/[Material] Convert material to lilToon", false, 1120)]
        private static void ConvertMaterial()
        {
            lang = Application.systemLanguage == SystemLanguage.Japanese ? 1 : 0;
            if(Selection.objects.Length == 0) return;
            bool shouldConvert = EditorUtility.DisplayDialog(TEXT_DIALOG_TITLE, TEXT_MESSAGE_BEFORE_CONVERT[lang], TEXT_OK[lang], TEXT_CANCEL[lang]);
            if(!shouldConvert) return;
            var unsupportedMaterials = new List<string>();
            for(int i = 0; i < Selection.objects.Length; i++)
            {
                if(Selection.objects[i] is Material material)
                {
                    lilToonConvertProperties d = new lilToonConvertProperties(){_RenderQueue = -1};
                    if(IsUTS(material) || IsUTS3(material))
                    {
                        GetUTSProperties(ref d, material);
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(material);
                        if(!string.IsNullOrEmpty(path)) unsupportedMaterials.Add(path);
                        continue;
                    }

                    SetPropertiesToMaterial(d, ref material);
                    RemoveShaderKeywords(material);
                    RemoveUnusedProperties(material);
                }
            }
            if(unsupportedMaterials.Count > 0) EditorUtility.DisplayDialog(TEXT_DIALOG_TITLE, TEXT_MESSAGE_UNSUPPORTED_SHADER[lang] + "\r\n" + string.Join("\r\n", unsupportedMaterials), TEXT_OK[lang]);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(TEXT_DIALOG_TITLE, TEXT_MESSAGE_COMPLETE[lang], TEXT_OK[lang]);
        }

        [MenuItem("Assets/lilToon/[Material] Convert material to lilToon", true, 1120)]
        private static bool CheckMaterialFormat()
        {
            if(Selection.activeObject == null) return false;
            return AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
        }

        private struct lilTex
        {
            public Texture tex;
            public Vector2 offset;
            public Vector2 scale;
        };

        private struct lilToonConvertProperties
        {
            public int _RenderQueue;
            public bool isOutline;
            public bool isCutout;
            public bool isTransparent;

            // Main
            public lilTex? _MainTex;
            public Color? _MainColor;
            public float? _Cutoff;
            public float? _Cull;

            // AlphaMask
            public float? _AlphaMaskMode;
            public lilTex? _AlphaMask;
            public float? _AlphaMaskScale;
            public float? _AlphaMaskValue;

            // Lighting
            public float? _LightMinLimit;
            public float? _AsUnlit;
            public Vector4? _LightDirectionOverride;

            // Shadow
            public float? _UseShadow;
            public float? _ShadowReceive;
            public float? _Shadow2ndReceive;
            public float? _Shadow3rdReceive;
            public float? _ShadowStrength;
            public lilTex? _ShadowAO;
            public lilTex? _Shadow2ndAO;
            public Vector4? _ShadowAOShift;
            public float? _ShadowPostAO;
            public lilTex? _ShadowStrengthMask;
            public Color? _ShadowColor;
            public lilTex? _ShadowColorTex;
            public float? _ShadowNormalStrength;
            public float? _ShadowBorderMax;
            public float? _ShadowBorderMin;
            public lilTex? _ShadowBlurMask;
            public Color? _Shadow2ndColor;
            public lilTex? _Shadow2ndColorTex;
            public float? _Shadow2ndNormalStrength;
            public float? _Shadow2ndBorderMax;
            public float? _Shadow2ndBorderMin;
            public float? _ShadowMainStrength;
            public float? _ShadowEnvStrength;
            public Color? _ShadowBorderColor;
            public float? _ShadowBorderRange;

            // Normal
            public float? _UseBumpMap;
            public lilTex? _BumpMap;
            public float? _BumpScale;

            public float? _UseBump2ndMap;
            public lilTex? _Bump2ndMap;
            public float? _Bump2ndScale;
            public lilTex? _Bump2ndMask;

            // Reflection
            public float? _UseReflection;
            public float? _Smoothness;
            public lilTex? _SmoothnessTex;
            public float? _Metallic;
            public lilTex? _MetallicGlossMap;
            public float? _Reflectance;
            public float? _ApplySpecular;
            public float? _ApplySpecularFA;
            public float? _SpecularToon;
            public float? _SpecularNormalStrength;
            public float? _SpecularBorderMax;
            public float? _SpecularBorderMin;
            public float? _ApplyReflection;
            public float? _ReflectionNormalStrength;
            public Color? _ReflectionColor;
            public lilTex? _ReflectionColorTex;
            public float? _ReflectionApplyTransparency;

            // MatCap
            public float? _UseMatCap;
            public Color? _MatCapColor;
            public lilTex? _MatCapTex;
            public Vector4? _MatCapBlendUV1;
            public float? _MatCapZRotCancel;
            public float? _MatCapPerspective;
            public float? _MatCapVRParallaxStrength;
            public float? _MatCapBlend;
            public lilTex? _MatCapBlendMask;
            public float? _MatCapEnableLighting;
            public float? _MatCapShadowMask;
            public float? _MatCapBackfaceMask;
            public float? _MatCapLod;
            public float? _MatCapBlendMode;
            public float? _MatCapApplyTransparency;
            public float? _MatCapNormalStrength;
            public float? _MatCapCustomNormal;
            public lilTex? _MatCapBumpMap;
            public float? _MatCapBumpScale;

            public float? _UseMatCap2nd;
            public Color? _MatCap2ndColor;
            public lilTex? _MatCap2ndTex;
            public Vector4? _MatCap2ndBlendUV1;
            public float? _MatCap2ndZRotCancel;
            public float? _MatCap2ndPerspective;
            public float? _MatCap2ndVRParallaxStrength;
            public float? _MatCap2ndBlend;
            public lilTex? _MatCap2ndBlendMask;
            public float? _MatCap2ndEnableLighting;
            public float? _MatCap2ndShadowMask;
            public float? _MatCap2ndBackfaceMask;
            public float? _MatCap2ndLod;
            public float? _MatCap2ndBlendMode;
            public float? _MatCap2ndApplyTransparency;
            public float? _MatCap2ndNormalStrength;
            public float? _MatCap2ndCustomNormal;
            public lilTex? _MatCap2ndBumpMap;
            public float? _MatCap2ndBumpScale;

            // Rim
            public float? _UseRim;
            public Color? _RimColor;
            public lilTex? _RimColorTex;
            public float? _RimNormalStrength;
            public float? _RimBorderMin;
            public float? _RimBorderMax;
            public float? _RimFresnelPower;
            public float? _RimEnableLighting;
            public float? _RimShadowMask;
            public float? _RimBackfaceMask;
            public float? _RimApplyTransparency;
            public float? _RimDirStrength;
            public float? _RimDirRange;
            public float? _RimIndirRange;
            public Color? _RimIndirColor;
            public float? _RimIndirBorderMin;
            public float? _RimIndirBorderMax;

            // Emission
            public float? _UseEmission;
            public Color? _EmissionColor;
            public lilTex? _EmissionMap;
            public Vector4? _EmissionMap_ScrollRotate;
            public float? _EmissionMap_UVMode;
            public float? _EmissionBlend;
            public lilTex? _EmissionBlendMask;
            public Vector4? _EmissionBlendMask_ScrollRotate;
            public Vector4? _EmissionBlink;
            public float? _EmissionUseGrad;
            public lilTex? _EmissionGradTex;
            public float? _EmissionGradSpeed;
            public float? _EmissionParallaxDepth;
            public float? _EmissionFluorescence;

            // Outline
            public Color? _OutlineColor;
            public lilTex? _OutlineTex;
            public Vector4? _OutlineTex_ScrollRotate;
            public Vector4? _OutlineTexHSVG;
            public float? _OutlineWidth;
            public lilTex? _OutlineWidthMask;
            public float? _OutlineFixWidth;
            public float? _OutlineVertexR2Width;
            public lilTex? _OutlineVectorTex;
            public float? _OutlineEnableLighting;

            public float? _StencilRef;
            public float? _StencilComp;
            public float? _StencilPass;
            public float? _StencilFail;
            public float? _OutlineStencilRef;
            public float? _OutlineStencilComp;
            public float? _OutlineStencilPass;
            public float? _OutlineStencilFail;
        };

        //----------------------------------------------------------------------------------------------------------------------
        // Remove Unused Properties
        public static void RemoveShaderKeywords(Material material)
        {
            foreach(string keyword in material.shaderKeywords)
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void RemoveUnusedProperties(Material material)
        {
            // https://light11.hatenadiary.com/entry/2018/12/04/224253
            var so = new SerializedObject(material);
            so.Update();
            var savedProps = so.FindProperty("m_SavedProperties");

            var texs = savedProps.FindPropertyRelative("m_TexEnvs");
            DeleteUnused(ref texs, material);

            var floats = savedProps.FindPropertyRelative("m_Floats");
            DeleteUnused(ref floats, material);

            var colors = savedProps.FindPropertyRelative("m_Colors");
            DeleteUnused(ref colors, material);

            so.ApplyModifiedProperties();
        }

        private static void DeleteUnused(ref SerializedProperty props, Material material)
        {
            for(int i = props.arraySize - 1; i >= 0; i--)
            {
                if(!material.HasProperty(props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue))
                {
                    props.DeleteArrayElementAtIndex(i);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        // Material
        private static lilTex? GetMaterialTexture(Material material, string name)
        {
            if(!material.HasProperty(name)) return null;
            lilTex outProp;
            outProp.tex = material.GetTexture(name);
            outProp.offset = material.GetTextureOffset(name);
            outProp.scale = material.GetTextureScale(name);
            return outProp;
        }

        private static Color? GetMaterialColor(Material material, string name)
        {
            if(!material.HasProperty(name)) return null;
            return material.GetColor(name);
        }

        private static Color? GetMaterialColorNA(Material material, string name)
        {
            if(!material.HasProperty(name)) return null;
            Color outColor = material.GetColor(name);
            return new Color(outColor.r, outColor.g, outColor.b, 1.0f);
        }

        private static float? GetMaterialFloat(Material material, string name)
        {
            if(!material.HasProperty(name)) return null;
            return material.GetFloat(name);
        }

        private static bool GetMaterialToggle(Material material, string name)
        {
            return material.HasProperty(name) && (material.GetFloat(name) == 1.0f);
        }

        private static bool IsColorWhite(Color color)
        {
            return color.r == 1.0f && color.g == 1.0f && color.b == 1.0f;
        }

        private static bool IsColorWhite(Color? color)
        {
            return color == null || IsColorWhite((Color)color);
        }

        private static bool IsColorBlack(Color color)
        {
            return color.r == 0.0f && color.g == 0.0f && color.b == 0.0f;
        }

        private static bool IsColorBlack(Color? color)
        {
            return color == null || IsColorBlack((Color)color);
        }

        private static Color ColorDiv(Color a, Color b)
        {
            return new Color(
                a.r / b.r,
                a.g / b.g,
                a.b / b.b,
                a.a / b.a
            );
        }

        private static void SetScaleAndOffset(ref lilTex? origTex, Vector2 Scale, Vector2 Offset)
        {
            origTex = new lilTex
            {
                tex = ((lilTex)origTex).tex,
                scale = Scale,
                offset = Offset
            };
        }

        private static void SetPropertiesToMaterial(lilToonConvertProperties d, ref Material material)
        {
            string shaderName = "lilToon";
            if(d.isCutout) shaderName += "Cutout";
            if(d.isTransparent) shaderName += "Transparent";
            if(d.isOutline) shaderName += "Outline";
            if(shaderName != "lilToon") shaderName = "Hidden/" + shaderName;
            Shader shader = Shader.Find(shaderName);
            if(shader != null) material.shader = shader;

            if(d.isTransparent)
            {
                material.SetFloat("_SrcBlend", 1.0f);
                material.SetFloat("_DstBlend", 10.0f);
                material.SetFloat("_SrcBlendAlpha", 1.0f);
                material.SetFloat("_DstBlendAlpha", 10.0f);
                material.SetFloat("_BlendOp", 0.0f);
                material.SetFloat("_BlendOpAlpha", 0.0f);
                material.SetFloat("_SrcBlendFA", 1.0f);
                material.SetFloat("_DstBlendFA", 1.0f);
                material.SetFloat("_SrcBlendAlphaFA", 0.0f);
                material.SetFloat("_DstBlendAlphaFA", 1.0f);
                material.SetFloat("_BlendOpFA", 4.0f);
                material.SetFloat("_BlendOpAlphaFA", 4.0f);
            }
            else
            {
                material.SetFloat("_SrcBlend", 1.0f);
                material.SetFloat("_DstBlend", 0.0f);
                material.SetFloat("_SrcBlendAlpha", 1.0f);
                material.SetFloat("_DstBlendAlpha", 10.0f);
                material.SetFloat("_BlendOp", 0.0f);
                material.SetFloat("_BlendOpAlpha", 0.0f);
                material.SetFloat("_SrcBlendFA", 1.0f);
                material.SetFloat("_DstBlendFA", 1.0f);
                material.SetFloat("_SrcBlendAlphaFA", 0.0f);
                material.SetFloat("_DstBlendAlphaFA", 1.0f);
                material.SetFloat("_BlendOpFA", 4.0f);
                material.SetFloat("_BlendOpAlphaFA", 4.0f);
            }

            if(d.isCutout)
            {
                material.SetFloat("_AlphaToMask", 1.0f);
            }
            else
            {
                material.SetFloat("_AlphaToMask", 0.0f);
            }

            SetProperty(d._MainTex, "_MainTex", ref material);
            SetProperty(d._MainColor, "_MainColor", ref material);
            SetProperty(d._Cutoff, "_Cutoff", ref material);
            SetProperty(d._Cull, "_Cull", ref material);

            SetProperty(d._AlphaMaskMode, "_AlphaMaskMode", ref material);
            SetProperty(d._AlphaMask, "_AlphaMask", ref material);
            SetProperty(d._AlphaMaskScale, "_AlphaMaskScale", ref material);
            SetProperty(d._AlphaMaskValue, "_AlphaMaskValue", ref material);

            SetProperty(d._LightMinLimit, "_LightMinLimit", ref material);
            SetProperty(d._AsUnlit, "_AsUnlit", ref material);
            SetProperty(d._LightDirectionOverride, "_LightDirectionOverride", ref material);

            SetProperty(d._UseShadow, "_UseShadow", ref material);
            SetProperty(d._ShadowReceive, "_ShadowReceive", ref material);
            SetProperty(d._Shadow2ndReceive, "_Shadow2ndReceive", ref material);
            SetProperty(d._Shadow3rdReceive, "_Shadow3rdReceive", ref material);
            SetProperty(d._ShadowStrength, "_ShadowStrength", ref material);
            SetProperty(d._ShadowAOShift, "_ShadowAOShift", ref material);
            SetProperty(d._ShadowPostAO, "_ShadowPostAO", ref material);
            SetProperty(d._ShadowStrengthMask, "_ShadowStrengthMask", ref material);
            SetProperty(d._ShadowColor, "_ShadowColor", ref material);
            SetProperty(d._ShadowColorTex, "_ShadowColorTex", ref material);
            SetProperty(d._ShadowNormalStrength, "_ShadowNormalStrength", ref material);
            SetBorderAndBlur(d._ShadowBorderMax, d._ShadowBorderMin, "_ShadowBorder", "_ShadowBlur", ref material);
            SetProperty(d._ShadowBlurMask, "_ShadowBlurMask", ref material);
            SetProperty(d._Shadow2ndColor, "_Shadow2ndColor", ref material);
            SetProperty(d._Shadow2ndColorTex, "_Shadow2ndColorTex", ref material);
            SetProperty(d._Shadow2ndNormalStrength, "_Shadow2ndNormalStrength", ref material);
            SetBorderAndBlur(d._Shadow2ndBorderMax, d._Shadow2ndBorderMin, "_Shadow2ndBorder", "_Shadow2ndBlur", ref material);
            SetProperty(d._ShadowMainStrength, "_ShadowMainStrength", ref material);
            SetProperty(d._ShadowEnvStrength, "_ShadowEnvStrength", ref material);
            SetProperty(d._ShadowBorderColor, "_ShadowBorderColor", ref material);
            SetProperty(d._ShadowBorderRange, "_ShadowBorderRange", ref material);
            SetAOMask(d._ShadowAO, d._Shadow2ndAO, "_ShadowBorderMask", ref material);

            SetProperty(d._UseBumpMap, "_UseBumpMap", ref material);
            SetProperty(d._BumpMap, "_BumpMap", ref material);
            SetProperty(d._BumpScale, "_BumpScale", ref material);

            SetProperty(d._UseBump2ndMap, "_UseBump2ndMap", ref material);
            SetProperty(d._Bump2ndMap, "_Bump2ndMap", ref material);
            SetProperty(d._Bump2ndScale, "_Bump2ndScale", ref material);
            SetProperty(d._Bump2ndMask, "_Bump2ndMask", ref material);

            SetProperty(d._UseReflection, "_UseReflection", ref material);
            SetProperty(d._Smoothness, "_Smoothness", ref material);
            SetProperty(d._SmoothnessTex, "_SmoothnessTex", ref material);
            SetProperty(d._Metallic, "_Metallic", ref material);
            SetProperty(d._MetallicGlossMap, "_MetallicGlossMap", ref material);
            SetProperty(d._Reflectance, "_Reflectance", ref material);
            SetProperty(d._ApplySpecular, "_ApplySpecular", ref material);
            SetProperty(d._ApplySpecularFA, "_ApplySpecularFA", ref material);
            SetProperty(d._SpecularToon, "_SpecularToon", ref material);
            SetProperty(d._SpecularNormalStrength, "_SpecularNormalStrength", ref material);
            SetBorderAndBlur(d._SpecularBorderMax, d._SpecularBorderMin, "_SpecularBorder", "_SpecularBlur", ref material);
            SetProperty(d._ApplyReflection, "_ApplyReflection", ref material);
            SetProperty(d._ReflectionNormalStrength, "_ReflectionNormalStrength", ref material);
            SetProperty(d._ReflectionColor, "_ReflectionColor", ref material);
            SetProperty(d._ReflectionColorTex, "_ReflectionColorTex", ref material);
            SetProperty(d._ReflectionApplyTransparency, "_ReflectionApplyTransparency", ref material);

            SetProperty(d._UseMatCap, "_UseMatCap", ref material);
            SetProperty(d._MatCapColor, "_MatCapColor", ref material);
            SetProperty(d._MatCapTex, "_MatCapTex", ref material);
            SetProperty(d._MatCapBlendUV1, "_MatCapBlendUV1", ref material);
            SetProperty(d._MatCapZRotCancel, "_MatCapZRotCancel", ref material);
            SetProperty(d._MatCapPerspective, "_MatCapPerspective", ref material);
            SetProperty(d._MatCapVRParallaxStrength, "_MatCapVRParallaxStrength", ref material);
            SetProperty(d._MatCapBlend, "_MatCapBlend", ref material);
            SetProperty(d._MatCapBlendMask, "_MatCapBlendMask", ref material);
            SetProperty(d._MatCapEnableLighting, "_MatCapEnableLighting", ref material);
            SetProperty(d._MatCapShadowMask, "_MatCapShadowMask", ref material);
            SetProperty(d._MatCapBackfaceMask, "_MatCapBackfaceMask", ref material);
            SetProperty(d._MatCapLod, "_MatCapLod", ref material);
            SetProperty(d._MatCapBlendMode, "_MatCapBlendMode", ref material);
            SetProperty(d._MatCapApplyTransparency, "_MatCapApplyTransparency", ref material);
            SetProperty(d._MatCapNormalStrength, "_MatCapNormalStrength", ref material);
            SetProperty(d._MatCapCustomNormal, "_MatCapCustomNormal", ref material);
            SetProperty(d._MatCapBumpMap, "_MatCapBumpMap", ref material);
            SetProperty(d._MatCapBumpScale, "_MatCapBumpScale", ref material);

            SetProperty(d._UseMatCap2nd, "_UseMatCap2nd", ref material);
            SetProperty(d._MatCap2ndColor, "_MatCap2ndColor", ref material);
            SetProperty(d._MatCap2ndTex, "_MatCap2ndTex", ref material);
            SetProperty(d._MatCap2ndBlendUV1, "_MatCap2ndBlendUV1", ref material);
            SetProperty(d._MatCap2ndZRotCancel, "_MatCap2ndZRotCancel", ref material);
            SetProperty(d._MatCap2ndPerspective, "_MatCap2ndPerspective", ref material);
            SetProperty(d._MatCap2ndVRParallaxStrength, "_MatCap2ndVRParallaxStrength", ref material);
            SetProperty(d._MatCap2ndBlend, "_MatCap2ndBlend", ref material);
            SetProperty(d._MatCap2ndBlendMask, "_MatCap2ndBlendMask", ref material);
            SetProperty(d._MatCap2ndEnableLighting, "_MatCap2ndEnableLighting", ref material);
            SetProperty(d._MatCap2ndShadowMask, "_MatCap2ndShadowMask", ref material);
            SetProperty(d._MatCap2ndBackfaceMask, "_MatCap2ndBackfaceMask", ref material);
            SetProperty(d._MatCap2ndLod, "_MatCap2ndLod", ref material);
            SetProperty(d._MatCap2ndBlendMode, "_MatCap2ndBlendMode", ref material);
            SetProperty(d._MatCap2ndApplyTransparency, "_MatCap2ndApplyTransparency", ref material);
            SetProperty(d._MatCap2ndNormalStrength, "_MatCap2ndNormalStrength", ref material);
            SetProperty(d._MatCap2ndCustomNormal, "_MatCap2ndCustomNormal", ref material);
            SetProperty(d._MatCap2ndBumpMap, "_MatCap2ndBumpMap", ref material);
            SetProperty(d._MatCap2ndBumpScale, "_MatCap2ndBumpScale", ref material);

            SetProperty(d._UseRim, "_UseRim", ref material);
            SetProperty(d._RimColor, "_RimColor", ref material);
            SetProperty(d._RimColorTex, "_RimColorTex", ref material);
            SetProperty(d._RimNormalStrength, "_RimNormalStrength", ref material);
            SetBorderAndBlur(d._RimBorderMax, d._RimBorderMin, "_RimBorder", "_RimBlur", ref material);
            SetProperty(d._RimFresnelPower, "_RimFresnelPower", ref material);
            SetProperty(d._RimEnableLighting, "_RimEnableLighting", ref material);
            SetProperty(d._RimShadowMask, "_RimShadowMask", ref material);
            SetProperty(d._RimBackfaceMask, "_RimBackfaceMask", ref material);
            SetProperty(d._RimApplyTransparency, "_RimApplyTransparency", ref material);
            SetProperty(d._RimDirStrength, "_RimDirStrength", ref material);
            SetProperty(d._RimDirRange, "_RimDirRange", ref material);
            SetProperty(d._RimIndirRange, "_RimIndirRange", ref material);
            SetProperty(d._RimIndirColor, "_RimIndirColor", ref material);
            SetBorderAndBlur(d._RimIndirBorderMax, d._RimIndirBorderMin, "_RimIndirBorder", "_RimIndirBlur", ref material);

            SetProperty(d._UseEmission, "_UseEmission", ref material);
            SetProperty(d._EmissionColor, "_EmissionColor", ref material);
            SetProperty(d._EmissionMap, "_EmissionMap", ref material);
            SetProperty(d._EmissionMap_ScrollRotate, "_EmissionMap_ScrollRotate", ref material);
            SetProperty(d._EmissionMap_UVMode, "_EmissionMap_UVMode", ref material);
            SetProperty(d._EmissionBlend, "_EmissionBlend", ref material);
            SetProperty(d._EmissionBlendMask, "_EmissionBlendMask", ref material);
            SetProperty(d._EmissionBlendMask_ScrollRotate, "_EmissionBlendMask_ScrollRotate", ref material);
            SetProperty(d._EmissionBlink, "_EmissionBlink", ref material);
            SetProperty(d._EmissionUseGrad, "_EmissionUseGrad", ref material);
            SetProperty(d._EmissionGradTex, "_EmissionGradTex", ref material);
            SetProperty(d._EmissionGradSpeed, "_EmissionGradSpeed", ref material);
            SetProperty(d._EmissionParallaxDepth, "_EmissionParallaxDepth", ref material);
            SetProperty(d._EmissionFluorescence, "_EmissionFluorescence", ref material);

            SetProperty(d._OutlineColor, "_OutlineColor", ref material);
            SetProperty(d._OutlineTex, "_OutlineTex", ref material);
            SetProperty(d._OutlineTex_ScrollRotate, "_OutlineTex_ScrollRotate", ref material);
            SetProperty(d._OutlineTexHSVG, "_OutlineTexHSVG", ref material);
            SetProperty(d._OutlineWidth, "_OutlineWidth", ref material);
            SetProperty(d._OutlineWidthMask, "_OutlineWidthMask", ref material);
            SetProperty(d._OutlineFixWidth, "_OutlineFixWidth", ref material);
            SetProperty(d._OutlineVertexR2Width, "_OutlineVertexR2Width", ref material);
            SetProperty(d._OutlineVectorTex, "_OutlineVectorTex", ref material);
            SetProperty(d._OutlineEnableLighting, "_OutlineEnableLighting", ref material);

            SetProperty(d._StencilRef, "_StencilRef", ref material);
            SetProperty(d._StencilComp, "_StencilComp", ref material);
            SetProperty(d._StencilPass, "_StencilPass", ref material);
            SetProperty(d._StencilFail, "_StencilFail", ref material);
            SetProperty(d._OutlineStencilRef, "_OutlineStencilRef", ref material);
            SetProperty(d._OutlineStencilComp, "_OutlineStencilComp", ref material);
            SetProperty(d._OutlineStencilPass, "_OutlineStencilPass", ref material);
            SetProperty(d._OutlineStencilFail, "_OutlineStencilFail", ref material);

            material.renderQueue = d._RenderQueue;
        }

        private static void SetProperty(float? d, string name, ref Material material)
        {
            if(d == null) return;
            material.SetFloat(name, (float)d);
        }

        private static void SetProperty(Vector4? d, string name, ref Material material)
        {
            if(d == null) return;
            material.SetVector(name, (Vector4)d);
        }

        private static void SetProperty(Color? d, string name, ref Material material)
        {
            if(d == null) return;
            material.SetColor(name, (Color)d);
        }

        private static void SetProperty(lilTex? d, string name, ref Material material)
        {
            if(d == null) return;
            material.SetTexture(name, ((lilTex)d).tex);
            material.SetTextureOffset(name, ((lilTex)d).offset);
            material.SetTextureScale(name, ((lilTex)d).scale);
        }

        private static void SetBorderAndBlur(float? max, float? min, string borderName, string blurName, ref Material material)
        {
            if(max == null || min == null) return;
            float border = ((float)max + (float)min) * 0.5f;
            float blur = (float)max - (float)min;
            material.SetFloat(borderName, border);
            material.SetFloat(blurName, blur);
        }

        private static void SetAOMask(lilTex? ao1st, lilTex? ao2nd, string name, ref Material material)
        {
            if(ao1st == null && ao2nd == null) return;
            if(ao1st != null && ao2nd != null && (((lilTex)ao1st).tex == ((lilTex)ao2nd).tex))
            {
                material.SetTexture(name, ((lilTex)ao1st).tex);
                return;
            }
            else
            {
                if((ao1st != null && ((lilTex)ao1st).tex != null) && !(ao2nd != null && ((lilTex)ao2nd).tex != null))
                {
                    material.SetTexture(name, ((lilTex)ao1st).tex);
                    return;
                }

                if(!(ao1st != null && ((lilTex)ao1st).tex != null) && (ao2nd != null && ((lilTex)ao2nd).tex != null))
                {
                    material.SetTexture(name, ((lilTex)ao2nd).tex);
                    return;
                }

                bool shouldSave = EditorUtility.DisplayDialog(TEXT_DIALOG_TITLE, TEXT_MESSAGE_CONVERT_AO_MASK[lang], TEXT_YES[lang], TEXT_NO[lang]);
                if(shouldSave)
                {
                    Texture2D srcTexture = new Texture2D(2, 2, TextureFormat.ARGB32, true, true);
                    Texture2D ao1stTexture = new Texture2D(2, 2, TextureFormat.ARGB32, true, true);
                    Texture2D ao2ndTexture = new Texture2D(2, 2, TextureFormat.ARGB32, true, true);
                    Material bakeMaterial = new Material(Shader.Find("Hidden/ltsother_baker"));
                    bakeMaterial.EnableKeyword("_TEXTURE_PACKING");

                    // Load
                    string path = "";
                    if(ao1st != null && ((lilTex)ao1st).tex != null)
                    {
                        path = AssetDatabase.GetAssetPath(((lilTex)ao1st).tex);
                        if(!string.IsNullOrEmpty(path))
                        {
                            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
                            ao1stTexture.LoadImage(bytes);
                            bakeMaterial.SetTexture("_PackingTexture1", ao1stTexture);
                            bakeMaterial.SetFloat("_PackingChannel1", 0.0f);
                            srcTexture = ao1stTexture;
                        }
                    }
                    if(ao2nd != null && ((lilTex)ao2nd).tex != null)
                    {
                        string path2 = AssetDatabase.GetAssetPath(((lilTex)ao2nd).tex);
                        if(!string.IsNullOrEmpty(path))
                        {
                            path = path2;
                            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
                            ao2ndTexture.LoadImage(bytes);
                            bakeMaterial.SetTexture("_PackingTexture1", ao2ndTexture);
                            bakeMaterial.SetFloat("_PackingChannel1", 0.0f);
                            srcTexture = ao2ndTexture;
                        }
                    }
                    if(srcTexture.width <= 2 || string.IsNullOrEmpty(path))
                    {
                        UnityEngine.Object.DestroyImmediate(bakeMaterial);
                        UnityEngine.Object.DestroyImmediate(srcTexture);
                        UnityEngine.Object.DestroyImmediate(ao1stTexture);
                        UnityEngine.Object.DestroyImmediate(ao2ndTexture);
                        return;
                    }

                    // Bake
                    RenderTexture dstTexture = new RenderTexture(srcTexture.width, srcTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    Graphics.Blit(srcTexture, dstTexture, bakeMaterial);
                    Texture2D outTexture = new Texture2D(srcTexture.width, srcTexture.height, TextureFormat.ARGB32, true, true);
                    outTexture.ReadPixels(new Rect(0, 0, srcTexture.width, srcTexture.height), 0, 0);
                    outTexture.Apply();

                    // Save
                    string savePath = Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "_lilAO.png";
                    File.WriteAllBytes(savePath, outTexture.EncodeToPNG());
                    AssetDatabase.Refresh();
                    AssetDatabase.ImportAsset(savePath);
                    Debug.Log("[lilMaterialConverter] AOMap exported at \"" + savePath + "\"");

                    UnityEngine.Object.DestroyImmediate(bakeMaterial);
                    UnityEngine.Object.DestroyImmediate(srcTexture);
                    UnityEngine.Object.DestroyImmediate(dstTexture);
                    UnityEngine.Object.DestroyImmediate(ao1stTexture);
                    UnityEngine.Object.DestroyImmediate(ao2ndTexture);

                    Texture aotex = AssetDatabase.LoadAssetAtPath<Texture>(savePath);
                    material.SetTexture(name, aotex);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------
        // UTS
        private static bool IsUTS(Material material)
        {
            return (material.shader?.name.Contains("UnityChanToonShader") == true) || material.HasProperty("_utsVersion");
        }

        private static bool IsUTS3(Material material)
        {
            return (material.shader?.name.Contains("Toon (Built-in)") == true) || material.HasProperty("_utsVersionX") || material.HasProperty("_utsVersionY") || material.HasProperty("_utsVersionZ");
        }

        private static bool IsUTSClipping(Material material)
        {
            return (material.shader?.name.Contains("_Clipping") == true) || (material.shader == null && !material.HasProperty("_ClippingMask") && !material.HasProperty("_Tweak_transparency")) || material.IsKeywordEnabled("_IS_CLIPPING_MODE") || material.IsKeywordEnabled("_IS_TRANSCLIPPING_OFF");
        }

        private static bool IsUTSTransparent(Material material)
        {
            return (material.shader != null && (material.shader.name.Contains("_TransClipping") || material.shader.name.Contains("_Transparent"))) || (material.shader == null && material.HasProperty("_Tweak_transparency")) || material.IsKeywordEnabled("_IS_CLIPPING_TRANSMODE") || material.IsKeywordEnabled("_IS_TRANSCLIPPING_ON");
        }

        private static bool IsUTSAngelRing(Material material)
        {
            return (material.shader?.name.Contains("AngelRing") == true) || (material.shader == null && material.HasProperty("_AngelRing")) || (IsUTS3(material) && material.HasProperty("_AngelRing"));
        }

        private static bool IsUTSOutline(Material material)
        {
            return (material.shader?.name.Contains("NoOutline") == false) || (material.shader == null && material.HasProperty("_Outline_Width")) || (IsUTS3(material) && !material.IsKeywordEnabled("_DISABLE_OUTLINE"));
        }

        private static bool IsUTSShadingGradeMap(Material material)
        {
            return (material.shader?.name.Contains("ShadingGradeMap") == true) || (material.shader == null && !material.HasProperty("_ShadingGradeMap")) || material.IsKeywordEnabled("_SHADINGGRADEMAP");
        }

        private static bool IsUTSStencilMask(Material material)
        {
            return material.shader?.name.Contains("StencilMask") == true || IsUTS3(material) && (GetMaterialFloat(material, "_StencilMode") == 2);
        }

        private static bool IsUTSStencilOut(Material material)
        {
            return material.shader?.name.Contains("StencilOut") == true || IsUTS3(material) && (GetMaterialFloat(material, "_StencilMode") == 1);
        }

        private static void GetUTSProperties(ref lilToonConvertProperties d, Material material)
        {
            d.isCutout = IsUTSClipping(material);
            d.isTransparent = IsUTSTransparent(material);
            d.isOutline = IsUTSOutline(material);
            bool isShadingGradeMap = IsUTSShadingGradeMap(material);

            // Main
            d._MainTex = GetMaterialTexture(material, "_MainTex");
            d._MainColor = GetMaterialColorNA(material, "_BaseColor") ?? Color.white;
            d._Cull = GetMaterialFloat(material, "_CullMode");

            // AlphaMask
            if(d.isCutout || d.isTransparent)
            {
                if(!GetMaterialToggle(material, "_IsBaseMapAlphaAsClippingMask"))
                {
                    d._AlphaMask = GetMaterialTexture(material, "_ClippingMask");
                }
                if(GetMaterialToggle(material, "_Inverse_Clipping"))
                {
                    d._AlphaMaskScale = -1.0f;
                    d._AlphaMaskValue = 1.0f;
                    d._AlphaMaskValue += GetMaterialFloat(material, "_Tweak_transparency") ?? 0.0f;
                }
                else
                {
                    d._AlphaMaskScale = 1.0f;
                    d._AlphaMaskValue = GetMaterialFloat(material, "_Tweak_transparency");
                }
                if(d._AlphaMask != null) d._AlphaMaskMode = 1.0f;
                d._Cutoff = Mathf.Clamp01(0.5f - (GetMaterialFloat(material, "_Clipping_Level") ?? 0.0f));
            }

            // Lighting
            d._AsUnlit = 1.0f - (GetMaterialFloat(material, "_Is_LightColor_Base") ?? 1.0f);
            //d._LightMinLimit = (GetMaterialFloat(material, "_Unlit_Intensity") ?? 1.0f) * 0.05f;
            d._LightMinLimit = 0.05f; // Avoid lighting differences

            // Shadow
            d._UseShadow = 1.0f;
            d._ShadowColor = GetMaterialColorNA(material, "_1st_ShadeColor") ?? Color.white;
            d._ShadowColor = ColorDiv((Color)d._ShadowColor, (Color)d._MainColor);
            d._ShadowBorderMax = GetMaterialFloat(material, "_BaseColor_Step") ?? 0.5f;
            d._ShadowBorderMin = d._ShadowBorderMax - (GetMaterialFloat(material, "_BaseShade_Feather") ?? 0.25f);
            if(!GetMaterialToggle(material, "_Use_BaseAs1st"))
            {
                d._ShadowColorTex = GetMaterialTexture(material, "_1st_ShadeMap");
            }

            d._Shadow2ndColor = GetMaterialColorNA(material, "_2nd_ShadeColor") ?? Color.white;
            d._Shadow2ndColor = ColorDiv((Color)d._Shadow2ndColor, (Color)d._MainColor);
            d._Shadow2ndBorderMax = GetMaterialFloat(material, "_ShadeColor_Step") ?? 0.25f;
            d._Shadow2ndBorderMin = d._Shadow2ndBorderMax - (GetMaterialFloat(material, "_1st2nd_Shades_Feather") ?? 0.125f);
            if(!GetMaterialToggle(material, "_Use_1stAs2nd"))
            {
                d._Shadow2ndColorTex = GetMaterialTexture(material, "_2nd_ShadeMap");
            }

            d._ShadowNormalStrength = GetMaterialFloat(material, "_Is_NormalMapToBase");
            d._Shadow2ndNormalStrength = d._ShadowNormalStrength;
            d._ShadowReceive = GetMaterialFloat(material, "_Set_SystemShadowsToBase");
            d._Shadow2ndReceive = d._ShadowReceive;
            d._ShadowBorderColor = Color.black;
            d._ShadowMainStrength = 0.0f;

            if(isShadingGradeMap)
            {
                d._ShadowAO = GetMaterialTexture(material, "_ShadingGradeMap");
                d._Shadow2ndAO = GetMaterialTexture(material, "_ShadingGradeMap");
                float tweak_ShadingGradeMapLevel = GetMaterialFloat(material, "_Tweak_ShadingGradeMapLevel") ?? 0.0f;
                d._ShadowAOShift = new Vector4(1.0f,tweak_ShadingGradeMapLevel,1.0f,tweak_ShadingGradeMapLevel);
                d._ShadowPostAO = 0.0f;
            }
            else
            {
                d._ShadowAO = GetMaterialTexture(material, "_Set_1st_ShadePosition");
                d._Shadow2ndAO = GetMaterialTexture(material, "_Set_2nd_ShadePosition");
                d._ShadowPostAO = 1.0f;
                if((d._ShadowAO != null && ((lilTex)d._ShadowAO).tex != null) && !(d._Shadow2ndAO != null && ((lilTex)d._Shadow2ndAO).tex != null))
                {
                    d._ShadowAOShift = new Vector4(1.0f,0.0f,0.0f,1.0f);
                }
                else if(!(d._ShadowAO != null && ((lilTex)d._ShadowAO).tex != null) && (d._Shadow2ndAO != null && ((lilTex)d._Shadow2ndAO).tex != null))
                {
                    d._ShadowAOShift = new Vector4(0.0f,1.0f,1.0f,0.0f);
                }
                else
                {
                    d._ShadowAOShift = new Vector4(1.0f,0.0f,1.0f,0.0f);
                }
            }

            // Fix shadow color
            if(d._ShadowBorderMin < 0.0f)
            {
                float blendFactor = -(float)d._ShadowBorderMin / ((float)d._ShadowBorderMax - (float)d._ShadowBorderMin);
                d._ShadowColor = Color.Lerp((Color)d._ShadowColor, Color.white, blendFactor);
                d._ShadowBorderMin = 0.0f;
            }
            if(d._Shadow2ndBorderMin < 0.0f)
            {
                float blendFactor = -(float)d._Shadow2ndBorderMin / ((float)d._Shadow2ndBorderMax - (float)d._Shadow2ndBorderMin);
                d._Shadow2ndColor = Color.Lerp((Color)d._Shadow2ndColor, (Color)d._ShadowColor, blendFactor);
                d._Shadow2ndBorderMin = 0.0f;
            }

            if(d._MainTex != null && ((lilTex)d._MainTex).tex != null)
            {
                if(d._ShadowColorTex != null && ((lilTex)d._ShadowColorTex).tex != null && ((lilTex)d._MainTex).tex == ((lilTex)d._ShadowColorTex).tex) d._ShadowColorTex = null;
                if(d._Shadow2ndColorTex != null && ((lilTex)d._Shadow2ndColorTex).tex != null && ((lilTex)d._MainTex).tex == ((lilTex)d._Shadow2ndColorTex).tex) d._Shadow2ndColorTex = null;
            }

            // Normal
            d._BumpMap = GetMaterialTexture(material, "_NormalMap");
            d._BumpScale = GetMaterialFloat(material, "_BumpScale");
            d._UseBumpMap = d._BumpMap != null ? 1.0f : 0.0f;

            // Reflection
            d._ReflectionColor = GetMaterialColorNA(material, "_HighColor");
            d._ReflectionColorTex = GetMaterialTexture(material, "_HighColor_Tex");
            if(d._ReflectionColorTex == null || ((lilTex)d._ReflectionColorTex).tex == null)
            {
                d._ReflectionColorTex = GetMaterialTexture(material, "_Set_HighColorMask");
            }
            d._UseReflection = IsColorBlack(d._ReflectionColor) ? 0.0f : 1.0f;
            d._SpecularNormalStrength = GetMaterialFloat(material, "_Is_NormalMapToHighColor");

            float highColor_Power = GetMaterialFloat(material, "_HighColor_Power") ?? 0.0f;
            d._SpecularToon = 1.0f;
            if(GetMaterialToggle(material, "_Is_SpecularToHighColor"))
            {
                d._Smoothness = Mathf.Clamp01(
                    0.969f
                    - 0.136f * highColor_Power
                    + 0.0387f * highColor_Power * highColor_Power
                    - 0.725f * highColor_Power * highColor_Power * highColor_Power
                );
                d._SpecularBorderMax = 1.0f;
                d._SpecularBorderMin = 0.0f;
            }
            else
            {
                d._SpecularBorderMax = 1.0f - (highColor_Power * highColor_Power * highColor_Power * highColor_Power * highColor_Power * 2.0f);
                d._SpecularBorderMax = Mathf.Clamp01((float)d._SpecularBorderMax);
                d._SpecularBorderMin = d._SpecularBorderMax;
                d._Smoothness = 0.0f;
            }

            // MatCap
            d._UseMatCap = GetMaterialFloat(material, "_MatCap");
            d._MatCapColor = GetMaterialColorNA(material, "_MatCapColor");
            d._MatCapTex = GetMaterialTexture(material, "_MatCap_Sampler");
            d._MatCapEnableLighting = GetMaterialFloat(material, "_Is_LightColor_MatCap");
            d._MatCapLod = GetMaterialFloat(material, "_BlurLevelMatcap");
            d._MatCapBlendMode = GetMaterialToggle(material, "_Is_BlendAddToMatCap") ? 1.0f : 3.0f;
            if(d._MatCapTex != null)
            {
                float tweak_MatCapUV = GetMaterialFloat(material, "_Tweak_MatCapUV") ?? 0.0f;
                tweak_MatCapUV = tweak_MatCapUV == 0.5f ? 999999.0f : 0.5f / (0.5f - tweak_MatCapUV);
                float offsetX = ((lilTex)d._MatCapTex).offset.x;
                float offsetY = ((lilTex)d._MatCapTex).offset.y;
                float scaleX = ((lilTex)d._MatCapTex).scale.x;
                float scaleY = ((lilTex)d._MatCapTex).scale.y;
                Vector2 Offset = new Vector2(offsetX + scaleX - 1.0f, offsetY + scaleY - 1.0f);
                Vector2 Scale = new Vector2(scaleX * tweak_MatCapUV, scaleY * tweak_MatCapUV);
                SetScaleAndOffset(ref d._MatCapTex, Scale, Offset);
            }
            if(d._MatCapTex == null || ((lilTex)d._MatCapTex).tex == null)
            {
                d._UseMatCap = 0.0f;
            }
            d._MatCapShadowMask = GetMaterialToggle(material, "_Is_UseTweakMatCapOnShadow") ? 1.0f - (GetMaterialFloat(material, "_TweakMatCapOnShadow") ?? 1.0f) : 0.0f;
            d._MatCapBlendMask = GetMaterialTexture(material, "_Set_MatcapMask");
            d._MatCapPerspective = GetMaterialToggle(material, "_Is_Ortho") ? 0.0f : 1.0f;
            d._MatCapZRotCancel = GetMaterialFloat(material, "_CameraRolling_Stabilizer");
            d._MatCapNormalStrength = 0.0f;
            d._MatCapCustomNormal = GetMaterialFloat(material, "_Is_NormalMapForMatCap");
            d._MatCapBumpMap = GetMaterialTexture(material, "_NormalMapForMatCap");
            d._MatCapBumpScale = GetMaterialFloat(material, "_BumpScaleMatcap");

            // AngelRing
            if(!IsUTSAngelRing(material))
            {
                // Nothing to do
            }
            else if(d._UseMatCap == null || d._UseMatCap != 1.0f)
            {
                d._UseMatCap = GetMaterialFloat(material, "_AngelRing");
                d._MatCapColor = GetMaterialColorNA(material, "_AngelRing_Color");
                d._MatCapTex = GetMaterialTexture(material, "_AngelRing_Sampler");
                d._MatCapEnableLighting = GetMaterialFloat(material, "_Is_LightColor_AR");
                d._MatCapLod = 0.0f;
                d._MatCapBlendMode = GetMaterialToggle(material, "_ARSampler_AlphaOn") ? 0.0f : 1.0f;
                d._MatCapBlendUV1 = new Vector4(0.0f, 1.0f - (GetMaterialFloat(material, "_AR_OffsetV") ?? 1.0f), 0.0f, 0.0f);
                d._MatCapShadowMask = 0.0f;
                d._MatCapBlendMask = null;
                d._MatCapPerspective = 0.0f;
                d._MatCapZRotCancel = 1.0f;
                d._MatCapNormalStrength = 0.0f;
                d._MatCapCustomNormal = 0.0f;
                if(d._MatCapTex != null)
                {
                    float ar_OffsetU = GetMaterialFloat(material, "_AR_OffsetU") ?? 0.0f;
                    ar_OffsetU = 1.0f - ar_OffsetU;
                    float offsetX = ((lilTex)d._MatCapTex).offset.x;
                    float offsetY = ((lilTex)d._MatCapTex).offset.y;
                    float scaleX = ((lilTex)d._MatCapTex).scale.x;
                    float scaleY = ((lilTex)d._MatCapTex).scale.y;
                    Vector2 Offset = new Vector2(offsetX + scaleX - 1.0f, offsetY + scaleY - 1.0f);
                    Vector2 Scale = new Vector2(scaleX * ar_OffsetU, scaleY * ar_OffsetU);
                    SetScaleAndOffset(ref d._MatCapTex, Scale, Offset);
                }
            }
            else
            {
                d._UseMatCap2nd = GetMaterialFloat(material, "_AngelRing");
                d._MatCap2ndColor = GetMaterialColorNA(material, "_AngelRing_Color");
                d._MatCap2ndTex = GetMaterialTexture(material, "_AngelRing_Sampler");
                d._MatCap2ndEnableLighting = GetMaterialFloat(material, "_Is_LightColor_AR");
                d._MatCap2ndLod = 0.0f;
                d._MatCap2ndBlendMode = GetMaterialToggle(material, "_ARSampler_AlphaOn") ? 0.0f : 1.0f;
                d._MatCap2ndBlendUV1 = new Vector4(0.0f, 1.0f - (GetMaterialFloat(material, "_AR_OffsetV") ?? 1.0f), 0.0f, 0.0f);
                d._MatCap2ndShadowMask = 0.0f;
                d._MatCap2ndBlendMask = null;
                d._MatCap2ndPerspective = 0.0f;
                d._MatCap2ndZRotCancel = 1.0f;
                d._MatCap2ndNormalStrength = 0.0f;
                d._MatCap2ndCustomNormal = 0.0f;
                if(d._MatCap2ndTex != null)
                {
                    float ar_OffsetU = GetMaterialFloat(material, "_AR_OffsetU") ?? 0.0f;
                    ar_OffsetU = 1.0f - ar_OffsetU;
                    float offsetX = ((lilTex)d._MatCap2ndTex).offset.x;
                    float offsetY = ((lilTex)d._MatCap2ndTex).offset.y;
                    float scaleX = ((lilTex)d._MatCap2ndTex).scale.x;
                    float scaleY = ((lilTex)d._MatCap2ndTex).scale.y;
                    Vector2 Offset = new Vector2(offsetX + scaleX - 1.0f, offsetY + scaleY - 1.0f);
                    Vector2 Scale = new Vector2(scaleX * ar_OffsetU, scaleY * ar_OffsetU);
                    SetScaleAndOffset(ref d._MatCap2ndTex, Scale, Offset);
                }
            }

            // Rim
            float rimLight_Power = GetMaterialFloat(material, "_RimLight_Power") ?? 1.0f;
            float tweak_RimLightMaskLevel = GetMaterialFloat(material, "_Tweak_RimLightMaskLevel") ?? 0.0f;
            tweak_RimLightMaskLevel = Mathf.Pow(1.0f + tweak_RimLightMaskLevel, 0.45f);
            float tweak_LightDirection_MaskLevel = GetMaterialFloat(material, "_Tweak_LightDirection_MaskLevel") ?? 0.0f;
            float rimLight_InsideMask = GetMaterialFloat(material, "_RimLight_InsideMask") ?? 0.0f;
            d._UseRim = GetMaterialFloat(material, "_RimLight");
            d._RimColor = GetMaterialColorNA(material, "_RimLightColor") ?? Color.black;
            d._RimColorTex = GetMaterialTexture(material, "_Set_RimLightMask");
            d._RimEnableLighting = GetMaterialFloat(material, "_Is_LightColor_RimLight");
            d._RimNormalStrength = GetMaterialFloat(material, "_Is_NormalMapToRimLight");
            d._RimFresnelPower = Mathf.Pow(2.0f, 3.0f - (3.0f * rimLight_Power));
            d._RimDirStrength = GetMaterialFloat(material, "_LightDirection_MaskOn");
            tweak_LightDirection_MaskLevel = d._RimDirStrength > 0.5f ? tweak_LightDirection_MaskLevel + 0.2f : 0.0f;
            d._RimDirRange = 0.0f;
            d._RimIndirRange = 0.0f;
            d._RimBorderMin = rimLight_InsideMask + tweak_LightDirection_MaskLevel - rimLight_InsideMask * tweak_LightDirection_MaskLevel;
            d._RimIndirBorderMin = d._RimBorderMin;
            d._RimBorderMax = GetMaterialToggle(material, "_RimLight_FeatherOff") ? d._RimBorderMin : 1.0f;
            d._RimIndirBorderMax = GetMaterialToggle(material, "_Ap_RimLight_FeatherOff") ? d._RimBorderMin : 1.0f;
            d._RimIndirColor = GetMaterialToggle(material, "_Add_Antipodean_RimLight") ? GetMaterialColorNA(material, "_Ap_RimLightColor") : Color.black;
            d._RimColor = Color.Lerp(Color.black, (Color)d._RimColor, tweak_RimLightMaskLevel);
            d._RimIndirColor = Color.Lerp(Color.black, (Color)d._RimIndirColor, tweak_RimLightMaskLevel);

            // Emission
            d._EmissionMap = GetMaterialTexture(material, "_Emissive_Tex");
            d._EmissionColor = GetMaterialColorNA(material, "_Emissive_Color");
            d._UseEmission = IsColorBlack(d._EmissionColor) ? 0.0f : 1.0f;
            if(material.IsKeywordEnabled("_EMISSIVE_ANIMATION"))
            {
                float base_Speed = GetMaterialFloat(material, "_Base_Speed") ?? 0.0f;
                float rotate_EmissiveUV = GetMaterialFloat(material, "_Rotate_EmissiveUV") ?? 0.0f;
                float scroll_EmissiveU = GetMaterialFloat(material, "_Scroll_EmissiveU") ?? 0.0f;
                float scroll_EmissiveV = GetMaterialFloat(material, "_Scroll_EmissiveV") ?? 0.0f;
                d._EmissionMap_ScrollRotate = new Vector4(-base_Speed * scroll_EmissiveU, -base_Speed * scroll_EmissiveV, 0.0f, -base_Speed * rotate_EmissiveUV * Mathf.PI);
            }

            // Outline
            if(d.isOutline)
            {
                d._OutlineFixWidth = 0.0f;
                d._OutlineWidth = 0.1f * (GetMaterialFloat(material, "_Outline_Width") ?? 0.0f);
                d._OutlineWidthMask = GetMaterialTexture(material, "_Outline_Sampler");
                d._OutlineColor = GetMaterialColorNA(material, "_Outline_Color");
                if(GetMaterialToggle(material, "_Is_OutlineTex"))
                {
                    d._OutlineTex = GetMaterialTexture(material, "_OutlineTex");
                }
                else if(GetMaterialToggle(material, "_Is_BlendBaseColor"))
                {
                    d._OutlineTex = GetMaterialTexture(material, "_MainTex");
                }
                if(GetMaterialToggle(material, "_Is_BakedNormal"))
                {
                    d._OutlineVectorTex = GetMaterialTexture(material, "_BakedNormal");
                }
            }

            if(IsUTSStencilMask(material))
            {
                d._StencilRef = GetMaterialFloat(material, "_StencilNo");
                d._OutlineStencilRef = GetMaterialFloat(material, "_StencilNo");
                d._StencilComp = (float)UnityEngine.Rendering.CompareFunction.Always;
                d._StencilPass = (float)UnityEngine.Rendering.StencilOp.Replace;
                d._StencilFail = (float)UnityEngine.Rendering.StencilOp.Replace;
                d._OutlineStencilComp = (float)UnityEngine.Rendering.CompareFunction.Always;
                d._OutlineStencilPass = (float)UnityEngine.Rendering.StencilOp.Replace;
                d._OutlineStencilFail = (float)UnityEngine.Rendering.StencilOp.Replace;
            }
            if(IsUTSStencilOut(material))
            {
                d._StencilRef = GetMaterialFloat(material, "_StencilNo");
                d._OutlineStencilRef = GetMaterialFloat(material, "_StencilNo");
                d._StencilComp = (float)UnityEngine.Rendering.CompareFunction.NotEqual;
                d._StencilPass = (float)UnityEngine.Rendering.StencilOp.Keep;
                d._StencilFail = (float)UnityEngine.Rendering.StencilOp.Keep;
                d._OutlineStencilComp = (float)UnityEngine.Rendering.CompareFunction.NotEqual;
                d._OutlineStencilPass = (float)UnityEngine.Rendering.StencilOp.Keep;
                d._OutlineStencilFail = (float)UnityEngine.Rendering.StencilOp.Keep;
            }

            if(GetMaterialToggle(material, "_Is_BLD"))
            {
                float x = (GetMaterialFloat(material, "_Offset_X_Axis_BLD") ?? 0.0f) * 1000.0f;
                float y = (GetMaterialFloat(material, "_Offset_Y_Axis_BLD") ?? 0.0f) * 1000.0f;
                float z = GetMaterialToggle(material, "_Inverse_Z_Axis_BLD") ? -100.0f : 100.0f;
                float w = 1.0f;
                d._LightDirectionOverride = new Vector4(x,y,z,w);
            }

            d._RenderQueue = material.renderQueue;
        }
    }
}
#endif