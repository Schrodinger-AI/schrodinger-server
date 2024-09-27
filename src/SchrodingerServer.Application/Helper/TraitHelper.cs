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
    
    
    public static List<string> ReplaceTraitValues(TraitOptions traitOptions, List<string> traitTypes, List<string> traitValues)
    {
        var newValues = new List<string>();

        var cnt = traitTypes.Count;
        for (var i = 0; i < cnt; i++)
        {
            var traitType = traitTypes[i];
            var traitValue = traitValues[i];
            var newValue = traitValue;
            
            foreach (var specialTrait in traitOptions.SpecialTraits)
            {
                var replaceItems = specialTrait.ReplaceTraits;
                if (!replaceItems.ContainsKey(traitType))
                {
                    continue;
                }

                var replaceValue = replaceItems[traitType];
                if (!replaceValue.ContainsKey(traitValue))
                {
                    continue;
                }

                newValue = replaceValue[traitValue];
            }
            
            newValues.Add(newValue);
        }
        
        return newValues;
    }
}