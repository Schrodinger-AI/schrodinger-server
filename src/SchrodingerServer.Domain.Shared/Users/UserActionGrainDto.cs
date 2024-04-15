using System;
using System.Collections.Generic;
using SchrodingerServer.Common;

namespace SchrodingerServer.Users;

public class UserActionGrainDto
{

    public Guid UserId;
    
    /// <summary>
    ///     <see cref="ActionType"/>
    ///     actionType => actionTime
    /// </summary>
    public Dictionary<string, long> ActionData = new();


}