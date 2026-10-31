using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodCarryPolicy
{
    // "음식 추가..." 선택 창. 음식 종류가 수백 개라 플로팅 메뉴 대신 검색 + 보유 중 필터 + 스크롤 목록을 쓴다.
    public class Dialog_PickCarryFood : Window
    {
        private const float RowHeight = 28f;
        private const string SearchControl = "FCP_Search";

        private readonly FoodCarryData data;
        private readonly Dictionary<ThingDef, int> ownedCounts = new Dictionary<ThingDef, int>();
        private readonly List<ThingDef> filtered = new List<ThingDef>();
        private string search = "";
        private bool ownedOnly = true;
        private bool focused;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(380f, 560f);

        public Dialog_PickCarryFood(FoodCarryData data)
        {
            this.data = data;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            // 창을 연 시점의 창고 보유량. 매 프레임 세지 않는다
            Map map = Find.CurrentMap;
            if (map != null)
            {
                foreach (ThingDef def in FoodCarry.CarriableFoods)
                {
                    int n = map.resourceCounter.GetCount(def);
                    if (n > 0) ownedCounts[def] = n;
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "FCP_PickFoodTitle".Translate());
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(inRect.x, inRect.y + 38f, inRect.width, 28f);
            GUI.SetNextControlName(SearchControl);
            search = Widgets.TextField(searchRect, search);
            if (!focused)
            {
                GUI.FocusControl(SearchControl);
                focused = true;
            }

            Rect ownedRect = new Rect(inRect.x, searchRect.yMax + 6f, inRect.width, 24f);
            Widgets.CheckboxLabeled(ownedRect, "FCP_OwnedOnly".Translate(), ref ownedOnly);

            Rect listRect = new Rect(inRect.x, ownedRect.yMax + 6f, inRect.width, inRect.yMax - ownedRect.yMax - 6f);
            Widgets.DrawMenuSection(listRect);
            listRect = listRect.ContractedBy(1f);

            filtered.Clear();
            foreach (ThingDef def in FoodCarry.CarriableFoods)
            {
                if (data.counts.ContainsKey(def)) continue;
                if (ownedOnly && !ownedCounts.ContainsKey(def)) continue;
                if (search.Length > 0 && def.label.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                filtered.Add(def);
            }
            if (filtered.Count == 0)
            {
                GUI.color = Color.grey;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listRect, "FCP_NoMatch".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, filtered.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            for (int i = 0; i < filtered.Count; i++)
            {
                if (DoRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), filtered[i], i))
                {
                    data.Set(filtered[i], 1);
                    Close();
                    break;
                }
            }
            Widgets.EndScrollView();
        }

        private bool DoRow(Rect rect, ThingDef def, int index)
        {
            if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
            Widgets.DrawHighlightIfMouseover(rect);
            Widgets.ThingIcon(new Rect(rect.x + 4f, rect.y + 2f, 24f, 24f), def);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 34f, rect.y, rect.width - 100f, rect.height), def.LabelCap);
            if (ownedCounts.TryGetValue(def, out int n))
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.grey;
                Widgets.Label(new Rect(rect.xMax - 64f, rect.y, 60f, rect.height), n.ToString());
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.ButtonInvisible(rect);
        }
    }
}
