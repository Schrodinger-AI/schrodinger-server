using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class DomainUserRelationShipQuery
{
    public DomainUserRelationShipListDto QueryUserAsync { get; set; }
}

public class DomainUserRelationShipListDto
{
    public long TotalRecordCount { get; set; }
    public List<DomainUserRelationShipDto> Data { get; set; }
}

public class DomainUserRelationShipDto
{
    public string Id { get; set; }

    public string Domain { get; set; }

    public string Address { get; set; }

    public string DappName { get; set; }

    public long CreateTime { get; set; }
}