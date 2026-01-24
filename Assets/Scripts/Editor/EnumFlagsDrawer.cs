#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
public sealed class EnumFlagsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        // property.intValue sis‰lt‰‰ taustalla enum-arvon (bittimaski)
        var enumType = fieldInfo.FieldType;
        var current = (Enum)Enum.ToObject(enumType, property.intValue);

        var next = EditorGUI.EnumFlagsField(position, label, current);

        property.intValue = Convert.ToInt32(next);

        EditorGUI.EndProperty();
    }
}
#endif
