using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace FoodCarryPolicy
{
    // 음식 정책 하나에 딸린 "소지 개수" 목록. FoodPolicy 에 필드를 못 붙이니 ConditionalWeakTable 로 매단다.
    public class FoodCarryData
    {
        public Dictionary<ThingDef, int> counts = new Dictionary<ThingDef, int>();

        public int Get(ThingDef def) => counts.TryGetValue(def, out int n) ? n : 0;

        public void Set(ThingDef def, int n)
        {
            if (n <= 0) counts.Remove(def);
            else counts[def] = n;
        }
    }

    public static class FoodCarry
    {
        private static readonly ConditionalWeakTable<FoodPolicy, FoodCarryData> table =
            new ConditionalWeakTable<FoodPolicy, FoodCarryData>();

        private static List<ThingDef> carriableFoods;

        public static FoodCarryData For(FoodPolicy policy) => table.GetValue(policy, _ => new FoodCarryData());

        public static FoodCarryData For(Pawn pawn)
        {
            FoodPolicy policy = pawn.foodRestriction?.CurrentFoodPolicy;
            return policy == null ? null : For(policy);
        }

        public static int CarryCount(Pawn pawn, ThingDef def) => For(pawn)?.Get(def) ?? 0;

        public static List<ThingDef> CarriableFoods
        {
            get
            {
                if (carriableFoods == null)
                {
                    carriableFoods = DefDatabase<ThingDef>.AllDefs
                        .Where(d => d.category == ThingCategory.Item && d.IsNutritionGivingIngestible && !d.IsDrug && !d.IsCorpse)
                        .OrderBy(d => d.label)
                        .ToList();
                }
                return carriableFoods;
            }
        }
    }
}
