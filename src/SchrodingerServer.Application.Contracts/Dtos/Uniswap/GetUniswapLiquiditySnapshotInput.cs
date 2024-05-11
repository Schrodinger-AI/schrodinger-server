using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.Dtos.Uniswap;

public class GetUniswapLiquidityInput : PagedAndSortedResultRequestDto
{
    [Required]public string Date { get; set; }
}