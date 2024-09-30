using System;
using Newtonsoft.Json;

namespace SchrodingerServer.Dto;

public class LevelInfoDto
{
    
    public string SingleProbability { get; set; }
    public string Items { get; set; }
    public string Situation { get; set; }
    public string TotalProbability { get; set; }
    public string Token { get; set; }
    public string Classify { get; set; }
    public string Level { get; set; }
    public string Grade { get; set; }
    public string Star { get; set; }
    
    public string Describe{ get; set; }

    public string AwakenPrice{ get; set; }
    
    public string SpecialTrait { get; set; }
    
    public LevelInfoDto DeepCopy()
    {
        var json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<LevelInfoDto>(json);
    }

}