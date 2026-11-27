using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models;

public class PrintRequestInput
{
    [Required, StringLength(100)]
    public string CustomerName { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string CustomerEmail { get; set; } = "";

    [Required, StringLength(4000)]
    public string ProjectDescription { get; set; } = "";

    [Range(1, 4)]
    public int RequestedColorsCount { get; set; } = 1;

    [Required]
    public IFormFile? ModelFile { get; set; }

    public IFormFile? ReferenceImage { get; set; }
}
