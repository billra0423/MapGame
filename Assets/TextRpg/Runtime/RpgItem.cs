using UnityEngine;

namespace MapGame.TextRpg
{
    [CreateAssetMenu(menuName = "Text RPG/Item", fileName = "New Item")]
    public sealed class RpgItem : ScriptableObject
    {
        public string displayName = "아이템";
        [TextArea] public string description;
        public Sprite icon;
    }
}
