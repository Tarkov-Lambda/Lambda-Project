using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public class UIBackgroundDisplacementEffect : MonoBehaviour, IMaterialModifier
{
    public Texture2D displacementMap;
    [SensitiveVector]
    public Vector2 displacementStrength = new Vector2(0.05f, 0.05f);
    public Vector2 displacementScale = new Vector2(1f, 1f);

    [SerializeField]
    [HideInInspector]
    private Shader _shader;

    private Material _customMaterial;

    private Graphic _graphic;

    const string ShaderAssetName = "UI/BackgroundDisplacement";

    private Graphic GraphicComponent
    {
        get
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            return _graphic;
        }
    }

    protected void OnEnable()
    {
        GraphicComponent.SetMaterialDirty();
    }

    protected void OnDisable()
    {
        if (_customMaterial != null)
        {
            if (Application.isPlaying) Destroy(_customMaterial);
            else DestroyImmediate(_customMaterial);

            _customMaterial = null;
        }

        if (GraphicComponent != null)
        {
            GraphicComponent.SetMaterialDirty();
        }
    }

#if UNITY_EDITOR
    void Reset()
    {
        EnsureShaderReferenceSerialized();
    }

    void OnValidate()
    {
        EnsureShaderReferenceSerialized();
        if (isActiveAndEnabled && GraphicComponent != null)
        {
            GraphicComponent.SetMaterialDirty();
        }
    }

    // Shader.Find will fail at runtime if the shader asset was sideloaded, so make sure the reference is serialized
    void EnsureShaderReferenceSerialized()
    {
        _shader = Shader.Find(ShaderAssetName);
        if (_shader == null)
        {
            Debug.LogError(ShaderAssetName + " not found!", this);
        }
    }
#endif

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || GraphicComponent == null)
            return baseMaterial;

#if UNITY_EDITOR
        EnsureShaderReferenceSerialized();
#endif

        if (_shader == null)
            return baseMaterial;

        if (_customMaterial == null || _customMaterial.shader != _shader)
        {
            _customMaterial = new Material(_shader);
            _customMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        _customMaterial.CopyPropertiesFromMaterial(baseMaterial);

        if (displacementMap != null)
        {
            _customMaterial.SetTexture("_DisplacementMap", displacementMap);
        }

        _customMaterial.SetVector("_DisplacementStrength", displacementStrength);
        _customMaterial.SetVector("_DisplacementScale", displacementScale);

        return _customMaterial;
    }
}
