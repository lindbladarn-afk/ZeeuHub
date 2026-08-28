namespace Entities.User;

public class JeevesCompanyVM
{
    [Column(name: "ForetagKod")]
    public int CompanyCode { get; set; }

    [Column(name:"Name")]
    public string Name { get; set; }

    [Column(name:"IsDefault")]
    public bool IsDefault { get; set; }
}
