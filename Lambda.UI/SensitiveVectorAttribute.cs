using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensitiveVectorAttribute : PropertyAttribute
{
    public readonly float dragSensitivity;

    public SensitiveVectorAttribute(float dragSensitivity = 0.001f)
    {
        this.dragSensitivity = dragSensitivity;
    }
}