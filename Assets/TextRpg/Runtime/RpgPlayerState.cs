using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapGame.TextRpg
{
    [Serializable]
    public sealed class RpgPlayerState
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int health = 100;
        [SerializeField, Min(0)] private int strength = 5, agility = 5, intelligence = 5, luck = 5;
        [SerializeField] private List<ItemAmount> inventory = new List<ItemAmount>();

        public int MaxHealth => Math.Max(1, maxHealth);
        public int Health => Math.Max(0, Math.Min(MaxHealth, health));
        public bool IsAlive => Health > 0;
        public IReadOnlyList<ItemAmount> Inventory => inventory;

        public int GetStat(RpgStat stat)
        {
            switch (stat)
            {
                case RpgStat.Strength: return Math.Max(0, strength);
                case RpgStat.Agility: return Math.Max(0, agility);
                case RpgStat.Intelligence: return Math.Max(0, intelligence);
                case RpgStat.Luck: return Math.Max(0, luck);
                default: throw new ArgumentOutOfRangeException(nameof(stat));
            }
        }

        internal static int AddClamped(int value, long change, int minimum)
            => (int)Math.Max(minimum, Math.Min(int.MaxValue, (long)value + change));

        public int CountItem(RpgItem item)
        {
            if (item == null) return 0;
            long total = 0;
            foreach (ItemAmount stack in inventory)
                if (stack != null && stack.item == item) total += Math.Max(0, stack.amount);
            return (int)Math.Min(int.MaxValue, total);
        }

        public void AddItem(RpgItem item, int amount)
        {
            if (item == null || amount <= 0) throw new ArgumentException("아이템과 양수 수량이 필요합니다.");
            SetItemCount(item, AddClamped(CountItem(item), amount, 0));
        }

        internal void SetItemCount(RpgItem item, int count)
        {
            inventory.RemoveAll(stack => stack == null || stack.item == item);
            if (count > 0) inventory.Add(new ItemAmount { item = item, amount = count });
        }

        internal void Apply(EventOutcome result)
        {
            int oldHealth = Health;
            maxHealth = AddClamped(MaxHealth, result.maxHealthChange, 1);
            health = Math.Min(maxHealth, AddClamped(oldHealth, result.healthChange, 0));
            foreach (StatChange change in result.stats)
            {
                int next = AddClamped(GetStat(change.stat), change.amount, 0);
                switch (change.stat)
                {
                    case RpgStat.Strength: strength = next; break;
                    case RpgStat.Agility: agility = next; break;
                    case RpgStat.Intelligence: intelligence = next; break;
                    case RpgStat.Luck: luck = next; break;
                }
            }
            foreach (ItemAmount reward in result.rewards) AddItem(reward.item, reward.amount);
        }
    }
}
