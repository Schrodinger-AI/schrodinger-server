using System;
using System.Collections.Generic;
using Orleans;
using SchrodingerServer.Common;

namespace SchrodingerServer.Users;

[GenerateSerializer]
public class UserActionGrainDto
{
    [Id(0)]
    public Guid UserId;
    
    /// <summary>
    ///     <see cref="ActionType"/>
    ///     actionType => actionTime
    /// </summary>
    [Id(1)]
    public Dictionary<string, long> ActionData = new();


}