using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderSense.Api.Data.Entities;

[Table("geo_zip")]
public class GeoZip
{
    [Key]
    [MaxLength(5)]
    public required string ZipPrefix { get; set; }

    public double Lat { get; set; }

    public double Lng { get; set; }
}