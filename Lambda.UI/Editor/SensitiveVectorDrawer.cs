#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(SensitiveVectorAttribute))]
public class SensitiveVectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool isVector2 = property.propertyType == SerializedPropertyType.Vector2;
        bool isVector3 = property.propertyType == SerializedPropertyType.Vector3;

        if (!isVector2 && !isVector3)
        {
            EditorGUI.LabelField(position, label.text, "Use SensitiveVectorAttribute with Vector2 or Vector3 only.");
            return;
        }

        var sensitiveAttribute = (SensitiveVectorAttribute)attribute;
        float sensitivity = sensitiveAttribute.dragSensitivity;

        EditorGUI.BeginProperty(position, label, property);

        Rect controlRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        int dimensions = isVector3 ? 3 : 2;
        Vector3 vector = isVector3 ? property.vector3Value : (Vector3)property.vector2Value;

        const float labelWidth = 14f;

        float fieldWidth = (controlRect.width / dimensions) - labelWidth - 2f;

        controlRect.height = EditorGUIUtility.singleLineHeight;

        float currentX = controlRect.x;

        GUIContent[] componentLabels = { new GUIContent("X"), new GUIContent("Y"), new GUIContent("Z") };

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < dimensions; i++)
        {
            Rect labelRect = new Rect(currentX, controlRect.y, labelWidth, controlRect.height);

            Rect fieldRect = new Rect(currentX + labelWidth, controlRect.y, fieldWidth, controlRect.height);

            vector[i] = DragValue(labelRect, vector[i], sensitivity);

            vector[i] = EditorGUI.FloatField(fieldRect, vector[i]);

            GUI.Label(labelRect, componentLabels[i], EditorStyles.label);

            currentX += labelWidth + fieldWidth + 3f;
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (isVector3)
            {
                property.vector3Value = vector;
            }
            else
            {
                property.vector2Value = new Vector2(vector.x, vector.y);
            }
        }

        EditorGUI.EndProperty();
    }

    private float DragValue(Rect dragRect, float value, float sensitivity)
    {
        int controlID = GUIUtility.GetControlID(FocusType.Passive, dragRect);

        Event currentEvent = Event.current;
        EventType eventType = currentEvent.GetTypeForControl(controlID);

        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.ResizeHorizontal);

        switch (eventType)
        {
            case EventType.MouseDown:
                if (dragRect.Contains(currentEvent.mousePosition) && currentEvent.button == 0)
                {
                    GUIUtility.hotControl = controlID;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID)
                {
                    value += currentEvent.delta.x * sensitivity;
                    GUI.changed = true;
                    currentEvent.Use();
                }
                break;
        }

        return value;
    }
}
#endif