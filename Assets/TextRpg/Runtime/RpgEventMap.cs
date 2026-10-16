using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGame.TextRpg
{
    [Serializable]
    public sealed class CellEvent
    {
        [Range(0, 14), Tooltip("A=0, B=1, ... O=14")] public int column;
        [Range(1, 10)] public int row = 1;
        public RpgEvent encounter;
    }

    [CreateAssetMenu(menuName = "Text RPG/Event Map", fileName = "New Event Map")]
    public sealed class RpgEventMap : ScriptableObject
    {
        public const int Columns = 15, Rows = 10;
        [Tooltip("개별 지정하지 않은 모든 칸의 이벤트입니다.")] public RpgEvent defaultEvent;
        public List<CellEvent> overrides = new List<CellEvent>();

        public RpgEvent GetEvent(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 1 || row > Rows) return null;
            if (overrides != null)
                foreach (CellEvent cell in overrides)
                    if (cell != null && cell.column == column && cell.row == row) return cell.encounter;
            return defaultEvent;
        }

        public bool IsValid(out string error)
        {
            var seen = new HashSet<int>();
            if (overrides != null)
                foreach (CellEvent cell in overrides)
                {
                    if (cell == null || cell.column < 0 || cell.column >= Columns || cell.row < 1 || cell.row > Rows ||
                        !seen.Add((cell.row - 1) * Columns + cell.column))
                    { error = "맵에 잘못된 좌표 또는 중복 지정된 칸이 있습니다."; return false; }
                }
            for (int row = 1; row <= Rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    RpgEvent encounter = GetEvent(column, row);
                    if (encounter == null) { error = $"{(char)('A' + column)}{row}에 이벤트가 없습니다."; return false; }
                    if (!encounter.IsValid(out error)) return false;
                }
            error = null;
            return true;
        }
    }
}
