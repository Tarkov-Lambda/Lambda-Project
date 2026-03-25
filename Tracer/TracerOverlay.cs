using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ifp.tracer
{
    public partial class TracerOverlay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F3;

        private const float FreqThreshold  = 5f;
        private const float FlashDuration  = 2f;
        private const float ColdPruneAfter = 3f;

        private const float PanelWidth      = 560f;
        private const float PropsPanelWidth = 560f;
        private const float DetailWidth  = 1200f;
        private const float RowHeight    = 24f;
        private const float RecordRowH   = 22f;
        private const float HeaderHeight = 30f;
        private const float TitleHeight  = 32f;
        private const float SectionGap   = 6f;
        private const float PanelPadY    = 16f;

        private const int HotDisplayCount  = 5;
        private const int ColdDisplayCount = 20;

        internal static bool IsVisible { get; private set; }

        private bool    _visible;
        private string  _selectedKey   = null;   // "TypeName.MethodName"
        private float   _nextSnapshotTime;

        private Vector2 _scrollHot    = Vector2.zero;
        private Vector2 _scrollCold   = Vector2.zero;
        private Vector2 _scrollProps  = Vector2.zero;
        private Vector2 _scrollDetail = Vector2.zero;

        private readonly HashSet<float> _expandedRecords = new HashSet<float>();
        private TracedCallRecord[]       _frozenSnapshot  = null;

        private bool            _wasCursorVisible;
        private CursorLockMode  _prevLockMode;
        private Harmony         _inputBlockHarmony;

        private static TracerOverlay s_activeInstance;

        private List<TracedMethodInfo> _hotSnapshot   = new();
        private List<TracedMethodInfo> _coldSnapshot  = new();
        private List<TracedMethodInfo> _propsSnapshot = new();   // all get_* methods


        private void Awake()
        {
            s_activeInstance = this;
            Harmony.UnpatchID("com.ifp.respawn.tracer.inputblock");

            _visible  = false;
            IsVisible = false;

            _inputBlockHarmony = new Harmony("com.ifp.respawn.tracer.inputblock");
            InputBlockPatches.Apply(_inputBlockHarmony);
        }

        private void OnDestroy()
        {
            if (this != s_activeInstance) return;

            s_activeInstance = null;
            _inputBlockHarmony?.UnpatchSelf();

            if (_visible)
            {
                Cursor.visible   = _wasCursorVisible;
                Cursor.lockState = _prevLockMode;
            }

            IsVisible = false;
        }

        private void Update()
        {
            if (this != s_activeInstance) return;

            if (Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
                IsVisible = _visible;

                if (_visible)
                {
                    _wasCursorVisible = Cursor.visible;
                    _prevLockMode     = Cursor.lockState;
                    Cursor.visible    = true;
                    Cursor.lockState  = CursorLockMode.None;
                }
                else
                {
                    Cursor.visible   = _wasCursorVisible;
                    Cursor.lockState = _prevLockMode;
                }
            }

            if (_visible)
            {
                Cursor.visible   = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (!_visible) return;

            if (Time.realtimeSinceStartup >= _nextSnapshotTime)
                RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (this != s_activeInstance) return;
            if (!_visible) return;
            EnsureStyles();

            DrawPropertiesPanel();
            DrawMainPanel();
            if (_selectedKey != null) DrawDetailPanel();

            if (Event.current.isMouse)
                Event.current.Use();
        }

        private void RefreshSnapshot()
        {
            _nextSnapshotTime = Time.realtimeSinceStartup + 0.25f;

            var all  = DynamicClassTracer.TracedData.Values.ToList();
            float now = Time.realtimeSinceStartup;

            // Property getters get their own live-value section.
            _propsSnapshot = all
                .Where(m => m.MethodName.StartsWith("get_"))
                .OrderBy(m => m.TypeName)
                .ThenBy(m => m.MethodName)
                .ToList();

            // Everything else goes into hot / cold.
            var nonProps = all.Where(m => !m.MethodName.StartsWith("get_")).ToList();

            _hotSnapshot = nonProps
                .Where(m => m.CallsPerSecond >= FreqThreshold)
                .OrderBy(m => m.MethodName)
                .ToList();

            _coldSnapshot = nonProps
                .Where(m => m.CallsPerSecond < FreqThreshold
                         && (now - m.LastCallTime) <= ColdPruneAfter)
                .OrderBy(m => m.MethodName)
                .ToList();
        }

        private static string MKey(TracedMethodInfo info) => $"{info.TypeName}.{info.MethodName}";
    }
}
