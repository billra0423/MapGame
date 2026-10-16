using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGame.TextRpg
{
    public enum RpgStat { Strength, Agility, Intelligence, Luck }

    [Serializable]
    public sealed class ItemAmount
    {
        public RpgItem item;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public sealed class StatChange
    {
        public RpgStat stat;
        public int amount;
    }

    [Serializable]
    public sealed class EventOutcome
    {
        [TextArea(2, 8)] public string narrative = "선택의 결과를 작성하세요.";
        [Tooltip("양수는 회복, 음수는 피해입니다.")] public int healthChange;
        public int maxHealthChange;
        public List<StatChange> stats = new List<StatChange>();
        public List<ItemAmount> rewards = new List<ItemAmount>();
    }

    [Serializable]
    public sealed class EventChoice
    {
        public string label = "선택지";
        [TextArea] public string description;
        public bool useStatCheck;
        public RpgStat checkStat;
        [Min(0)] public int minimumStat = 5;
        public EventOutcome success = new EventOutcome();
        public EventOutcome failure = new EventOutcome();
    }

    [Serializable]
    public sealed class ItemInteraction
    {
        public RpgItem item;
        [Min(1)] public int amount = 1;
        public bool consume = true;
        public string title = "아이템 특수 상호작용";
        [TextArea(3, 8)] public string narrative = "아이템을 사용해 새로운 상황이 펼쳐집니다.";
        public List<EventChoice> choices = new List<EventChoice>
        {
            new EventChoice { label = "계속 진행한다" },
            new EventChoice { label = "조심스럽게 대응한다" }
        };
    }

    [CreateAssetMenu(menuName = "Text RPG/Event", fileName = "New Event")]
    public sealed class RpgEvent : ScriptableObject
    {
        public string title = "새로운 사건";
        [TextArea(3, 12)] public string narrative = "이 칸에서 일어나는 사건을 작성하세요.";
        [Tooltip("해결 후 같은 칸에서 다시 실행할 수 있습니다.")] public bool repeatable;
        public List<EventChoice> choices = new List<EventChoice>
        {
            new EventChoice { label = "조사한다" },
            new EventChoice { label = "지나간다" }
        };
        [Tooltip("기본 선택지와 별개로, 전용 슬롯에서 아이템을 사용했을 때 열리는 사건입니다.")]
        public List<ItemInteraction> itemInteractions = new List<ItemInteraction>();

        // Both the Inspector and the executor reject invalid content; no silent auto-repair.
        public bool IsValid(out string error)
        {
            if (!ValidChoices(choices, out error)) return false;
            if (itemInteractions == null) { error = "특수 상호작용 목록이 없습니다."; return false; }
            var seen = new HashSet<RpgItem>();
            foreach (ItemInteraction interaction in itemInteractions)
            {
                if (interaction == null || interaction.item == null || interaction.amount < 1 || !seen.Add(interaction.item))
                { error = "특수 상호작용의 아이템·수량과 중복 지정을 확인하세요."; return false; }
                if (!ValidChoices(interaction.choices, out error)) return false;
            }
            error = null;
            return true;
        }

        private static bool ValidChoices(List<EventChoice> choices, out string error)
        {
            if (choices == null || choices.Count < 2)
            { error = "이벤트에는 최소 2개의 선택지가 필요합니다."; return false; }
            foreach (EventChoice choice in choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.label) || choice.success == null || choice.failure == null)
                { error = "선택지 이름과 성공/실패 결과를 설정하세요."; return false; }
                if (choice.minimumStat < 0 || !Enum.IsDefined(typeof(RpgStat), choice.checkStat))
                { error = "스탯 판정 설정이 올바르지 않습니다."; return false; }
                if (!ValidOutcome(choice.success) || !ValidOutcome(choice.failure))
                { error = "결과의 보상 아이템·수량 또는 스탯을 확인하세요."; return false; }
            }
            error = null;
            return true;
        }

        private static bool ValidOutcome(EventOutcome result)
        {
            if (result.rewards == null || result.stats == null) return false;
            foreach (ItemAmount reward in result.rewards)
                if (reward == null || reward.item == null || reward.amount < 1) return false;
            foreach (StatChange stat in result.stats)
                if (stat == null || !Enum.IsDefined(typeof(RpgStat), stat.stat)) return false;
            return true;
        }
    }
}
