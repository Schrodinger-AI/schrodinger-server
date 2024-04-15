namespace SchrodingerServer.Common.Options;

public class StableDiffusionOption
{
    public string Prompt { get; set; } = "<lora:pixelcat1000lr08b2e8-000002:0.3>, cute cat standing character, ((pixel art)), ";
    public string SamplerIndex { get; set; } = "DPM++ 2M Karras";
    public string NegativePrompt { get; set; } = "NSFW";

    public string SdModelCheckpoint { get; set; } = "revAnimated_v122.safetensors";
    public int Steps { get; set; } = 20;
    public int BatchSize { get; set; } = 2;
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int NIter { get; set; } = 1;
}