// Only the asset base types and Inspector attributes are stubbed. All game rules
// come directly from Runtime/*.cs. This does not test Unity serialization or UI.
using System;
namespace UnityEngine
{
    public class ScriptableObject { }
    public class Sprite { }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public sealed class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int min, int max) { }
    }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public sealed class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
}
