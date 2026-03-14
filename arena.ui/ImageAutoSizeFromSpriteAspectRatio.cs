// Written by Claude 4.6

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Automatically sizes an Image to preserve its Sprite's aspect ratio
/// when used inside a HorizontalLayoutGroup or VerticalLayoutGroup.
///
/// Reads the parent layout group's <c>Control Child Size</c> flags:
/// <list type="bullet">
///   <item>Parent controls <b>width</b>  → component computes <b>height</b> from the aspect ratio.</item>
///   <item>Parent controls <b>height</b> → component computes <b>width</b>  from the aspect ratio.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
[DisallowMultipleComponent]
public class ImageAutoSizeFromSpriteAspectRatio : UIBehaviour, ILayoutSelfController, ILayoutElement
{
    // ──────────────────────────────────────────────────────────────
    // Cached references
    // ──────────────────────────────────────────────────────────────
    private Image _image;
    private RectTransform _rectTransform;
    private DrivenRectTransformTracker _tracker;

    private Image CachedImage =>
        _image != null ? _image : (_image = GetComponent<Image>());

    private RectTransform CachedRectTransform =>
        _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

    // ──────────────────────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────────────────────
    private float _cachedPreferredWidth  = -1f;
    private float _cachedPreferredHeight = -1f;

    private bool _parentControlsWidth;
    private bool _parentControlsHeight;

    /// <summary>Width / Height of the active sprite (1 when no sprite).</summary>
    private float SpriteAspectRatio
    {
        get
        {
            Sprite sprite = CachedImage.sprite;
            if (sprite == null) return 1f;
            Rect r = sprite.rect;
            return r.height > 0f ? r.width / r.height : 1f;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Parent layout group detection
    // ──────────────────────────────────────────────────────────────
    private void DetectParentLayoutControl()
    {
        _parentControlsWidth  = false;
        _parentControlsHeight = false;

        Transform parent = transform.parent;
        if (parent == null) return;

        if (parent.TryGetComponent<HorizontalLayoutGroup>(out var hlg) && hlg.enabled)
        {
            _parentControlsWidth  = hlg.childControlWidth;
            _parentControlsHeight = hlg.childControlHeight;
        }
        else if (parent.TryGetComponent<VerticalLayoutGroup>(out var vlg) && vlg.enabled)
        {
            _parentControlsWidth  = vlg.childControlWidth;
            _parentControlsHeight = vlg.childControlHeight;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ILayoutElement
    //
    //   layoutPriority = 1 so we can selectively override values
    //   reported by Image (priority 0).
    //   Returning -1 means "I have no opinion" — the system falls
    //   back to the next-lower-priority ILayoutElement (Image).
    // ──────────────────────────────────────────────────────────────
    public int   layoutPriority  => 1;
    public float minWidth        => -1f;
    public float minHeight       => -1f;
    public float flexibleWidth   => -1f;
    public float flexibleHeight  => -1f;
    public float preferredWidth  => _cachedPreferredWidth;
    public float preferredHeight => _cachedPreferredHeight;

    /// <summary>
    /// Called bottom-up BEFORE the horizontal layout pass.
    /// At this point widths have NOT been assigned yet.
    /// </summary>
    public void CalculateLayoutInputHorizontal()
    {
        DetectParentLayoutControl();

        if (_parentControlsHeight && !_parentControlsWidth)
        {
            // HeightControlsWidth mode.
            // We can't know the final width yet (height hasn't been set).
            // Report the sprite's native width so the parent has something
            // reasonable; we'll fix the actual width in SetLayoutVertical.
            Sprite sprite = CachedImage.sprite;
            _cachedPreferredWidth = sprite != null ? sprite.rect.width : -1f;
        }
        else
        {
            _cachedPreferredWidth = -1f; // let Image decide
        }
    }

    /// <summary>
    /// Called bottom-up BEFORE the vertical layout pass.
    /// At this point the horizontal pass is complete, so
    /// <c>rectTransform.rect.width</c> is already valid.
    /// </summary>
    public void CalculateLayoutInputVertical()
    {
        if (_parentControlsWidth && !_parentControlsHeight)
        {
            Sprite sprite = CachedImage.sprite;
            if (sprite == null)
            {
                _cachedPreferredHeight = -1f;
                return;
            }

            // WidthControlsHeight mode: derive height from the
            // already-assigned width.
            float width = CachedRectTransform.rect.width;
            _cachedPreferredHeight = width / SpriteAspectRatio;
        }
        else
        {
            _cachedPreferredHeight = -1f; // let Image decide
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ILayoutSelfController
    //
    //   Self-controllers run AFTER the parent ILayoutGroup has
    //   already assigned sizes on the same axis, so by the time
    //   SetLayoutVertical executes, both the parent-controlled
    //   width (horizontal pass) and height (vertical pass) are
    //   available.
    // ──────────────────────────────────────────────────────────────
    public void SetLayoutHorizontal()
    {
        _tracker.Clear();
        // Nothing to set here — if we need to drive width
        // (HeightControlsWidth), height isn't known yet.
        // We handle it in SetLayoutVertical instead.
    }

    public void SetLayoutVertical()
    {
        if (CachedImage.sprite == null) return;

        float aspect = SpriteAspectRatio;

        if (_parentControlsWidth && !_parentControlsHeight)
        {
            // Parent owns width → we drive height.
            float width  = CachedRectTransform.rect.width;
            float height = width / aspect;

            _tracker.Add(this, CachedRectTransform,
                         DrivenTransformProperties.SizeDeltaY);
            CachedRectTransform.SetSizeWithCurrentAnchors(
                         RectTransform.Axis.Vertical, height);
        }
        else if (_parentControlsHeight && !_parentControlsWidth)
        {
            // Parent owns height → we drive width.
            float height = CachedRectTransform.rect.height;
            float width  = height * aspect;

            _tracker.Add(this, CachedRectTransform,
                         DrivenTransformProperties.SizeDeltaX);
            CachedRectTransform.SetSizeWithCurrentAnchors(
                         RectTransform.Axis.Horizontal, width);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // UIBehaviour lifetime & dirty helpers
    // ──────────────────────────────────────────────────────────────
    protected override void OnEnable()
    {
        base.OnEnable();
        SetDirty();
    }

    protected override void OnDisable()
    {
        _tracker.Clear();
        LayoutRebuilder.MarkLayoutForRebuild(CachedRectTransform);
        base.OnDisable();
    }

    protected override void OnTransformParentChanged()
    {
        SetDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        SetDirty();
    }

    private void SetDirty()
    {
        if (!IsActive()) return;
        LayoutRebuilder.MarkLayoutForRebuild(CachedRectTransform);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        SetDirty();
    }
#endif
}