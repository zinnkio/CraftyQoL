using System;
using System.Collections.Generic;
using System.Globalization;

namespace CraftyQoL.Core
{
    [Flags] internal enum EquipmentFlags { None = 0, Weapons = 1, Armour = 2, Shields = 4, Tools = 8, Ammunition = 16, Magic = 32, Other = 64 }
    internal enum EquipmentCategory { All, Weapons, Armour, Shields, Tools, Ammunition, Magic, Other }
    internal enum EquipmentSort { Vanilla, Alphabetical }

    // Numeric enum contracts are verified against the installed ItemType/SkillType/AttackType.
    // Keeping the reader's plain values here makes classification independently testable.
    internal static class EquipmentClassifier
    {
        internal static EquipmentFlags Classify(string prefab, int type, int skill, double chop, double pickaxe,
            bool consumedProjectile, double eitrRegen)
        {
            // Exact non-semantic identities, never display-name matching. See EQUIPMENT_EXPANSION.
            if (prefab == "KnifeButcher") return EquipmentFlags.Tools;
            if (prefab == "Tankard" || prefab == "TankardAnniversary" || prefab == "TankardOdin") return EquipmentFlags.Other;
            if (prefab == "Catapult_ammo" || prefab == "BombSiege") return EquipmentFlags.Ammunition;
            if (type == 5) return EquipmentFlags.Shields;
            if (type == 6 || type == 7 || type == 11 || type == 12 || type == 17)
                return EquipmentFlags.Armour | (eitrRegen > 0 && !double.IsInfinity(eitrRegen) ? EquipmentFlags.Magic : 0);
            if (type == 9 || type == 23) return EquipmentFlags.Ammunition;
            bool weapon = type == 3 || type == 4 || type == 14 || type == 22;
            if ((weapon || type == 19) && (skill == 12 || pickaxe > 0 || skill == 106)) return EquipmentFlags.Tools;
            EquipmentFlags flags = weapon ? EquipmentFlags.Weapons : EquipmentFlags.None;
            if (type == 19 || (weapon && (skill == 7 || chop > 0))) flags |= EquipmentFlags.Tools;
            if (consumedProjectile) flags |= EquipmentFlags.Ammunition;
            if (weapon && (skill == 9 || skill == 10)) flags |= EquipmentFlags.Magic;
            return flags == EquipmentFlags.None ? EquipmentFlags.Other : flags;
        }
        internal static EquipmentFlags Flag(EquipmentCategory category)
            => category == EquipmentCategory.All ? EquipmentFlags.None : (EquipmentFlags)(1 << ((int)category - 1));
    }

    internal static class EquipmentQuery
    {
        internal static List<RecipeInfo> Filter(IReadOnlyList<RecipeInfo> source, QueryState query, ISet<string> favourites)
        {
            string search = RecipeInfo.Normalize(query.Search.Trim());
            EquipmentFlags category = EquipmentClassifier.Flag(query.EquipmentCategory);
            var result = new List<RecipeInfo>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                RecipeInfo info = source[i];
                if (category != EquipmentFlags.None && (info.Equipment & category) == 0) continue;
                if (RecipeQuery.MatchesCommon(info, query, favourites, search)) result.Add(info);
            }
            return result;
        }
        internal static int Compare(RecipeInfo left, RecipeInfo right)
        {
            int name = CultureInfo.CurrentCulture.CompareInfo.Compare(left.Name, right.Name, CompareOptions.IgnoreCase);
            return name != 0 ? name : StringComparer.Ordinal.Compare(left.Key, right.Key);
        }
    }
}
