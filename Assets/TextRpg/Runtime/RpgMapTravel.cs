using System;

namespace MapGame.TextRpg
{
    public sealed class RpgMapTravel
    {
        public int Column { get; private set; }
        public int Row { get; private set; }
        public bool AllowDiagonal { get; }
        private readonly RpgEventSession session;
        private readonly RpgEventMap map;

        public RpgMapTravel(RpgEventSession session, RpgEventMap map, int column, int row, bool allowDiagonal)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.map = map;
            if (!InBounds(column, row)) throw new ArgumentOutOfRangeException(nameof(column));
            Column = column;
            Row = row;
            AllowDiagonal = allowDiagonal;
        }

        public static bool InBounds(int column, int row)
            => column >= 0 && column < RpgEventMap.Columns && row >= 1 && row <= RpgEventMap.Rows;

        public bool IsNeighbour(int column, int row)
        {
            if (!InBounds(column, row)) return false;
            int dx = Math.Abs(column - Column), dy = Math.Abs(row - Row);
            return AllowDiagonal ? Math.Max(dx, dy) == 1 : dx + dy == 1;
        }

        public bool TryMoveTo(int column, int row, out string error)
        {
            if (!IsNeighbour(column, row)) { error = "주변의 이동 가능한 칸을 눌러 주세요."; return false; }
            if (!session.TryVisit(map, column, row, out error)) return false;
            // Failed event entry must never move the player.
            Column = column;
            Row = row;
            return true;
        }

        // Coordinates are normalized relative to the actual board, with (0,0) at its top left.
        public static bool TryGetCell(float x, float y, out int column, out int row)
        {
            column = row = 0;
            if (float.IsNaN(x) || float.IsNaN(y) || x < 0 || x > 1 || y < 0 || y > 1) return false;
            column = Math.Min(RpgEventMap.Columns - 1, (int)(x * RpgEventMap.Columns));
            row = Math.Min(RpgEventMap.Rows - 1, (int)(y * RpgEventMap.Rows)) + 1;
            return true;
        }
    }
}
