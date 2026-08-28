namespace Entities.ZeeuDashboard;

public class ProductionDashboardVM
{

    public string Id { get; set; }

    [Display(Name = "Anställnings ID")]
    public string PersSign { get; set; }

    [Display(Name = "Namn")]
    public string Name { get; set; }

    [Display(Name="Närvarande")]
    public bool Present { get; set; }

    [Display(Name ="Kom datum")]
    [DisplayFormat(DataFormatString = "{0:d}", ApplyFormatInEditMode = true)]
    public DateTime? ArrivedDate { get; set; }

    [Display(Name = "Kom tid")]
    public DateTime? ArrivedTime { get; set; }

    //public DateTime? LeftDate { get; set; }
    //public DateTime? LeftTime { get; set; }
    [Display(Name="AONR")]
    public string? WorkOrder { get; set; }

    [Display(Name="OPNR")]
    public string? OperationNumber { get; set; }

    [Display(Name ="Prod grp")]
    public string? ProductionGroup { get; set; }

    [Display(Name ="Startad")]
    public DateTime? OperationStarted { get; set; }

    [Display(Name = "Beräknad tid")]
    public double? CalculatedOperationTime { get; set; }

    [Display(Name ="Rapporterad tid")]
    public double? ReportedOperationTime { get; set; }

    [Display(Name="Kvarvarande tid")]
    public double? RemainingTime { get; set; }

    [Display(Name = "Nästa AO")]
    public string? NextWorkOrder { get; set; }

    [Display(Name ="Nästa Grp")]
    public string? NextProductionGroup { get; set; }

}
