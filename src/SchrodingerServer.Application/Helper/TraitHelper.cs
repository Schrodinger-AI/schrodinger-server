using System.Collections.Generic;
using System.Linq;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Helper;

public static class TraitHelper
{
    public static string GetSpecialTrait(TraitOptions traitOptions, List<TraitsInfo> Traits)
    {
        var defaultTag = "Elon S1";
        
        var currentTrait = traitOptions.SpecialTraits.Where(x => x.Id == traitOptions.CurrentId).ToList().FirstOrDefault();
        if (currentTrait == null)
        {
            return defaultTag;
        }
        
        var activityTypes = currentTrait.ReplaceTraits.Keys.ToList();
        foreach (var trait in Traits)
        {
            if (!activityTypes.Contains(trait.TraitType))
            {
                continue;
            }

            var activityValues = currentTrait.ReplaceTraits[trait.TraitType];
            if (activityValues.ContainsKey(trait.Value))
            {
                return currentTrait.Tag;
            }
        }
        
        return defaultTag;
    }
}