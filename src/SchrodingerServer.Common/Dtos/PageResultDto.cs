namespace SchrodingerServer.Common.Dtos;

public class PageResultDto<T>
{
    public PageResultDto()
    {
        Data = new List<T>();
        TotalRecordCount = 0;
    }

    public PageResultDto(long totalRecordCount, List<T> data)
    {
        Data = data;
        TotalRecordCount = totalRecordCount;
    }

    public List<T> Data { get; set; }
    public long TotalRecordCount { get; set; }
}