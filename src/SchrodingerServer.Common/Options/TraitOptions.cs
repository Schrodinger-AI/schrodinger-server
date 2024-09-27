namespace SchrodingerServer.Common.Options;

public class TraitOptions
{
    public List<SpecialTrait> SpecialTraits { get; set; } = new();
    public string CurrentId { get; set; }
    public class SpecialTrait
    {
        public string Id { get; set; }
        public string Tag { get; set; }
        public Dictionary<string, Dictionary<string, string>> ReplaceTraits { get; set; } = new(); // {Type: { NewValue: OldValue } }
    }
}