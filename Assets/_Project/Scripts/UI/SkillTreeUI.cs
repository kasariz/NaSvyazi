using System.Collections.Generic;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Player;
using NaSvyazi.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Окно дерева навыков (клавиша K): три ветки по три ступени.
    /// Карточка навыка показывает ранг, описание и кнопку «Изучить».
    /// </summary>
    public class SkillTreeUI : ModalWindow
    {
        private class Card
        {
            public SkillDefinition skill;
            public Image background;
            public TextMeshProUGUI rank;
            public Button button;
            public TextMeshProUGUI buttonLabel;
        }

        private readonly List<Card> cards = new List<Card>();
        private GameObject root;
        private TextMeshProUGUI pointsText;
        private PlayerController player;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        private void OnEnable() => SkillManager.Changed += Refresh;
        private void OnDisable() => SkillManager.Changed -= Refresh;

        protected override void Update()
        {
            base.Update();

            bool free = !IsAnyOpen && LastClosedFrame != Time.frameCount && !FurniturePlacer.IsPlacing;
            if (free && GameInput.SkillsPressed)
            {
                if (player == null) player = FindAnyObjectByType<PlayerController>();
                Open(player);
            }
            else if (IsOpen && GameInput.SkillsPressed && LastOpenedFrame != Time.frameCount)
            {
                Close();
            }
        }

        private int LastOpenedFrame { get; set; } = -1;

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        protected override void OnOpened()
        {
            LastOpenedFrame = Time.frameCount;
            Refresh();
        }

        private void Refresh()
        {
            SkillManager skills = SkillManager.Instance;
            if (skills == null || pointsText == null) return;

            pointsText.text = $"Свободных очков: {skills.AvailablePoints}   ·   очко даётся за каждый уровень салона";

            foreach (Card card in cards)
            {
                int rank = skills.RankOf(card.skill.id);
                bool maxed = rank >= card.skill.maxRank;
                bool available = skills.IsAvailable(card.skill);

                card.rank.text = $"Ранг {rank} / {card.skill.maxRank}";
                card.background.color = rank > 0
                    ? new Color(0.86f, 0.18f, 0.55f, 0.18f)
                    : available ? UIFactory.RowColor : new Color(1f, 1f, 1f, 0.02f);

                card.button.interactable = skills.CanUpgrade(card.skill);
                card.buttonLabel.text = maxed ? "Изучено" : !available ? "Закрыто" : "Изучить";
            }
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Skills Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1500f, 860f));
            UIFactory.AddVerticalLayout(window.gameObject, 12f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform, "Навыки салона", 36f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 50f);

            pointsText = UIFactory.CreateText("Points", window.transform, "", 22f, UIFactory.AccentColor);
            UIFactory.SetSize(pointsText, height: 34f);

            RectTransform columns = UIFactory.CreateRect("Columns", window.transform);
            HorizontalLayoutGroup layout = UIFactory.AddHorizontalLayout(columns.gameObject, 20f, 0);
            layout.childForceExpandWidth = true;
            UIFactory.SetSize(columns, flexibleHeight: 1f);

            BuildBranch(columns, SkillBranch.Service, "Сервис", "лояльность и терпение покупателей");
            BuildBranch(columns, SkillBranch.Marketing, "Маркетинг", "сколько покупателей и что они берут");
            BuildBranch(columns, SkillBranch.Logistics, "Логистика", "закупка, доставка и расходы");

            RectTransform footer = UIFactory.CreateRect("Footer", window.transform);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 56f);
            RectTransform spacer = UIFactory.CreateRect("Spacer", footer);
            UIFactory.SetSize(spacer, flexibleWidth: 1f);
            UIFactory.SetSize(UIFactory.CreateButton("Close", footer, "Закрыть (K / Esc)", Close,
                UIFactory.ButtonColor), width: 260f);
        }

        private void BuildBranch(Transform parent, SkillBranch branch, string name, string subtitle)
        {
            RectTransform column = UIFactory.CreateRect($"Branch_{branch}", parent);
            UIFactory.AddVerticalLayout(column.gameObject, 12f, 0);
            UIFactory.SetSize(column, flexibleWidth: 1f, flexibleHeight: 1f);

            TextMeshProUGUI header = UIFactory.CreateText("Header", column, name, 30f, UIFactory.TextColor);
            header.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(header, height: 40f);

            TextMeshProUGUI sub = UIFactory.CreateText("Sub", column, subtitle, 18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(sub, height: 26f);

            foreach (SkillDefinition skill in SkillManager.Definitions)
            {
                if (skill.branch != branch) continue;
                cards.Add(BuildCard(column, skill));
            }
        }

        private Card BuildCard(Transform parent, SkillDefinition skill)
        {
            Image bg = UIFactory.CreatePanel($"Skill_{skill.id}", parent, UIFactory.RowColor);
            UIFactory.AddVerticalLayout(bg.gameObject, 6f, 16);
            UIFactory.SetSize(bg, height: 190f);

            TextMeshProUGUI name = UIFactory.CreateText("Name", bg.transform, $"{skill.tier}. {skill.name}", 24f,
                UIFactory.TextColor);
            name.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(name, height: 32f);

            TextMeshProUGUI rank = UIFactory.CreateText("Rank", bg.transform, "", 18f, UIFactory.AccentColor);
            UIFactory.SetSize(rank, height: 24f);

            TextMeshProUGUI desc = UIFactory.CreateText("Desc", bg.transform, skill.description, 18f,
                UIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 13f;
            desc.fontSizeMax = 18f;
            UIFactory.SetSize(desc, flexibleHeight: 1f);

            string id = skill.id;
            Button button = UIFactory.CreateButton("Upgrade", bg.transform, "Изучить", () =>
            {
                if (SkillManager.Instance != null) SkillManager.Instance.TryUpgrade(id);
            }, UIFactory.AccentColor, 22f);
            UIFactory.SetSize(button, height: 44f);

            return new Card
            {
                skill = skill,
                background = bg,
                rank = rank,
                button = button,
                buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>()
            };
        }
    }
}
