using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodCarryPolicy
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("9ranr11d.FoodCarryPolicy").PatchAll();
        }
    }

    // 저장: 정책 노드 안에 소지 목록을 같이 쓴다. 모드를 빼도 바닐라는 모르는 노드를 무시한다.
    [HarmonyPatch(typeof(FoodPolicy), nameof(FoodPolicy.ExposeData))]
    public static class Patch_FoodPolicy_ExposeData
    {
        public static void Postfix(FoodPolicy __instance)
        {
            FoodCarryData data = FoodCarry.For(__instance);
            List<ThingDef> defs = null;
            List<int> counts = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                defs = new List<ThingDef>(data.counts.Keys);
                counts = new List<int>(data.counts.Values);
            }
            Scribe_Collections.Look(ref defs, "fcp_carryDefs", LookMode.Def);
            Scribe_Collections.Look(ref counts, "fcp_carryCounts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                data.counts.Clear();
                if (defs != null && counts != null)
                {
                    // 삭제된 모드의 음식은 null 로 들어오므로 건너뛴다
                    for (int i = 0; i < defs.Count && i < counts.Count; i++)
                    {
                        if (defs[i] != null) data.Set(defs[i], counts[i]);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(FoodPolicy), nameof(FoodPolicy.CopyFrom))]
    public static class Patch_FoodPolicy_CopyFrom
    {
        public static void Postfix(FoodPolicy __instance, Policy other)
        {
            if (other is FoodPolicy src)
            {
                FoodCarry.For(__instance).counts = new Dictionary<ThingDef, int>(FoodCarry.For(src).counts);
            }
        }
    }

    // 소지 목록은 짐 내려놓기(카라반 복귀 후 등)에서 남긴다.
    // 바닐라 getter 를 그대로 옮기고 음식 소지 개수만 보관 목록에 더했다 (1.6.4871 기준).
    [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.FirstUnloadableThing), MethodType.Getter)]
    public static class Patch_Pawn_InventoryTracker_FirstUnloadableThing
    {
        private static readonly Dictionary<ThingDef, int> keep = new Dictionary<ThingDef, int>();

        private static void AddKeep(ThingDef def, int count)
        {
            keep[def] = Mathf.Max(count, keep.TryGetValue(def, out int n) ? n : 0);
        }

        public static bool Prefix(Pawn_InventoryTracker __instance, ref ThingCount __result)
        {
            Pawn pawn = __instance.pawn;
            ThingOwner<Thing> container = __instance.innerContainer;
            __result = default;
            if (container.Count == 0)
            {
                return false;
            }
            keep.Clear();
            DrugPolicy drugPolicy = pawn.drugs?.CurrentPolicy;
            if (drugPolicy != null)
            {
                for (int i = 0; i < drugPolicy.Count; i++)
                {
                    if (drugPolicy[i].takeToInventory > 0)
                    {
                        AddKeep(drugPolicy[i].drug, drugPolicy[i].takeToInventory);
                    }
                }
            }
            if (pawn.inventoryStock?.stockEntries != null)
            {
                foreach (InventoryStockEntry entry in pawn.inventoryStock.stockEntries.Values)
                {
                    AddKeep(entry.thingDef, entry.count);
                }
            }
            FoodCarryData carry = FoodCarry.For(pawn);
            if (carry != null)
            {
                foreach (var kv in carry.counts)
                {
                    AddKeep(kv.Key, kv.Value);
                }
            }
            foreach (Thing item in container)
            {
                int keepCount = keep.TryGetValue(item.def, out int n) ? n : 0;
                if (pawn.IsColonist && item.def.IsNutritionGivingIngestible && !item.def.IsDrug && JobGiver_PackFood.IsGoodPackableFoodFor(item, pawn, checkMass: false))
                {
                    // 바닐라: 배고픔 게이지 최대치만큼의 휴대 식량은 남긴다
                    float packed = JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn);
                    float maxLevel = pawn.needs.food.MaxLevel;
                    float nutrition = item.GetStatValue(StatDefOf.Nutrition);
                    if (packed - nutrition * item.stackCount <= maxLevel)
                    {
                        int k = 0;
                        while (packed - nutrition * k > maxLevel)
                        {
                            k++;
                        }
                        keepCount = Mathf.Max(keepCount, item.stackCount - k);
                    }
                }
                if (item.stackCount > keepCount)
                {
                    __result = new ThingCount(item, item.stackCount - keepCount);
                    return false;
                }
                keep[item.def] = keepCount - item.stackCount;
            }
            return false;
        }
    }

    // 집 구역에서 오래 안 쓴 날음식을 버리는 동작이 소지 지정분까지 버리지 않게 한다.
    [HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
    public static class Patch_JobGiver_DropUnusedInventory_Drop
    {
        public static bool Prefix(Pawn pawn, Thing thing)
        {
            int carry = FoodCarry.CarryCount(pawn, thing.def);
            if (carry <= 0)
            {
                return true;
            }
            int excess = Mathf.Min(thing.stackCount, pawn.inventory.innerContainer.TotalStackCountOfDef(thing.def) - carry);
            if (excess > 0)
            {
                pawn.inventory.innerContainer.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, excess, out Thing _);
            }
            return false;
        }
    }
}
