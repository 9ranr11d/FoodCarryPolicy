using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodCarryPolicy
{
    // 음식 정책 창을 넓히고 오른쪽에 "소지품에 보관" 패널을 붙인다.
    [HarmonyPatch(typeof(Dialog_ManageFoodPolicies), nameof(Dialog_ManageFoodPolicies.InitialSize), MethodType.Getter)]
    public static class Patch_Dialog_ManageFoodPolicies_InitialSize
    {
        public static void Postfix(ref Vector2 __result)
        {
            __result.x += CarryPanel.Width + CarryPanel.Gap;
        }
    }

    [HarmonyPatch(typeof(Dialog_ManageFoodPolicies), "DoContentsRect")]
    public static class Patch_Dialog_ManageFoodPolicies_DoContentsRect
    {
        private static readonly Func<Dialog_ManagePolicies<FoodPolicy>, FoodPolicy> getSelectedPolicy =
            AccessTools.MethodDelegate<Func<Dialog_ManagePolicies<FoodPolicy>, FoodPolicy>>(
                AccessTools.PropertyGetter(typeof(Dialog_ManagePolicies<FoodPolicy>), "SelectedPolicy"));

        public static void Prefix(Dialog_ManageFoodPolicies __instance, ref Rect rect)
        {
            FoodPolicy policy = getSelectedPolicy(__instance);
            if (policy == null)
            {
                return;
            }
            Rect panel = new Rect(rect.xMax - CarryPanel.Width, rect.y, CarryPanel.Width, rect.height);
            rect.width -= CarryPanel.Width + CarryPanel.Gap;
            CarryPanel.Draw(panel, policy);
        }
    }

    public static class CarryPanel
    {
        public const float Width = 280f;
        public const float Gap = 10f;
        private const float RowHeight = 30f;
        private const int MaxCarry = 999;

        private static Vector2 scrollPosition;
        private static FoodPolicy bufferPolicy;
        private static readonly Dictionary<ThingDef, string> buffers = new Dictionary<ThingDef, string>();
        private static readonly List<ThingDef> tmpDefs = new List<ThingDef>();

        public static void Draw(Rect rect, FoodPolicy policy)
        {
            if (bufferPolicy != policy)
            {
                bufferPolicy = policy;
                buffers.Clear();
            }
            FoodCarryData data = FoodCarry.For(policy);

            Rect header = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(header, "FCP_CarryHeader".Translate());
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(header, "FCP_CarryTip".Translate());

            Rect addButton = new Rect(rect.x, header.yMax + 4f, rect.width, 30f);
            if (Widgets.ButtonText(addButton, "FCP_AddFood".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickCarryFood(data));
            }

            Rect listRect = new Rect(rect.x, addButton.yMax + 6f, rect.width, rect.yMax - addButton.yMax - 6f);
            Widgets.DrawMenuSection(listRect);
            listRect = listRect.ContractedBy(1f);
            if (data.counts.Count == 0)
            {
                GUI.color = Color.grey;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listRect, "FCP_NoCarry".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            tmpDefs.Clear();
            tmpDefs.AddRange(data.counts.Keys);
            tmpDefs.SortBy(d => d.label);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, tmpDefs.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            for (int i = 0; i < tmpDefs.Count; i++)
            {
                DoRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), tmpDefs[i], data, i);
            }
            Widgets.EndScrollView();
        }

        private static void DoRow(Rect rect, ThingDef def, FoodCarryData data, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rect);
            }
            float x = rect.x + 4f;
            Widgets.ThingIcon(new Rect(x, rect.y + 3f, 24f, 24f), def);
            x += 28f;

            Rect deleteRect = new Rect(rect.xMax - 26f, rect.y + 3f, 24f, 24f);
            Rect fieldRect = new Rect(deleteRect.x - 64f, rect.y + 3f, 60f, 24f);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, rect.y, fieldRect.x - x - 4f, rect.height), def.LabelCap.Truncate(fieldRect.x - x - 4f));
            Text.Anchor = TextAnchor.UpperLeft;

            int count = data.Get(def);
            buffers.TryGetValue(def, out string buffer);
            // 약물 정책 칸과 같은 입력 위젯. 상한은 바닐라 GetMaxAllowedToPickUp 이 음식엔 0 을 돌려주므로 따로 둔다
            Widgets.TextFieldNumeric(fieldRect, ref count, ref buffer, 0f, MaxCarry);
            buffers[def] = buffer;
            // 0 을 입력해도 행은 남겨 두고, X 로만 지운다 (입력 중 행이 사라지지 않게)
            data.counts[def] = count;

            if (Widgets.ButtonImage(deleteRect, TexButton.Delete))
            {
                data.counts.Remove(def);
                buffers.Remove(def);
            }
        }
    }
}
