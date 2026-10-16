using System;
using System.Collections.Generic;

namespace MapGame.TextRpg
{
    // One session belongs to one player. Shared event assets never store play progress.
    public sealed class RpgEventSession
    {
        public RpgPlayerState Player { get; }
        public RpgEvent ActiveEvent { get; private set; }
        public ItemInteraction ActiveInteraction { get; private set; }
        public bool IsSpecialInteraction => ActiveInteraction != null;
        public IReadOnlyList<EventChoice> CurrentChoices => ActiveInteraction != null ? ActiveInteraction.choices : ActiveEvent?.choices;
        public string CurrentTitle => ActiveInteraction != null ? ActiveInteraction.title : ActiveEvent?.title;
        public bool HasPendingChoice => ActiveEvent != null;
        public string LastNarrative { get; private set; }
        public bool LastCheckSucceeded { get; private set; }
        private readonly HashSet<int> completedCells = new HashSet<int>();
        private int activeCell;
        private RpgEventMap sessionMap;

        public RpgEventSession(RpgPlayerState player)
        { Player = player ?? throw new ArgumentNullException(nameof(player)); }

        public bool TryEnter(RpgEventMap map, int column, int row, out string error)
            => TryEnterInternal(map, column, row, false, out error);

        // Walking through a resolved cell is allowed without granting its outcome again.
        public bool TryVisit(RpgEventMap map, int column, int row, out string error)
            => TryEnterInternal(map, column, row, true, out error);

        private bool TryEnterInternal(RpgEventMap map, int column, int row, bool allowResolved, out string error)
        {
            if (HasPendingChoice) { error = "현재 이벤트의 선택을 먼저 완료하세요."; return false; }
            if (!Player.IsAlive) { error = "체력이 0인 상태에서는 탐험할 수 없습니다."; return false; }
            if (map == null) { error = "이벤트 맵을 연결하세요."; return false; }
            if (!map.IsValid(out error)) return false;
            if (sessionMap != null && sessionMap != map) { error = "새 맵은 새로운 세션에서 시작하세요."; return false; }
            RpgEvent encounter = map.GetEvent(column, row);
            if (encounter == null) { error = "맵 범위를 벗어난 좌표입니다."; return false; }
            int key = (row - 1) * RpgEventMap.Columns + column;
            if (!encounter.repeatable && completedCells.Contains(key))
            {
                if (!allowResolved) { error = "이미 해결한 칸입니다."; return false; }
                LastNarrative = "이미 탐색한 칸입니다. 주변 칸으로 이동할 수 있습니다.";
                error = null;
                return true;
            }
            sessionMap = map;
            activeCell = key;
            ActiveEvent = encounter;
            ActiveInteraction = null;
            LastNarrative = encounter.narrative;
            error = null;
            return true;
        }

        public bool CanChoose(int index, out string error)
        {
            if (!HasPendingChoice) { error = "진행 중인 이벤트가 없습니다."; return false; }
            if (!Player.IsAlive) { error = "체력이 0입니다."; return false; }
            if (!ActiveEvent.IsValid(out error)) return false;
            if (index < 0 || index >= CurrentChoices.Count) { error = "잘못된 선택지입니다."; return false; }
            error = null;
            return true;
        }

        public bool TryChoose(int index, out string error)
        {
            // Validate all requirements before changing inventory or stats.
            if (!CanChoose(index, out error)) return false;
            EventChoice choice = CurrentChoices[index];
            LastCheckSucceeded = !choice.useStatCheck || Player.GetStat(choice.checkStat) >= choice.minimumStat;
            EventOutcome outcome = LastCheckSucceeded ? choice.success : choice.failure;
            Player.Apply(outcome);
            LastNarrative = outcome.narrative;
            if (!ActiveEvent.repeatable) completedCells.Add(activeCell);
            ActiveEvent = null;
            ActiveInteraction = null;
            return true;
        }

        public bool CanUseItem(RpgItem item, out string error)
        {
            if (!HasPendingChoice) { error = "아이템을 사용할 사건이 없습니다."; return false; }
            if (!Player.IsAlive) { error = "체력이 0입니다."; return false; }
            if (IsSpecialInteraction) { error = "진행 중인 특수 상호작용을 먼저 완료하세요."; return false; }
            if (!ActiveEvent.IsValid(out error)) return false;
            if (item == null) { error = "가방에서 아이템을 선택하세요."; return false; }
            ItemInteraction interaction = ActiveEvent.itemInteractions.Find(entry => entry.item == item);
            if (interaction == null) { error = "이 사건에는 사용할 수 없는 아이템입니다."; return false; }
            if (Player.CountItem(item) < interaction.amount)
            { error = $"{item.displayName} {interaction.amount}개가 필요합니다."; return false; }
            error = null;
            return true;
        }

        public bool TryUseItem(RpgItem item, out string error)
        {
            if (!CanUseItem(item, out error)) return false;
            ItemInteraction interaction = ActiveEvent.itemInteractions.Find(entry => entry.item == item);
            if (interaction.consume) Player.SetItemCount(item, Player.CountItem(item) - interaction.amount);
            ActiveInteraction = interaction;
            LastNarrative = interaction.narrative;
            return true;
        }
    }
}
