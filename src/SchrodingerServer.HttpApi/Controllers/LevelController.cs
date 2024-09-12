using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Dto;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Users")]
[Route("api/app")]
public class LevelController : AbpController
{
    private readonly ILevelProvider _levelProvider;

    public LevelController(ILevelProvider levelProvider)
    {
        _levelProvider = levelProvider;
    }

    [HttpPost("item/level")]
    public async Task<List<RankData>> GetItemLevelInfoAsync(GetLevelInfoInputDto input)
    {
        var catsTraits = input.CatsTraits;
        foreach (var catTraits in catsTraits)
        {
            var gen2To9Traits = catsTraits.LastOrDefault();
            foreach (var traits in gen2To9Traits)
            {
                var traitValues = traits.LastOrDefault();
                LinkedListNode<string> currentNode = traitValues.First;
                while (currentNode != null)
                {
                    if (currentNode.Value == "WUKONG Face Paint")
                    {
                        currentNode.Value = "Monkey King Face Paint";
                        break;
                    }
                    currentNode = currentNode.Next;
                }
            }
        }
        
        return await _levelProvider.GetItemLevelAsync(input);
    }
}