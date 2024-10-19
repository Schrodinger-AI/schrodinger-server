using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Adopts;

public class GetVotesOutput
{
    public int Countdown { get; set; }
    public List<long> Votes { get; set; }
}