using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace FoodCarryPolicy
{
    // JobGiver_MoveDrugsToInventory 의 음식판. 정책에 지정된 개수보다 적게 들고 있으면 가장 가까운 것을 집어온다.
    public class JobGiver_MoveFoodToInventory : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            FoodCarryData data = FoodCarry.For(pawn);
            if (data == null || data.counts.Count == 0 || pawn.inventory == null)
            {
                return 0f;
            }
            foreach (var kv in data.counts)
            {
                if (pawn.inventory.innerContainer.TotalStackCountOfDef(kv.Key) < kv.Value)
                {
                    return 7.5f;
                }
            }
            return 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            FoodCarryData data = FoodCarry.For(pawn);
            if (data == null || pawn.inventory == null || pawn.Map == null)
            {
                return null;
            }
            foreach (var kv in data.counts)
            {
                int num = kv.Value - pawn.inventory.innerContainer.TotalStackCountOfDef(kv.Key);
                if (num <= 0)
                {
                    continue;
                }
                Thing thing = FindFoodFor(pawn, kv.Key, num);
                if (thing != null)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, thing);
                    job.count = Mathf.Min(num, thing.stackCount);
                    return job;
                }
            }
            return null;
        }

        private Thing FindFoodFor(Pawn pawn, ThingDef def, int desired)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(def), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f,
                (Thing x) => !x.IsForbidden(pawn) && x.IngestibleNow && pawn.CanReserve(x, 10, Mathf.Min(x.stackCount, desired)));
        }
    }
}
