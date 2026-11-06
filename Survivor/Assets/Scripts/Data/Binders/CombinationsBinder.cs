using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Survivor.Data
{
    /// Binds combinations.json (5.13.3) field by field; weapon/passive references are checked by the loader's reference step.
    internal static class CombinationsBinder
    {
        /// Returns the rules in file order (references unchecked), or null when the file-level structure is broken.
        internal static List<CombinationRule> Bind(JObject root, DataValidationReport report)
        {
            var rules = new List<CombinationRule>();
            return BinderSupport.BindList(root, DataFileKind.Combinations, "combinations", report, rules, BindOne) ? rules : null;
        }

        private static CombinationRule BindOne(FieldReader r)
        {
            return new CombinationRule
            {
                Id = r.DefinitionId,
                WeaponId = r.String("weaponId", true, true),
                PassiveId = r.String("passiveId", true, true),
                ResultWeaponId = r.String("resultWeaponId", true, true),
            };
        }
    }
}
