using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace SchrodingerServer.EntityEventHandler.Core.Options;

public class WorkerOptions
{
    public const string DefaultCron = "0 0/3 * * * ?";

    public string[] ChainIds { get; set; } = System.Array.Empty<string>();
    
    public string BizDate { get; set; }

    public List<string> TxPointNames { get; set; } = new ();

    public Dictionary<string, Worker> Workers { get; set; } = new Dictionary<string, Worker>();

    public string GetWorkerBizDate(string workerName)
    {
        var workerBizDate = Workers.TryGetValue(workerName, out var worker) ? worker.BizDate : null;

        return workerBizDate.IsNullOrEmpty() ? BizDate : workerBizDate;
    }
    
    public List<string> GetWorkerBizDateList(string workerName)
    {
        var workerBizDateList = Workers.TryGetValue(workerName, out var worker) ? worker.BizDateList : new List<string>();

        return workerBizDateList;
    }
    
    public int GetWorkerPeriodMinutes(string workerName)
    {
        var minutes = Workers.TryGetValue(workerName, out var worker) ? worker.Minutes : Worker.DefaultMinutes;
        return minutes;
    }
    
    public bool GetWorkerSwitch(string workerName)
    {
        return Workers.TryGetValue(workerName, out var worker) && worker.OpenSwitch;
    }
}


public class Worker
{
    public const int DefaultMinutes = 10;

    public int Minutes { get; set; } = DefaultMinutes;
    public string Cron { get; set; } = WorkerOptions.DefaultCron;
    public string BizDate { get; set; }
    
    public List<string> BizDateList { get; set; }

    public bool OpenSwitch { get; set; } = false;
}