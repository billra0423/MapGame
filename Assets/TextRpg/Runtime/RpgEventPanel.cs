using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MapGame.TextRpg
{
    // Every UI object and button callback is authored in the scene, not created here.
    public sealed class RpgEventPanel : MonoBehaviour
    {
        [SerializeField] private RpgEventController controller;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text heading;
        [SerializeField] private Text narrative;
        [SerializeField] private Text choiceContext;
        [SerializeField] private Text message;
        [SerializeField] private Text playerSummary;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceLabels;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Text nextPageLabel;
        [SerializeField] private GameObject continueButton;
        [Header("Bag and dedicated item slot (scene UI)")]
        [SerializeField] private Button[] bagButtons;
        [SerializeField] private Text[] bagLabels;
        [SerializeField] private Image[] bagIcons;
        [SerializeField] private Button bagPageButton;
        [SerializeField] private Text bagPageLabel;
        [SerializeField] private GameObject itemSlot;
        [SerializeField] private Text itemSlotLabel;
        [SerializeField] private Text itemUseHint;
        [SerializeField] private Button useItemButton;
        [SerializeField] private Image selectedItemIcon;
        private readonly List<RpgItem> bagItems = new List<RpgItem>();
        private RpgItem selectedItem;
        private int bagPage;
        private int page;
        private int PageSize => choiceButtons == null ? 0 : choiceButtons.Length;

        private void Start() { RefreshSummary(); RefreshBag(); }

        public void ShowEvent()
        {
            page = 0;
            selectedItem = null;
            panel.SetActive(true);
            if (message != null) message.text = "선택을 완료하면 주변 칸으로 이동할 수 있습니다.";
            Refresh();
        }

        public void ShowResult()
        {
            selectedItem = null;
            panel.SetActive(true);
            if (message != null) message.text = controller.Player.IsAlive ? "이동 가능한 주변 칸이 표시됩니다." : "체력이 0입니다. 더 이상 이동할 수 없습니다.";
            Refresh();
        }

        public void ShowError()
        {
            if (message != null) message.text = controller.LastError;
            RefreshSummary();
            RefreshBag();
        }

        public void CloseResult()
        {
            if (!controller.Session.HasPendingChoice) panel.SetActive(false);
        }

        public void ChooseSlot(int slot)
        {
            if (slot < 0 || slot >= PageSize) return;
            controller.Choose(page * PageSize + slot);
        }

        public void NextPage()
        {
            var choices = controller.Session.CurrentChoices;
            if (choices == null || PageSize == 0) return;
            page = (page + 1) % ((choices.Count + PageSize - 1) / PageSize);
            Refresh();
        }

        private void Refresh()
        {
            RpgEvent encounter = controller.CurrentEvent;
            var choices = controller.Session.CurrentChoices;
            bool pending = encounter != null;
            if (heading != null) heading.text = pending ? controller.Session.CurrentTitle : "탐험 결과";
            if (narrative != null) narrative.text = controller.Narrative;
            if (choiceContext != null) choiceContext.text = !pending ? "" : controller.Session.IsSpecialInteraction ? "아이템 특수 상호작용 · 아래에서 행동을 선택하세요" : "기본 선택지 · 아이템을 사용하지 않습니다";
            if (continueButton != null) continueButton.SetActive(!pending);
            for (int slot = 0; slot < PageSize; slot++)
            {
                int index = page * PageSize + slot;
                bool visible = pending && index < choices.Count;
                choiceButtons[slot].gameObject.SetActive(visible);
                if (!visible) continue;
                bool canChoose = controller.Session.CanChoose(index, out string reason);
                choiceButtons[slot].interactable = canChoose;
                if (choiceLabels != null && slot < choiceLabels.Length && choiceLabels[slot] != null)
                {
                    EventChoice choice = choices[index];
                    var label = new StringBuilder(choice.label);
                    if (!canChoose) label.Append("\n").Append(reason);
                    choiceLabels[slot].text = label.ToString();
                }
            }
            bool multiplePages = pending && PageSize > 0 && choices.Count > PageSize;
            if (nextPageButton != null) nextPageButton.gameObject.SetActive(multiplePages);
            if (nextPageLabel != null && multiplePages)
                nextPageLabel.text = $"다음 선택지 ({page + 1}/{(choices.Count + PageSize - 1) / PageSize})";
            RefreshSummary();
            RefreshBag();
        }

        private void RefreshSummary()
        {
            if (controller == null || playerSummary == null) return;
            RpgPlayerState player = controller.Player;
            var text = new StringBuilder($"HP {player.Health}/{player.MaxHealth}\n힘 {player.GetStat(RpgStat.Strength)}  민첩 {player.GetStat(RpgStat.Agility)}\n지능 {player.GetStat(RpgStat.Intelligence)}  행운 {player.GetStat(RpgStat.Luck)}");
            playerSummary.text = text.ToString();
        }

        public void SelectBagSlot(int slot)
        {
            if (bagButtons == null || slot < 0 || slot >= bagButtons.Length) return;
            int index = bagPage * bagButtons.Length + slot;
            if (index >= bagItems.Count) return;
            selectedItem = bagItems[index];
            RefreshItemSlot();
        }

        public void ClearItemSlot() { selectedItem = null; RefreshItemSlot(); }

        public void UseSelectedItem()
        {
            // Validation happens in the session as well; UI selection never consumes anything.
            controller.UseItem(selectedItem);
        }

        public void NextBagPage()
        {
            if (bagButtons == null || bagButtons.Length == 0) return;
            bagPage = (bagPage + 1) % System.Math.Max(1, (bagItems.Count + bagButtons.Length - 1) / bagButtons.Length);
            RefreshBag();
        }

        private void RefreshBag()
        {
            bagItems.Clear();
            foreach (ItemAmount stack in controller.Player.Inventory)
                if (stack != null && stack.item != null && controller.Player.CountItem(stack.item) > 0 && !bagItems.Contains(stack.item)) bagItems.Add(stack.item);
            int size = bagButtons == null ? 0 : bagButtons.Length;
            int pages = size == 0 ? 1 : System.Math.Max(1, (bagItems.Count + size - 1) / size);
            bagPage = System.Math.Min(bagPage, pages - 1);
            for (int slot = 0; slot < size; slot++)
            {
                int index = bagPage * size + slot;
                RpgItem item = index < bagItems.Count ? bagItems[index] : null;
                bagButtons[slot].interactable = item != null && !controller.Session.IsSpecialInteraction;
                if (bagLabels != null && slot < bagLabels.Length && bagLabels[slot] != null)
                    bagLabels[slot].text = item == null ? "비어 있음" : $"{item.displayName}\n×{controller.Player.CountItem(item)}";
                if (bagIcons != null && slot < bagIcons.Length && bagIcons[slot] != null)
                {
                    bagIcons[slot].sprite = item == null ? null : item.icon;
                    bagIcons[slot].enabled = item != null && item.icon != null;
                }
            }
            if (bagPageButton != null) bagPageButton.gameObject.SetActive(pages > 1);
            if (bagPageLabel != null) bagPageLabel.text = $"가방 {bagPage + 1}/{pages} →";
            RefreshItemSlot();
        }

        private void RefreshItemSlot()
        {
            if (selectedItem != null && controller.Player.CountItem(selectedItem) == 0) selectedItem = null;
            bool available = controller.Session.HasPendingChoice && !controller.Session.IsSpecialInteraction;
            if (itemSlot != null) itemSlot.SetActive(available);
            if (itemSlotLabel != null) itemSlotLabel.text = selectedItem == null ? "사용할 아이템\n가방에서 선택" : $"{selectedItem.displayName} ×{controller.Player.CountItem(selectedItem)}\n{selectedItem.description}";
            bool canUse = controller.Session.CanUseItem(selectedItem, out string reason);
            if (useItemButton != null) useItemButton.interactable = canUse;
            if (itemUseHint != null)
            {
                if (canUse)
                {
                    ItemInteraction interaction = controller.CurrentEvent.itemInteractions.Find(entry => entry.item == selectedItem);
                    itemUseHint.text = interaction.consume ? $"사용 시 {interaction.amount}개 소모 · 결과가 더 나빠질 수도 있습니다." : "아이템 유지 · 특수 상황의 결과는 선택에 따라 달라집니다.";
                }
                else itemUseHint.text = reason;
            }
            if (selectedItemIcon != null)
            {
                selectedItemIcon.sprite = selectedItem == null ? null : selectedItem.icon;
                selectedItemIcon.enabled = selectedItem != null && selectedItem.icon != null;
            }
        }
    }
}
